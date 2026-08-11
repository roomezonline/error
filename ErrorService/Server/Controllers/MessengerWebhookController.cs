using ErrorService.Server.Data;
using ErrorService.Server.Hubs;
using ErrorService.Server.Models;
using ErrorService.Server.Services.Messenger;
using ErrorService.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/chat/messenger-webhook")]
public class MessengerWebhookController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IHubContext<ChatHub> _hub;
    private readonly IConfiguration _config;
    private readonly ILogger<MessengerWebhookController> _logger;
    private readonly MessengerRouter _router;

    public MessengerWebhookController(ErrorServiceDbContext db, IHubContext<ChatHub> hub,
        IConfiguration config, ILogger<MessengerWebhookController> logger, MessengerRouter router)
    {
        _db = db;
        _hub = hub;
        _config = config;
        _logger = logger;
        _router = router;
    }

    [HttpPost("{channel}")]
    public async Task<IActionResult> HandleWebhook(string channel, [FromQuery] string? secret)
    {
        if (channel is not ("telegram" or "eitaa" or "bale"))
            return NotFound();

        var expectedSecret = _config["BaleBot:WebhookSecret"] ?? "";
        if (!string.IsNullOrEmpty(expectedSecret) && secret != expectedSecret)
        {
            _logger.LogWarning("{Channel} webhook: invalid secret", channel);
            return Unauthorized();
        }

        string rawBody;
        using (var reader = new StreamReader(Request.Body))
        {
            rawBody = await reader.ReadToEndAsync();
        }

        if (string.IsNullOrWhiteSpace(rawBody))
            return Ok();

        var channelService = _router.GetByKey(channel);
        if (channelService == null)
            return Ok();

        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings == null)
            return Ok();

        if (!channelService.IsConfigured(settings))
        {
            _logger.LogWarning("{Channel} webhook: bot not configured", channel);
            return Ok();
        }

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
            _logger.LogError(ex, "{Channel} webhook: failed to parse JSON", channel);
            return Ok();
        }

        if (payload?.Message == null)
            return Ok();

        // Optional group restriction (if configured, only accept group messages)
        var groupId = GetGroupId(channelService, settings);
        var actualChatId = payload.Message.Chat?.Id.ToString();
        if (!string.IsNullOrEmpty(groupId) && actualChatId != groupId)
        {
            _logger.LogWarning("{Channel} webhook: chat ID mismatch (expected={Expected}, received={Received})", channel, groupId, actualChatId);
            return Ok();
        }

        var replyTo = payload.Message.ReplyToMessage;
        var replyText = replyTo?.Text ?? replyTo?.Caption;
        var directText = payload.Message.Text;
        var sessionId = MessengerHelper.ExtractSessionId(replyText) ?? MessengerHelper.ExtractSessionId(directText);

        if (sessionId == null)
        {
            _logger.LogWarning("{Channel} webhook: could not extract sessionId", channel);
            return Ok();
        }

        var session = await _db.ChatSessions.FindAsync(sessionId.Value);
        if (session == null || session.Status == ChatSessionStatus.Closed)
            return Ok();

        var text = payload.Message.Text ?? payload.Message.Caption ?? "";
        var messageType = ChatMessageType.Text;
        string? mediaUrl = null;

        if (payload.Message.Photo is { Count: > 0 })
        {
            messageType = ChatMessageType.Image;
            var fileId = payload.Message.Photo[^1].FileId;
            mediaUrl = await channelService.DownloadFileAsync(fileId);
        }
        else if (payload.Message.Voice != null)
        {
            messageType = ChatMessageType.Voice;
            mediaUrl = await channelService.DownloadFileAsync(payload.Message.Voice.FileId);
        }

        var msg = new ChatMessage
        {
            SessionId = sessionId.Value,
            SenderType = ChatSenderType.Operator,
            SenderId = $"اپراتور ({channelService.DisplayName})",
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

        _logger.LogInformation("{Channel} webhook: broadcasting message {MsgId} ({Type}) to session_{SessionId}",
            channel, msg.Id, messageType, sessionId);
        await _hub.Clients.Group($"session_{sessionId}").SendAsync("NewMessage", dto);
        return Ok();
    }

    private static string? GetGroupId(IChatMessengerChannel ch, Models.SiteSettings s)
    {
        return ch.Key switch
        {
            "telegram" => s.TelegramGroupId?.Trim(),
            "eitaa" => s.EitaaGroupId?.Trim(),
            _ => s.BaleBotGroupId?.Trim()
        };
    }
}