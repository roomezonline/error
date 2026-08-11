using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ErrorService.Server.Hubs;
using System.Text.Json;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/chat/bale-webhook")]
public class BaleWebhookController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IHubContext<ChatHub> _hub;
    private readonly IConfiguration _config;
    private readonly ILogger<BaleWebhookController> _logger;
    private readonly BaleBotService _baleBot;
    private readonly IHttpClientFactory _httpClientFactory;

    public BaleWebhookController(ErrorServiceDbContext db, IHubContext<ChatHub> hub,
        IConfiguration config, ILogger<BaleWebhookController> logger, BaleBotService baleBot,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _hub = hub;
        _config = config;
        _logger = logger;
        _baleBot = baleBot;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> TestGet([FromQuery] string? secret, [FromQuery] bool? test = null)
    {
        var expectedSecret = _config["BaleBot:WebhookSecret"] ?? "";
        var match = string.IsNullOrEmpty(expectedSecret) || secret == expectedSecret;

        var result = new Dictionary<string, object?>
        {
            ["status"] = "alive",
            ["secretMatch"] = match,
            ["secretConfigured"] = !string.IsNullOrEmpty(expectedSecret),
            ["currentSiteUrl"] = $"{Request.Scheme}://{Request.Host}/api/chat/bale-webhook" + (!string.IsNullOrEmpty(expectedSecret) ? $"?secret={expectedSecret}" : ""),
        };

        // Check webhook registration with Bale API
        var (token, groupId) = await GetBotSettingsAsync();
        if (!string.IsNullOrEmpty(token))
        {
            var client = _httpClientFactory.CreateClient();
            var baseUrl = $"https://tapi.bale.ai/bot{token}/";

            // getWebhookInfo
            try
            {
                var resp = await client.GetAsync(baseUrl + "getWebhookInfo");
                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("ok", out var ok) && ok.GetBoolean())
                    {
                        var wh = doc.RootElement.GetProperty("result");
                        var registeredUrl = wh.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                        var pendingCount = wh.TryGetProperty("pending_update_count", out var p) ? p.GetInt32() : -1;
                        result["webhookRegistered"] = registeredUrl;
                        result["pendingUpdateCount"] = pendingCount;
                        result["webhookMatch"] = registeredUrl == (string)result["currentSiteUrl"]!;
                        result["botApiReachable"] = true;
                    }
                    else
                    {
                        result["botApiReachable"] = false;
                        result["botApiError"] = body;
                    }
                }
                else
                {
                    result["botApiReachable"] = false;
                    result["botApiError"] = $"HTTP {(int)resp.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                result["botApiReachable"] = false;
                result["botApiError"] = ex.Message;
            }

            // getUpdates (only if test=true)
            if (test == true)
            {
                try
                {
                    var updResp = await client.GetAsync(baseUrl + "getUpdates?timeout=0&offset=-1");
                    var updBody = await updResp.Content.ReadAsStringAsync();
                    using var updDoc = JsonDocument.Parse(updBody);
                    if (updDoc.RootElement.TryGetProperty("ok", out var updOk) && updOk.GetBoolean())
                    {
                        var updates = updDoc.RootElement.GetProperty("result");
                        result["pendingUpdates"] = updates.GetArrayLength();
                        if (updates.GetArrayLength() > 0)
                        {
                            var samples = new List<object>();
                            foreach (var upd in updates.EnumerateArray().Take(3))
                            {
                                var msg = upd.TryGetProperty("message", out var m) ? m : default;
                                samples.Add(new
                                {
                                    updateId = upd.TryGetProperty("update_id", out var ui) ? ui.GetInt64() : 0,
                                    messageId = msg.ValueKind != JsonValueKind.Undefined && msg.TryGetProperty("message_id", out var mi) ? mi.GetInt64() : 0,
                                    text = msg.ValueKind != JsonValueKind.Undefined && msg.TryGetProperty("text", out var t) ? t.GetString() : "",
                                    hasReplyTo = msg.ValueKind != JsonValueKind.Undefined && msg.TryGetProperty("reply_to_message", out var _),
                                    chatId = msg.ValueKind != JsonValueKind.Undefined && msg.TryGetProperty("chat", out var c) && c.TryGetProperty("id", out var ci) ? ci.GetInt64() : 0
                                });
                            }
                            result["pendingUpdateSamples"] = samples;
                        }
                    }
                    else
                    {
                        result["pendingUpdates"] = -1;
                        result["pendingUpdatesError"] = updBody;
                    }
                }
                catch (Exception ex)
                {
                    result["pendingUpdates"] = -2;
                    result["pendingUpdatesError"] = ex.Message;
                }
            }
        }
        else
        {
            result["botTokenConfigured"] = false;
        }

        // Debug log info
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "bale-debug", "webhook.log");
        var logExists = System.IO.File.Exists(logPath);
        result["debugLogExists"] = logExists;
        result["debugLogSize"] = logExists ? new System.IO.FileInfo(logPath).Length : 0;

        return Ok(result);
    }

    private async Task<(string token, string groupId)> GetBotSettingsAsync()
    {
        try
        {
            var settings = await _db.SiteSettings.FirstOrDefaultAsync();
            return (settings?.BaleBotToken?.Trim() ?? "", settings?.BaleBotGroupId?.Trim() ?? "");
        }
        catch { return ("", ""); }
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook([FromQuery] string? secret)
    {
        // Validate webhook secret
        var expectedSecret = _config["BaleBot:WebhookSecret"] ?? "";
        if (!string.IsNullOrEmpty(expectedSecret) && secret != expectedSecret)
        {
            _logger.LogWarning("Bale webhook: invalid secret (expected={Expected}, received={Received})", expectedSecret, secret);
            return Unauthorized();
        }

        // Read raw body for debugging
        string rawBody;
        using (var reader = new StreamReader(Request.Body))
        {
            rawBody = await reader.ReadToEndAsync();
        }

        // Write to debug log
        await WriteDebugLog(rawBody);

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            _logger.LogWarning("Bale webhook: empty body");
            return Ok();
        }

        _logger.LogInformation("Bale webhook: raw body received: {Body}", rawBody);

        // Parse manually with flexible options
        BaleWebhookPayload? payload;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
            payload = JsonSerializer.Deserialize<BaleWebhookPayload>(rawBody, options);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Bale webhook: failed to parse JSON: {Body}", rawBody);
            return Ok();
        }

        if (payload?.Message == null)
        {
            _logger.LogWarning("Bale webhook: Message is null after parsing. Raw: {Body}", rawBody);
            return Ok();
        }

        // Validate chat ID matches configured group
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        var expectedGroupId = settings?.BaleBotGroupId?.Trim() ?? "";
        var actualChatId = payload.Message.Chat?.Id.ToString();
        if (!string.IsNullOrEmpty(expectedGroupId) && actualChatId != expectedGroupId)
        {
            _logger.LogWarning("Bale webhook: chat ID mismatch (expected={Expected}, received={Received})", expectedGroupId, actualChatId);
            return Ok();
        }

        _logger.LogInformation("Bale webhook: received message from chat {ChatId}: {Text}",
            payload.Message.Chat?.Id, payload.Message.Text);

        var replyTo = payload.Message.ReplyToMessage;
        int? sessionId = null;

        if (replyTo != null)
        {
            var replyText = replyTo.Text ?? replyTo.Caption;
            sessionId = BaleBotService.ExtractSessionId(replyText);
            _logger.LogInformation("Bale webhook: reply to message, extracted sessionId={SessionId} from '{ReplyText}'", sessionId, replyText);
        }
        else
        {
            sessionId = BaleBotService.ExtractSessionId(payload.Message.Text);
            _logger.LogInformation("Bale webhook: direct message, extracted sessionId={SessionId} from '{Text}'", sessionId, payload.Message.Text);
        }

        if (sessionId == null)
        {
            _logger.LogWarning("Bale webhook: could not extract sessionId from message");
            return Ok();
        }

        var session = await _db.ChatSessions.FindAsync(sessionId.Value);
        if (session == null)
        {
            _logger.LogWarning("Bale webhook: session {Id} not found", sessionId.Value);
            return Ok();
        }

        if (session.Status == ChatSessionStatus.Closed)
        {
            _logger.LogWarning("Bale webhook: session {Id} is closed", sessionId.Value);
            return Ok();
        }

        var text = payload.Message.Text ?? payload.Message.Caption ?? "";
        var messageType = ChatMessageType.Text;
        string? mediaUrl = null;

        if (payload.Message.Photo is { Count: > 0 })
        {
            messageType = ChatMessageType.Image;
            var largest = payload.Message.Photo.OrderByDescending(p => p.FileSize).First();
            mediaUrl = await _baleBot.DownloadFileAsync(largest.FileId);
        }
        else if (payload.Message.Voice != null)
        {
            messageType = ChatMessageType.Voice;
            mediaUrl = await _baleBot.DownloadFileAsync(payload.Message.Voice.FileId);
        }

        var msg = new ChatMessage
        {
            SessionId = sessionId.Value,
            SenderType = ChatSenderType.Operator,
            SenderId = "اپراتور (بله)",
            Content = text,
            MessageType = messageType,
            MediaUrl = mediaUrl,
            CreatedAt = DateTime.UtcNow
        };

        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync();

        var dto = new ChatMessageDto
        {
            Id = msg.Id,
            SessionId = msg.SessionId,
            SenderType = ChatSenderTypeDto.Operator,
            SenderId = msg.SenderId,
            Content = msg.Content,
            MessageType = (ChatMessageTypeDto)(int)messageType,
            MediaUrl = msg.MediaUrl,
            FileName = msg.FileName,
            FileSize = msg.FileSize,
            ContentType = msg.ContentType,
            CreatedAt = msg.CreatedAt,
            IsRead = msg.IsRead,
            Status = (ChatMessageStatusDto)(int)msg.Status
        };

        _logger.LogInformation("Bale webhook: broadcasting message {MsgId} ({Type}) to session_{SessionId}",
            msg.Id, messageType, sessionId);
        await _hub.Clients.Group($"session_{sessionId}").SendAsync("NewMessage", dto);
        return Ok();
    }

    [HttpGet("log")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public IActionResult GetLog()
    {
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "bale-debug", "webhook.log");
        if (!System.IO.File.Exists(logPath))
            return Ok(new { entries = new string[0] });
        var lines = System.IO.File.ReadAllLines(logPath);
        return Ok(new { entries = lines });
    }

    private async Task WriteDebugLog(string rawBody)
    {
        try
        {
            var logDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "bale-debug");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "webhook.log");
            var entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {rawBody}";
            await System.IO.File.AppendAllTextAsync(logPath, entry + Environment.NewLine);
        }
        catch { }
    }
}
