using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services.Messenger;

public sealed class MessengerRouter
{
    private readonly ErrorServiceDbContext _db;
    private readonly IEnumerable<IChatMessengerChannel> _channels;
    private readonly ILogger<MessengerRouter> _logger;

    public MessengerRouter(ErrorServiceDbContext db, IEnumerable<IChatMessengerChannel> channels, ILogger<MessengerRouter> logger)
    {
        _db = db;
        _channels = channels;
        _logger = logger;
    }

    public async Task<List<IChatMessengerChannel>> GetActiveChannelsAsync()
    {
        SiteSettings? settings;
        try
        {
            settings = await _db.SiteSettings.FirstOrDefaultAsync();
        }
        catch
        {
            settings = null;
        }
        if (settings == null) return new List<IChatMessengerChannel>();

        return _channels
            .Where(c => c.IsEnabled(settings) && c.IsConfigured(settings))
            .ToList();
    }

    public IChatMessengerChannel? GetByKey(string key)
        => _channels.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

    public async Task ForwardUserTextAsync(int sessionId, string text, string? userName)
    {
        foreach (var ch in await GetActiveChannelsAsync())
        {
            try { await ch.SendTextToGroupAsync(sessionId, text, userName); }
            catch (Exception ex) { _logger.LogWarning(ex, "Channel {Key}: forward text failed", ch.Key); }
        }
    }

    public async Task ForwardUserPhotoAsync(int sessionId, string photoUrl, string? caption, string? userName)
    {
        foreach (var ch in await GetActiveChannelsAsync())
        {
            try { await ch.SendPhotoToGroupAsync(sessionId, photoUrl, caption, userName); }
            catch (Exception ex) { _logger.LogWarning(ex, "Channel {Key}: forward photo failed", ch.Key); }
        }
    }

    public async Task ForwardUserVoiceAsync(int sessionId, string voiceUrl, string? userName)
    {
        foreach (var ch in await GetActiveChannelsAsync())
        {
            try { await ch.SendVoiceToGroupAsync(sessionId, voiceUrl, userName); }
            catch (Exception ex) { _logger.LogWarning(ex, "Channel {Key}: forward voice failed", ch.Key); }
        }
    }

    public async Task SendSystemToAllAsync(int sessionId, string text)
    {
        foreach (var ch in await GetActiveChannelsAsync())
        {
            try { await ch.SendSystemMessageToGroupAsync(sessionId, text); }
            catch (Exception ex) { _logger.LogWarning(ex, "Channel {Key}: forward system failed", ch.Key); }
        }
    }
}