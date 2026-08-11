using ErrorService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services.ChatAi;

public sealed class ChatAiCoordinator
{
    private readonly ErrorServiceDbContext _db;
    private readonly IEnumerable<IChatAiService> _services;
    private readonly ILogger<ChatAiCoordinator> _logger;

    public ChatAiCoordinator(ErrorServiceDbContext db, IEnumerable<IChatAiService> services, ILogger<ChatAiCoordinator> logger)
    {
        _db = db;
        _services = services;
        _logger = logger;
    }

    public bool IsEnabled => GetEnabledFlag();

    private bool GetEnabledFlag()
    {
        try
        {
            var s = _db.SiteSettings.AsNoTracking().FirstOrDefault();
            return s?.ChatEnableAiAssistant == true;
        }
        catch { return false; }
    }

    public async Task<string?> GetReplyAsync(string userMessage)
    {
        foreach (var svc in _services)
        {
            try
            {
                var reply = await svc.GetReplyAsync(userMessage, "");
                if (!string.IsNullOrWhiteSpace(reply))
                {
                    _logger.LogInformation("ChatAi: {Name} produced a reply", svc.Name);
                    return reply.Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ChatAi: service {Name} failed", svc.Name);
            }
        }
        return null;
    }
}