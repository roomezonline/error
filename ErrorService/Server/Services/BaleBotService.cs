using ErrorService.Server.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace ErrorService.Server.Services;

public sealed class BaleBotService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ErrorServiceDbContext _db;
    private readonly ILogger<BaleBotService> _logger;
    private const string SessionPrefix = "[Session:";

    public BaleBotService(IHttpClientFactory httpClientFactory, ErrorServiceDbContext db, ILogger<BaleBotService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _logger = logger;
    }

    public async Task<bool> IsConfiguredAsync()
    {
        try
        {
            return await _db.SiteSettings.AnyAsync(s =>
                !string.IsNullOrEmpty(s.BaleBotToken) &&
                !string.IsNullOrEmpty(s.BaleBotGroupId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BaleBot: IsConfiguredAsync query failed (migration may be missing)");
            return false;
        }
    }

    private async Task<(string token, string groupId)> GetSettingsAsync()
    {
        try
        {
            var settings = await _db.SiteSettings.FirstOrDefaultAsync();
            var token = settings?.BaleBotToken?.Trim() ?? "";
            var groupId = settings?.BaleBotGroupId?.Trim() ?? "";
            if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(groupId))
                _logger.LogInformation("BaleBot: loaded settings (token={TokenLen} chars, groupId={GroupId})", token.Length, groupId);
            else
                _logger.LogWarning("BaleBot: settings not found or incomplete in DB");
            return (token, groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BaleBot: failed to load settings from DB");
            return ("", "");
        }
    }

    public async Task SendTextToGroup(int sessionId, string text, string? userName)
    {
        var (token, groupId) = await GetSettingsAsync();
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(groupId)) return;

        var message = $"{SessionPrefix}{sessionId}]\n👤 {userName ?? "کاربر"}:\n{text}";
        await SendApiAsync(token, "sendMessage", new { chat_id = groupId, text = message });
    }

    public async Task SendPhotoToGroup(int sessionId, string photoUrl, string? caption, string? userName)
    {
        var (token, groupId) = await GetSettingsAsync();
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(groupId)) return;

        var text = $"{SessionPrefix}{sessionId}]\n👤 {userName ?? "کاربر"}:\n{caption ?? ""}";
        await SendApiAsync(token, "sendPhoto", new { chat_id = groupId, photo = photoUrl, caption = text });
    }

    public async Task SendVoiceToGroup(int sessionId, string voiceUrl, string? userName)
    {
        var (token, groupId) = await GetSettingsAsync();
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(groupId)) return;

        var text = $"{SessionPrefix}{sessionId}]\n🎤 {userName ?? "کاربر"}";
        await SendApiAsync(token, "sendVoice", new { chat_id = groupId, voice = voiceUrl, caption = text });
    }

    public async Task SendSystemMessageToGroup(int sessionId, string text)
    {
        var (token, groupId) = await GetSettingsAsync();
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(groupId)) return;

        var message = $"{SessionPrefix}{sessionId}]\nℹ️ {text}";
        await SendApiAsync(token, "sendMessage", new { chat_id = groupId, text = message });
    }

    public async Task<(bool success, string message)> SetWebhookAsync(string webhookUrl, string secret)
    {
        var (token, _) = await GetSettingsAsync();
        if (string.IsNullOrEmpty(token))
            return (false, "توکن بله تنظیم نشده است.");

        var url = $"{webhookUrl.TrimEnd('/')}/api/chat/bale-webhook";
        if (!string.IsNullOrEmpty(secret))
            url += $"?secret={secret}";

        try
        {
            var client = _httpClientFactory.CreateClient("BaleBot");
            var baseUrl = $"https://tapi.bale.ai/bot{token}/";
            var payload = new
            {
                url,
                allowed_updates = new[] { "message", "edited_message" }
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(baseUrl + "setWebhook", content);
            var body = await response.Content.ReadAsStringAsync();

            // Parse response to check ok field
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("ok", out var ok) && ok.GetBoolean())
                    return (true, $"✅ وب‌هوک با موفقیت ثبت شد.\nآدرس: {url}");
                var desc = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                return (false, $"⚠️ خطا در ثبت وب‌هوک: {desc}");
            }
            catch
            {
                if (response.IsSuccessStatusCode)
                    return (true, $"✅ وب‌هوک با موفقیت ثبت شد.\nآدرس: {url}");
                else
                    return (false, $"⚠️ خطا در ثبت وب‌هوک: {body}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"خطا: {ex.Message}");
        }
    }

    public async Task<(bool hasWebhook, string webhookUrl)> GetWebhookInfoAsync()
    {
        var (token, _) = await GetSettingsAsync();
        if (string.IsNullOrEmpty(token))
            return (false, "");

        try
        {
            var client = _httpClientFactory.CreateClient("BaleBot");
            var baseUrl = $"https://tapi.bale.ai/bot{token}/";
            var response = await client.GetAsync(baseUrl + "getWebhookInfo");
            var body = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var ok) && ok.GetBoolean())
            {
                var result = root.GetProperty("result");
                var url = result.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                return (!string.IsNullOrEmpty(url), url);
            }
            return (false, "");
        }
        catch
        {
            return (false, "");
        }
    }

    public static int? ExtractSessionId(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var start = text.IndexOf(SessionPrefix);
        if (start < 0) return null;
        start += SessionPrefix.Length;
        var end = text.IndexOf(']', start);
        if (end < 0) return null;
        if (int.TryParse(text[start..end], out var id)) return id;
        return null;
    }

    public async Task<string?> DownloadFileAsync(string fileId)
    {
        var (token, _) = await GetSettingsAsync();
        if (string.IsNullOrEmpty(token)) return null;

        try
        {
            var client = _httpClientFactory.CreateClient("BaleBot");
            var baseUrl = $"https://tapi.bale.ai/bot{token}/";
            var getResp = await client.GetAsync(baseUrl + $"getFile?file_id={fileId}");
            if (!getResp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean()) return null;
            var filePath = doc.RootElement.GetProperty("result").GetProperty("file_path").GetString();
            if (string.IsNullOrEmpty(filePath)) return null;

            var fileResp = await client.GetAsync($"https://tapi.bale.ai/file/bot{token}/{filePath}");
            if (!fileResp.IsSuccessStatusCode) return null;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".bin";
            var fileName = $"{Guid.NewGuid()}{ext}";
            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
            Directory.CreateDirectory(uploadsRoot);
            var fullPath = Path.Combine(uploadsRoot, fileName);
            await using var fs = new FileStream(fullPath, FileMode.Create);
            await fileResp.Content.CopyToAsync(fs);

            return $"/uploads/chat/{fileName}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BaleBot: failed to download file {FileId}", fileId);
            return null;
        }
    }

    private async Task SendApiAsync(string token, string method, object payload)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BaleBot");
            var baseUrl = $"https://tapi.bale.ai/bot{token}/";
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _logger.LogInformation("BaleBot: sending {Method} to {Url}", method, baseUrl + method);
            var response = await client.PostAsync(baseUrl + method, content);
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("BaleBot: {Method} success: {Body}", method, body);
            }
            else
            {
                _logger.LogWarning("BaleBot: {Method} failed ({Status}): {Body}", method, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BaleBot: exception sending {Method}", method);
        }
    }
}
