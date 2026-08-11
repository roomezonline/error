using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services.ChatAi;

public sealed class FaqChatService : IChatAiService
{
    private readonly ErrorServiceDbContext _db;
    private readonly ILogger<FaqChatService> _logger;

    public FaqChatService(ErrorServiceDbContext db, ILogger<FaqChatService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string Name => "پاسخ‌گوی خودکار (سوالات پرتکرار)";

    public async Task<string?> GetReplyAsync(string userMessage, string operatorName)
    {
        try
        {
            var entries = await _db.ChatFaqEntries
                .Where(f => f.IsEnabled)
                .ToListAsync();
            if (entries.Count == 0) return null;

            var normalized = PersianTextMatcher.Normalize(userMessage);
            if (normalized.Length < 2) return null;

            ChatFaqEntry? best = null;
            var bestScore = 0.0;
            var hardMatch = false;

            foreach (var e in entries)
            {
                var nq = PersianTextMatcher.Normalize(e.Question);
                if (nq.Length == 0) continue;

                // exact containment either way
                if (normalized.Contains(nq) || (nq.Contains(normalized) && normalized.Length >= 4))
                {
                    best = e;
                    hardMatch = true;
                    break;
                }

                // keyword match
                if (!string.IsNullOrWhiteSpace(e.Keywords))
                {
                    var matched = e.Keywords
                        .Split(new[] { ',', '،', ';', '؛' })
                        .Any(k => PersianTextMatcher.ContainsKeyword(normalized, k));
                    if (matched)
                    {
                        best = e;
                        hardMatch = true;
                        break;
                    }
                }

                // fuzzy similarity on question
                var score = normalized.Length >= 4 ? PersianTextMatcher.Coef(normalized, nq) : 0;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = e;
                }
            }

            if (best == null) return null;
            return hardMatch || bestScore >= 0.65 ? best.Answer : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FaqChatService: query failed");
            return null;
        }
    }
}