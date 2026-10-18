using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

public sealed class StockAlertNotifier
{
    private const string EventType = "product.restocked";

    private readonly ErrorServiceDbContext _db;
    private readonly NotificationEventService _events;
    private readonly ILogger<StockAlertNotifier> _logger;

    public StockAlertNotifier(ErrorServiceDbContext db, NotificationEventService events, ILogger<StockAlertNotifier> logger)
    {
        _db = db;
        _events = events;
        _logger = logger;
    }

    /// <summary>
    /// If any of the given products just transitioned from out-of-stock to
    /// in-stock, notify every user with an active StockAlert for them and
    /// consume those alerts (one-shot). Safe to call with an empty list or
    /// when nothing restocked. Never throws into the caller's business flow.
    /// </summary>
    public async Task NotifyRestockedAsync(IReadOnlyCollection<int> productIds)
    {
        if (productIds.Count == 0)
            return;

        try
        {
            var rule = await _db.NotificationRules
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.EventType == EventType);

            // Rule explicitly disabled by admin: do not consume subscriptions.
            if (rule is { IsEnabled: false })
                return;

            var restocked = await _db.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id) && p.IsAvailable && p.StockQuantity > 0)
                .Select(p => new { p.Id, p.Name, p.Slug, p.StockQuantity })
                .ToListAsync();

            if (restocked.Count == 0)
                return;

            var restockedIds = restocked.Select(p => p.Id).ToList();
            var alerts = await _db.StockAlerts
                .Where(a => a.Status == StockAlertStatus.Active && restockedIds.Contains(a.ProductId))
                .ToListAsync();

            if (alerts.Count == 0)
                return;

            var now = DateTimeOffset.UtcNow;
            foreach (var product in restocked)
            {
                foreach (var alert in alerts.Where(a => a.ProductId == product.Id))
                {
                    await _events.NotifyAsync(
                        eventType: EventType,
                        values: new Dictionary<string, string?>
                        {
                            ["ProductName"] = product.Name,
                            ["ProductId"] = product.Id.ToString(),
                            ["StockQuantity"] = product.StockQuantity.ToString()
                        },
                        idempotencyKey: $"product.restocked:alert:{alert.Id}",
                        fallbackSeverity: NotificationSeverity.Success,
                        fallbackTitle: "کالای مورد نظرتان موجود شد",
                        fallbackBody: $"کالای «{product.Name}» موجود شد؛ همین حالا سفارش دهید.",
                        fallbackUrl: $"/products/{product.Id}",
                        appUserId: alert.AppUserId);

                    alert.Status = StockAlertStatus.Notified;
                    alert.NotifiedAt = now;
                }
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation(
                "Restock alerts notified: {AlertCount} alert(s) for products [{ProductIds}]",
                alerts.Count, string.Join(",", restockedIds));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restock alert notification failed for products [{ProductIds}]. Main operation continues.",
                string.Join(",", productIds));
        }
    }
}
