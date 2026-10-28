using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Data.Seed;

public static class NotificationRuleSeeder
{
    /// <summary>
    /// قالب‌های قدیمی که اگر هنوز بدون تغییر در دیتابیس مانده باشند،
    /// با نسخهٔ فعلی جایگزین می‌شوند. ویرایش‌های دستی ادمین حفظ می‌شوند.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Title, string Body)> SupersededTemplates =
        new Dictionary<string, (string Title, string Body)>(StringComparer.OrdinalIgnoreCase)
        {
            ["monitoring.alert"] = ("هشدار پایش دستگاه", "{Message}"),
            ["order.receipt_uploaded"] = ("رسید پرداخت دریافت شد", "رسید پرداخت سفارش {OrderNumber} دریافت شد و در انتظار تأیید است."),
            ["order.status_changed"] = ("وضعیت سفارش به‌روزرسانی شد", "وضعیت سفارش {OrderNumber} به «{Status}» تغییر کرد."),
            ["order.auto_cancelled"] = ("سفارش لغو شد", "سفارش {OrderNumber} به دلیل عدم پرداخت در مهلت مقرر لغو شد."),
        };

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
                Channels = def.Channels,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        var upgraded = 0;
        if (SupersededTemplates.Count > 0)
        {
            var supersededTypes = SupersededTemplates.Keys.ToList();
            var rows = await db.NotificationRules
                .Where(x => supersededTypes.Contains(x.EventType))
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                if (!SupersededTemplates.TryGetValue(row.EventType, out var old))
                    continue;
                if (!string.Equals(row.TitleTemplate, old.Title, StringComparison.Ordinal) ||
                    !string.Equals(row.BodyTemplate, old.Body, StringComparison.Ordinal))
                    continue;

                var def = NotificationRuleDefaults.All
                    .FirstOrDefault(x => string.Equals(x.EventType, row.EventType, StringComparison.OrdinalIgnoreCase));
                if (def == null)
                    continue;
                if (string.Equals(row.TitleTemplate, def.Title, StringComparison.Ordinal) &&
                    string.Equals(row.BodyTemplate, def.Body, StringComparison.Ordinal))
                    continue;

                row.TitleTemplate = def.Title;
                row.BodyTemplate = def.Body;
                row.UpdatedAt = DateTimeOffset.UtcNow;
                upgraded++;
            }
        }

        if (missing.Count == 0 && upgraded == 0)
            return;

        await db.SaveChangesAsync(ct);

        if (missing.Count > 0)
            logger.LogInformation("NotificationRuleSeeder: Seeded {Count} notification rules.", missing.Count);
        if (upgraded > 0)
            logger.LogInformation("NotificationRuleSeeder: Refreshed {Count} superseded notification rule templates.", upgraded);
    }
}
