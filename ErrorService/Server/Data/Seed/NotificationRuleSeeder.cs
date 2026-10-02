using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Data.Seed;

public static class NotificationRuleSeeder
{
    public static async Task SeedAsync(ErrorServiceDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var existing = await db.NotificationRules
            .AsNoTracking()
            .Select(x => x.EventType)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var missing = NotificationRuleDefaults.All
            .Where(x => !existingSet.Contains(x.EventType))
            .ToList();

        if (missing.Count == 0)
            return;

        foreach (var def in missing)
        {
            db.NotificationRules.Add(new NotificationRule
            {
                EventType = def.EventType,
                Group = def.Group,
                Label = def.Label,
                IsEnabled = def.IsEnabled,
                Severity = def.Severity,
                TitleTemplate = def.Title,
                BodyTemplate = def.Body,
                ActionUrlTemplate = def.ActionUrl,
                BroadcastToAdmins = def.BroadcastToAdmins,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("NotificationRuleSeeder: Seeded {Count} notification rules.", missing.Count);
    }
}
