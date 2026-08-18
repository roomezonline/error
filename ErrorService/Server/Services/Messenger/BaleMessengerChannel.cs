using ErrorService.Server.Models;

namespace ErrorService.Server.Services.Messenger;

public sealed class BaleMessengerChannel : IChatMessengerChannel
{
    private readonly BaleBotService _baleBot;

    public BaleMessengerChannel(BaleBotService baleBot)
    {
        _baleBot = baleBot;
    }

    public string Key => "bale";

    public string DisplayName => "بله";

    public bool IsEnabled(SiteSettings settings) => settings.EnableOnlineChat && settings.EnableBaleChat;

    public bool IsConfigured(SiteSettings settings)
        => !string.IsNullOrWhiteSpace(settings.BaleBotToken) && !string.IsNullOrWhiteSpace(settings.BaleBotGroupId);

    public Task SendTextToGroupAsync(int sessionId, string text, string? userName)
        => _baleBot.SendTextToGroup(sessionId, text, userName);

    public Task SendPhotoToGroupAsync(int sessionId, string photoUrl, string? caption, string? userName)
        => _baleBot.SendPhotoToGroup(sessionId, photoUrl, caption, userName);

    public Task SendVoiceToGroupAsync(int sessionId, string voiceUrl, string? userName)
        => _baleBot.SendVoiceToGroup(sessionId, voiceUrl, userName);

    public Task SendOperatorTextToGroupAsync(int sessionId, string text, string? operatorName)
        => _baleBot.SendOperatorTextToGroup(sessionId, text, operatorName);

    public Task SendOperatorPhotoToGroupAsync(int sessionId, string photoUrl, string? caption, string? operatorName)
        => _baleBot.SendOperatorPhotoToGroup(sessionId, photoUrl, caption, operatorName);

    public Task SendOperatorVoiceToGroupAsync(int sessionId, string voiceUrl, string? operatorName)
        => _baleBot.SendOperatorVoiceToGroup(sessionId, voiceUrl, operatorName);

    public Task SendSystemMessageToGroupAsync(int sessionId, string text)
        => _baleBot.SendSystemMessageToGroup(sessionId, text);

    public Task SendTextToChatAsync(long chatId, string text)
        => _baleBot.SendTextToChat(chatId, text);

    public Task<string?> DownloadFileAsync(string fileId)
        => _baleBot.DownloadFileAsync(fileId);

    public Task<(bool success, string message)> SetWebhookAsync(string webhookUrl, string secret)
        => _baleBot.SetWebhookAsync(webhookUrl, secret);
}