using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

public sealed class PriceDropNotifier
{
    private const string EventType = "product.price_dropped";

    private readonly ErrorServiceDbContext _db;
    private readonly NotificationEventService _events;
    private readonly ILogger<PriceDropNotifier> _logger;

    public PriceDropNotifier(ErrorServiceDbContext db, NotificationEventService events, ILogger<PriceDropNotifier> logger)
    {
        _db = db;
        _events = events;
        _logger = logger;
    }

    /// <summary>
    /// وقتی قیمت مؤثر محصول کاهش می‌یابد، به همهٔ مشتریانی که برای آن یادآوری
    /// فعال دارند اطلاع می‌دهد و یادآوری را یک‌بارمصرف می‌کند.
    /// هرگز خطا را به جریان اصلی ذخیرهٔ محصول پرتاب نمی‌کند.
    /// </summary>
    public async Task NotifyDroppedAsync(int productId, decimal oldPrice, decimal newPrice)
    {
        if (newPrice <= 0 || oldPrice <= 0 || newPrice >= oldPrice)
            return;

        try
        {
            var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            if (settings is { EnablePriceDropAlerts: false })
                return;

            var minPercent = Math.Max(0, settings?.PriceDropMinPercent ?? 0);
            if (minPercent > 0)
            {
                var percent = (oldPrice - newPrice) / oldPrice * 100m;
                if (percent < minPercent)
                    return;
            }

            var product = await _db.Products
                .AsNoTracking()
                .Where(p => p.Id == productId && !p.IsDraft)
                .Select(p => new { p.Id, p.Name, p.Slug })
                .FirstOrDefaultAsync();

            if (product == null)
                return;

            var alerts = await _db.PriceDropAlerts
                .Where(a => a.ProductId == productId
                    && a.Status == PriceDropAlertStatus.Active
                    && a.SubscribedPrice > newPrice)
                .ToListAsync();

            if (alerts.Count == 0)
                return;

            var now = DateTimeOffset.UtcNow;
            foreach (var alert in alerts)
            {
                await _events.NotifyAsync(
                    eventType: EventType,
                    values: new Dictionary<string, string?>
                    {
                        ["ProductName"] = product.Name,
                        ["ProductId"] = product.Id.ToString(),
                        ["OldPrice"] = oldPrice.ToString("N0"),
                        ["NewPrice"] = newPrice.ToString("N0")
                    },
                    idempotencyKey: $"product.price_dropped:alert:{alert.Id}:{now.UtcTicks}",
                    fallbackSeverity: NotificationSeverity.Success,
                    fallbackTitle: "قیمت کالای مورد نظرتان کاهش یافت",
                    fallbackBody: $"قیمت «{product.Name}» از {oldPrice:N0} به {newPrice:N0} تومان کاهش یافت.",
                    fallbackUrl: $"/products/{product.Id}",
                    appUserId: alert.AppUserId);

                alert.Status = PriceDropAlertStatus.Notified;
                alert.NotifiedAt = now;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation(
                "Price drop alerts notified: {AlertCount} alert(s) for product {ProductId}",
                alerts.Count, productId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Price drop notification failed for product {ProductId}. Main operation continues.", productId);
        }
    }
}
