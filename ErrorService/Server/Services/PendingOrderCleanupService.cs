using ErrorService.Server.Data;
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
                var cutoff = DateTimeOffset.UtcNow.AddMinutes(-timeoutMinutes);

                var expiredOrders = await db.Orders
                    .Where(o => o.Status == OrderStatus.PendingPayment && o.CreatedAt < cutoff)
                    .ToListAsync(stoppingToken);

                _logger.LogInformation(
                    "Cleanup tick #{Seq}: cutoff={Cutoff} (timeout={Timeout}m), found {Count} expired order(s) [{Ids}]",
                    _tick, cutoff, timeoutMinutes, expiredOrders.Count,
                    string.Join(",", expiredOrders.Select(o => $"{o.Id}(sd={o.StockDeducted},created={o.CreatedAt:O})")));

                if (expiredOrders.Count > 0)
                {
                    _logger.LogInformation(
                        "Cleanup found {Count} expired order(s): {Ids}",
                        expiredOrders.Count,
                        string.Join(",", expiredOrders.Select(o => o.Id)));

                    // یک تراکنش برای کل چرخه: برگرداندن موجودی و آزادسازی کوپن با
                    // SQL مستقل انجام می‌شوند؛ بدون تراکنش، خطای بعدی باعث اجرای
                    // دوبارهٔ آن‌ها در چرخهٔ بعدی می‌شد (کم/زیاد شدن اشتباه موجودی).
                    await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);

                    var shouldRestoreFlags = new Dictionary<int, bool>();
                    var restoredRowsById = new Dictionary<int, int>();
                    var stockRestored = false;

                    foreach (var order in expiredOrders)
                    {
                        // موجودی‌ای که هنگام ثبت سفارش رزرو شده باید برگردد؛
                        // وگرنه موجودی محصول به‌صورت دائم کم می‌ماند.
                        var shouldRestore = order.StockDeducted;
                        var restoredRows = 0;
                        if (order.StockDeducted)
                        {
                            var items = await db.OrderItems
                                .Where(i => i.OrderId == order.Id)
                                .ToListAsync(stoppingToken);

                            _logger.LogInformation(
                                "Cleanup order {OrderId}: stockDeducted={SD}, items={Count} [{Products}]",
                                order.Id, order.StockDeducted, items.Count,
                                string.Join(",", items.Select(i => $"{i.ProductId}x{i.Quantity}")));

                            foreach (var item in items)
                            {
                                restoredRows += await db.Database.ExecuteSqlInterpolatedAsync(
                                    $"UPDATE Products SET StockQuantity = StockQuantity + {item.Quantity}, IsAvailable = 1 WHERE Id = {item.ProductId}",
                                    stoppingToken);
                            }

                            stockRestored = true;

                            order.StockDeducted = false;
                            _logger.LogInformation(
                                "Cleanup order {OrderId}: restoredRows={Rows}, stockDeductedAfterSet={SD}",
                                order.Id, restoredRows, order.StockDeducted);
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Cleanup order {OrderId}: stock already deducted=FALSE, skipping restore",
                                order.Id);
                        }

                        shouldRestoreFlags[order.Id] = shouldRestore;
                        restoredRowsById[order.Id] = restoredRows;

                        order.Status = OrderStatus.Cancelled;
                        order.UpdatedAt = DateTimeOffset.UtcNow;

                        if (order.CouponId.HasValue)
                        {
                            await db.Database.ExecuteSqlInterpolatedAsync(
                                $"UPDATE Coupons SET CurrentUsageCount = CASE WHEN CurrentUsageCount > 0 THEN CurrentUsageCount - 1 ELSE 0 END WHERE Id = {order.CouponId.Value}",
                                stoppingToken);
                        }

                        await notifier.NotifyOrderAutoCancelledAsync(order);
                    }

                    await db.SaveChangesAsync(stoppingToken);
                    await tx.CommitAsync(stoppingToken);

                    if (stockRestored)
                    {
                        try { await outputCache.EvictByTagAsync("products", CancellationToken.None); } catch { }
                    }

                    foreach (var order in expiredOrders)
                    {
                        db.Entry(order).State = EntityState.Detached;
                        var fresh = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == order.Id, stoppingToken);
                        _logger.LogInformation(
                            "Cleanup committed order {OrderId}: status={Status}, stockDeducted={SD} (wasRestore={WasRestore}, rows={Rows})",
                            order.Id, fresh?.Status, fresh?.StockDeducted, shouldRestoreFlags.TryGetValue(order.Id, out var wf) ? wf : false, restoredRowsById.TryGetValue(order.Id, out var rr) ? rr : 0);
                    }

                    _logger.LogInformation("Cancelled {Count} expired pending orders", expiredOrders.Count);
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
