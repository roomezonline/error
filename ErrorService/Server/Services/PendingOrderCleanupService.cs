using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ErrorService.Server.Services;

/// <summary>
/// مهلت لغو خودکار سفارش‌های «در انتظار پرداخت».
/// با کلید <c>Orders:PendingTimeoutMinutes</c> در appsettings قابل تغییر است؛
/// اگر تنظیم نشده باشد <see cref="DefaultMinutes"/> استفاده می‌شود.
/// </summary>
public static class PendingOrderTimeout
{
    public const int DefaultMinutes = 30;

    public static int GetMinutes(IConfiguration config)
    {
        var configured = config.GetValue<int?>("Orders:PendingTimeoutMinutes");
        return configured is > 0 ? configured.Value : DefaultMinutes;
    }
}

public sealed class PendingOrderCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PendingOrderCleanupService> _logger;
    private int _tick;

    public PendingOrderCleanupService(IServiceProvider services, ILogger<PendingOrderCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PendingOrderCleanupService is running");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

                _tick++;
                var tickAt = DateTimeOffset.UtcNow;
                _logger.LogInformation("Cleanup tick #{Seq} started at {Now}", _tick, tickAt);

                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
                var notifier = scope.ServiceProvider.GetRequiredService<NotificationEventService>();
                var outputCache = scope.ServiceProvider.GetRequiredService<IOutputCacheStore>();

                var timeoutMinutes = PendingOrderTimeout.GetMinutes(
                    scope.ServiceProvider.GetRequiredService<IConfiguration>());
                var now = DateTimeOffset.UtcNow;
                var cutoff = now.AddMinutes(-timeoutMinutes);

                // سفارش‌های ادمین مهلت اختصاصی (ExpiresAt) دارند؛ سفارش‌های قدیمی
                // بدون ExpiresAt با همان قاعدهٔ ۳۰ دقیقه از زمان ایجاد منقضی می‌شوند.
                // پیش‌فاکتور (Draft) هرگز به‌صورت خودکار لغو نمی‌شود.
                var expiredOrders = await db.Orders.AsNoTracking()
                    .Where(o => o.Status == OrderStatus.PendingPayment
                        && ((o.ExpiresAt != null && o.ExpiresAt < now)
                            || (o.ExpiresAt == null && o.CreatedAt < cutoff)))
                    .Select(o => new { o.Id, o.CouponId, o.StockDeducted })
                    .ToListAsync(stoppingToken);

                _logger.LogInformation(
                    "Cleanup tick #{Seq}: cutoff={Cutoff} (timeout={Timeout}m), found {Count} expired order(s) [{Ids}]",
                    _tick, cutoff, timeoutMinutes, expiredOrders.Count,
                    string.Join(",", expiredOrders.Select(o => $"{o.Id}(sd={o.StockDeducted})")));

                if (expiredOrders.Count > 0)
                {
                    _logger.LogInformation(
                        "Cleanup found {Count} expired order(s): {Ids}",
                        expiredOrders.Count,
                        string.Join(",", expiredOrders.Select(o => o.Id)));

                    // لغو و بازگرداندن موجودی باید در یک تراکنش باشد. علاوه بر آن،
                    // تغییر وضعیت با UPDATE شرطی انجام می‌شود تا اگر در همین لحظه
                    // پرداخت/ادمین سفارش را تغییر داده باشد، لغو خودکار آن را
                    // بازنویسی نکند (منبع اصلی باگ «پرداخت شد ولی لغو ماند»).
                    await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);

                    var cancelledCount = 0;
                    var stockRestored = false;

                    foreach (var candidate in expiredOrders)
                    {
                        // ابتدا سفارشی را claim می‌کنیم که موجودی رزرو کرده است.
                        var claimedWithStock = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Orders
SET Status = {(int)OrderStatus.Cancelled}, StockDeducted = 0, UpdatedAt = {now}
WHERE Id = {candidate.Id}
  AND Status = {(int)OrderStatus.PendingPayment}
  AND ((ExpiresAt IS NOT NULL AND ExpiresAt < {now}) OR (ExpiresAt IS NULL AND CreatedAt < {cutoff}))
  AND StockDeducted = 1", stoppingToken);

                        var claimed = claimedWithStock > 0;
                        if (!claimed)
                        {
                            // سفارش‌های قدیمی که موجودی‌شان قبلاً آزاد شده است.
                            claimed = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Orders
SET Status = {(int)OrderStatus.Cancelled}, UpdatedAt = {now}
WHERE Id = {candidate.Id}
  AND Status = {(int)OrderStatus.PendingPayment}
  AND ((ExpiresAt IS NOT NULL AND ExpiresAt < {now}) OR (ExpiresAt IS NULL AND CreatedAt < {cutoff}))", stoppingToken) > 0;
                        }

                        if (!claimed)
                        {
                            _logger.LogInformation(
                                "Cleanup skipped order {OrderId}: status changed concurrently (probably paid or edited)",
                                candidate.Id);
                            continue;
                        }

                        var restoredRows = 0;
                        if (claimedWithStock > 0)
                        {
                            var items = await db.OrderItems.AsNoTracking()
                                .Where(i => i.OrderId == candidate.Id)
                                .ToListAsync(stoppingToken);

                            _logger.LogInformation(
                                "Cleanup order {OrderId}: restoring {Count} item(s) [{Products}]",
                                candidate.Id, items.Count,
                                string.Join(",", items.Select(i => $"{i.ProductId}x{i.Quantity}")));

                            foreach (var item in items)
                            {
                                restoredRows += await db.Database.ExecuteSqlInterpolatedAsync(
                                    $"UPDATE Products SET StockQuantity = StockQuantity + {item.Quantity}, IsAvailable = 1 WHERE Id = {item.ProductId}",
                                    stoppingToken);
                            }

                            if (items.Count > 0)
                                stockRestored = true;
                        }

                        if (candidate.CouponId.HasValue)
                        {
                            await db.Database.ExecuteSqlInterpolatedAsync(
                                $"UPDATE Coupons SET CurrentUsageCount = CASE WHEN CurrentUsageCount > 0 THEN CurrentUsageCount - 1 ELSE 0 END WHERE Id = {candidate.CouponId.Value}",
                                stoppingToken);
                        }

                        var claimedOrder = await db.Orders.AsNoTracking()
                            .FirstOrDefaultAsync(o => o.Id == candidate.Id, stoppingToken);
                        if (claimedOrder != null)
                            await notifier.NotifyOrderAutoCancelledAsync(claimedOrder);

                        db.OrderEvents.Add(new OrderEvent
                        {
                            OrderId = candidate.Id,
                            EventType = "auto_cancelled",
                            FromStatus = OrderStatus.PendingPayment,
                            ToStatus = OrderStatus.Cancelled,
                            Message = "لغو خودکار به دلیل عدم پرداخت در مهلت مقرر",
                            CreatedBy = "system",
                            CreatedAt = now
                        });

                        cancelledCount++;
                        _logger.LogInformation(
                            "Cleanup cancelled order {OrderId}: restoredStock={Restored}, rows={Rows}",
                            candidate.Id, claimedWithStock > 0, restoredRows);
                    }

                    await db.SaveChangesAsync(stoppingToken);
                    await tx.CommitAsync(stoppingToken);

                    if (stockRestored)
                    {
                        try { await outputCache.EvictByTagAsync("products", CancellationToken.None); } catch { }
                    }

                    _logger.LogInformation(
                        "Cancelled {Cancelled} of {Found} expired pending order(s)",
                        cancelledCount, expiredOrders.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PendingOrderCleanupService");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
