using ErrorService.Server.Data;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

public sealed class PendingOrderCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PendingOrderCleanupService> _logger;

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

                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
                var notifier = scope.ServiceProvider.GetRequiredService<NotificationEventService>();

                var cutoff = DateTimeOffset.UtcNow.AddMinutes(-30);

                var expiredOrders = await db.Orders
                    .Where(o => o.Status == OrderStatus.PendingPayment && o.CreatedAt < cutoff)
                    .ToListAsync(stoppingToken);

                if (expiredOrders.Count > 0)
                {
                    foreach (var order in expiredOrders)
                    {
                        order.Status = OrderStatus.Cancelled;
                        order.UpdatedAt = DateTimeOffset.UtcNow;

                        if (order.CouponId.HasValue)
                        {
                            await db.Database.ExecuteSqlInterpolatedAsync(
                                $"UPDATE Coupons SET CurrentUsageCount = CASE WHEN CurrentUsageCount > 0 THEN CurrentUsageCount - 1 ELSE 0 END WHERE Id = {order.CouponId.Value}");
                        }

                        await notifier.NotifyOrderAutoCancelledAsync(order);
                    }

                    await db.SaveChangesAsync(stoppingToken);
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
