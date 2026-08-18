using ErrorService.Server.Models;

namespace ErrorService.Server.Services.Messenger;

public interface IChatMessengerChannel
{
    string Key { get; }

    string DisplayName { get; }

    bool IsEnabled(SiteSettings settings);

    bool IsConfigured(SiteSettings settings);

    Task SendTextToGroupAsync(int sessionId, string text, string? userName);

    Task SendPhotoToGroupAsync(int sessionId, string photoUrl, string? caption, string? userName);

    Task SendVoiceToGroupAsync(int sessionId, string voiceUrl, string? userName);

    Task SendOperatorTextToGroupAsync(int sessionId, string text, string? operatorName);

    Task SendOperatorPhotoToGroupAsync(int sessionId, string photoUrl, string? caption, string? operatorName);

    Task SendOperatorVoiceToGroupAsync(int sessionId, string voiceUrl, string? operatorName);

    Task SendSystemMessageToGroupAsync(int sessionId, string text);

    Task SendTextToChatAsync(long chatId, string text);

    Task<string?> DownloadFileAsync(string fileId);

    Task<(bool success, string message)> SetWebhookAsync(string webhookUrl, string secret);
}

public static class MessengerHelper
{
    public const string SessionPrefix = "[Session:";

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
}