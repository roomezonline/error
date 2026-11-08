using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Data.Seed;

public static class ContentSeeder
{
    public static async Task SeedAsync(ErrorServiceDbContext db, ILogger logger, CancellationToken ct = default)
    {
        // تنظیمات تک‌ردیفی مرکز محتوا؛ فقط در صورت نبودِ رکورد ساخته می‌شود.
        if (!await db.ContentSettings.AnyAsync(ct))
        {
            db.ContentSettings.Add(new ContentSettings
            {
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Default content settings created.");
        }

        // رکوردهایی که قبل از چندنوعی‌شدن ساخته شده‌اند مقدار پیش‌فرض ۰ گرفته‌اند.
        // Enum.IsDefined در SQL ترجمه نمی‌شود، پس با مقایسه مستقیم اعضای مجاز انجام می‌شود.
        var invalidTypes = await db.News
            .Where(n => n.ContentType != ContentType.News
                     && n.ContentType != ContentType.Tip
                     && n.ContentType != ContentType.Announcement)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ContentType, ContentType.News), ct);
        if (invalidTypes > 0)
            logger.LogInformation("Normalized content type for {Count} legacy rows.", invalidTypes);

        // یکدست‌سازی برچسب‌ها: حذف فاصله‌های اضافه و تکراری‌ها.
        var rows = await db.News
            .Where(n => n.Tags != null && n.Tags != "")
            .ToListAsync(ct);

        var changed = 0;
        foreach (var row in rows)
        {
            var normalized = ContentTags.Normalize(row.Tags);
            if (string.Equals(normalized, row.Tags, StringComparison.Ordinal)) continue;

            row.Tags = normalized.Length == 0 ? null : normalized;
            changed++;
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Normalized tags for {Count} content rows.", changed);
        }
    }
}
