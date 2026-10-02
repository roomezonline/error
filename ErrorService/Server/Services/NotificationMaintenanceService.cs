using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

public sealed class NotificationMaintenanceService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    private readonly IServiceProvider _services;
    private readonly ILogger<NotificationMaintenanceService> _logger;

    public NotificationMaintenanceService(IServiceProvider services, ILogger<NotificationMaintenanceService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationMaintenanceService is running");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sweeping notification deliveries");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<NotificationEventService>();

        var threshold = DateTimeOffset.UtcNow - StaleAfter;
        var stale = await db.NotificationDeliveries
            .Where(x => x.Status == NotificationDeliveryStatus.Pending
                && x.LastAttemptAt != null
                && x.LastAttemptAt < threshold)
            .Take(500)
            .ToListAsync(ct);

        if (stale.Count == 0)
            return;

        foreach (var delivery in stale)
        {
            delivery.Status = NotificationDeliveryStatus.Failed;
            delivery.ErrorMessage = "مهلت تحویل به پایان رسید";
            delivery.LastAttemptAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);

        var failedCount = stale.Count;
        await notifier.NotifyAsync(
            eventType: "notification.delivery_failed",
            values: new Dictionary<string, string?>
            {
                ["Count"] = failedCount.ToString("N0")
            },
            idempotencyKey: $"notification.delivery_failed:{DateTimeOffset.UtcNow:yyyyMMddHH}",
            fallbackSeverity: NotificationSeverity.Critical,
            fallbackTitle: "برخی اعلان‌ها تحویل نشدند",
            fallbackBody: $"{failedCount} اعلان در صف تحویل ناموفق ماند و برای ارسال مجدد برنامه‌ریزی شد.",
            fallbackUrl: "/admin/notifications");
        await db.SaveChangesAsync(ct);

        _logger.LogWarning("{Count} stale notification deliveries marked as failed", failedCount);
    }
}
