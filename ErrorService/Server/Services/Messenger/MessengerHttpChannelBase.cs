using ErrorService.Server.Models;
using System.Text;
using System.Text.Json;

namespace ErrorService.Server.Services.Messenger;

public abstract class MessengerHttpChannelBase : IChatMessengerChannel
{
    private readonly IHttpClientFactory _httpClientFactory;
    protected readonly ILogger Logger;

    protected MessengerHttpChannelBase(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        Logger = logger;
    }

    public abstract string Key { get; }

    public abstract string DisplayName { get; }

    protected abstract string ApiBaseUrl { get; }

    protected abstract string FileBaseUrl { get; }

    protected abstract string? GetToken(SiteSettings settings);

    protected abstract string? GetGroupId(SiteSettings settings);

    public abstract bool IsEnabled(SiteSettings settings);

    public bool IsConfigured(SiteSettings settings)
        => !string.IsNullOrWhiteSpace(GetToken(settings)) && !string.IsNullOrWhiteSpace(GetGroupId(settings));

    private string BotUrl(string token, string method) => $"{ApiBaseUrl}/bot{token}/{method}";

    public async Task SendTextToGroupAsync(int sessionId, string text, string? userName)
    {
        var settings = await LoadSettingsAsync();
        var token = GetToken(settings);
        var groupId = GetGroupId(settings);
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(groupId)) return;

        var message = $"{MessengerHelper.SessionPrefix}{sessionId}]\n👤 {userName ?? "کاربر"}:\n{text}";
        await SendApiAsync(token, "sendMessage", new { chat_id = groupId, text = message });
    }

    public async Task SendPhotoToGroupAsync(int sessionId, string photoUrl, string? caption, string? userName)
    {
        var settings = await LoadSettingsAsync();
        var token = GetToken(settings);
        var groupId = GetGroupId(settings);
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(groupId)) return;

        var text = $"{MessengerHelper.SessionPrefix}{sessionId}]\n👤 {userName ?? "کاربر"}:\n{caption ?? ""}";
        await SendApiAsync(token, "sendPhoto", new { chat_id = groupId, photo = photoUrl, caption = text });
    }

    public async Task SendVoiceToGroupAsync(int sessionId, string voiceUrl, string? userName)
    {
        var settings = await LoadSettingsAsync();
        var token = GetToken(settings);
        var groupId = GetGroupId(settings);
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(groupId)) return;

        var text = $"{MessengerHelper.SessionPrefix}{sessionId}]\n🎤 {userName ?? "کاربر"}";
        await SendApiAsync(token, "sendVoice", new { chat_id = groupId, voice = voiceUrl, caption = text });
    }

    public async Task SendSystemMessageToGroupAsync(int sessionId, string text)
    {
        var settings = await LoadSettingsAsync();
        var token = GetToken(settings);
        var groupId = GetGroupId(settings);
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(groupId)) return;

        var message = $"{MessengerHelper.SessionPrefix}{sessionId}]\nℹ️ {text}";
        await SendApiAsync(token, "sendMessage", new { chat_id = groupId, text = message });
    }

    public async Task SendTextToChatAsync(long chatId, string text)
    {
        var settings = await LoadSettingsAsync();
        var token = GetToken(settings);
        if (string.IsNullOrEmpty(token)) return;

        await SendApiAsync(token, "sendMessage", new { chat_id = chatId, text });
    }

    public async Task<string?> DownloadFileAsync(string fileId)
    {
        var settings = await LoadSettingsAsync();
        var token = GetToken(settings);
        if (string.IsNullOrEmpty(token)) return null;

        try
        {
            var client = _httpClientFactory.CreateClient();
            var getResp = await client.GetAsync(BotUrl(token, $"getFile?file_id={fileId}"));
            if (!getResp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean()) return null;
            if (!doc.RootElement.GetProperty("result").TryGetProperty("file_path", out var fp)) return null;
            var filePath = fp.GetString();
            if (string.IsNullOrEmpty(filePath)) return null;

            var fileResp = await client.GetAsync($"{FileBaseUrl}/file/bot{token}/{filePath}");
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
            Logger.LogError(ex, "{Channel}: failed to download file {FileId}", Key, fileId);
            return null;
        }
    }

    public async Task<bool> TestConnectionAsync(string token, string groupId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var getMe = await client.GetAsync(BotUrl(token, "getMe"));
            if (!getMe.IsSuccessStatusCode) return false;

            var testPayload = JsonSerializer.Serialize(new
            {
                chat_id = groupId,
                text = "🔧 تست اتصال از سایت"
            });
            var content = new StringContent(testPayload, Encoding.UTF8, "application/json");
            var sendResp = await client.PostAsync(BotUrl(token, "sendMessage"), content);
            return sendResp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<SiteSettings> LoadSettingsAsync()
    {
        // Overridden in derived classes that hold a DbContext; default returns empty settings.
        return await LoadSettingsCoreAsync();
    }

    protected abstract Task<SiteSettings> LoadSettingsCoreAsync();

    private async Task SendApiAsync(string token, string method, object payload)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(BotUrl(token, method), content);
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                Logger.LogInformation("{Channel}: {Method} success: {Body}", Key, method, body);
            }
            else
            {
                Logger.LogWarning("{Channel}: {Method} failed ({Status}): {Body}", Key, method, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Channel}: exception sending {Method}", Key, method);
        }
    }
}