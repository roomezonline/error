using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

public sealed class MonitoringExpiryNotificationService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private const int LookAheadDays = 7;
    private const int LookBackDays = 7;

    private readonly IServiceProvider _services;
    private readonly ILogger<MonitoringExpiryNotificationService> _logger;

    public MonitoringExpiryNotificationService(IServiceProvider services, ILogger<MonitoringExpiryNotificationService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MonitoringExpiryNotificationService is running");

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
                _logger.LogError(ex, "Error while checking monitoring expirations");
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

        var now = DateTimeOffset.UtcNow;
        var expiring = await db.MonitoringDeviceAssignments
            .AsNoTracking()
            .Include(a => a.MonitoringDevice)
            .Where(a => a.EndAt != null && a.EndAt > now && a.EndAt <= now.AddDays(LookAheadDays))
            .ToListAsync(ct);

        var expired = await db.MonitoringDeviceAssignments
            .AsNoTracking()
            .Include(a => a.MonitoringDevice)
            .Where(a => a.EndAt != null && a.EndAt <= now && a.EndAt >= now.AddDays(-LookBackDays))
            .ToListAsync(ct);

        if (expiring.Count == 0 && expired.Count == 0)
            return;

        foreach (var assignment in expiring)
        {
            var endAt = assignment.EndAt!.Value;
            var deviceName = assignment.MonitoringDevice?.Title ?? $"دستگاه #{assignment.MonitoringDeviceId}";
            await notifier.NotifyAsync(
                eventType: "monitoring.expiring_soon",
                values: new Dictionary<string, string?>
                {
                    ["DeviceName"] = deviceName,
                    ["EndDate"] = PersianDateHelper.ToPersianDateTimeString(endAt, false)
                },
                idempotencyKey: $"monitoring.expiring:{assignment.Id}:{endAt:yyyyMMdd}",
                fallbackSeverity: NotificationSeverity.Warning,
                fallbackTitle: "انقضای سرویس پایش نزدیک است",
                fallbackBody: $"سرویس پایش دستگاه {deviceName} در تاریخ {PersianDateHelper.ToPersianDateTimeString(endAt, false)} به پایان می‌رسد.",
                fallbackUrl: "/admin/monitoring/renewal-requests",
                workshopId: assignment.WorkshopId);
        }

        foreach (var assignment in expired)
        {
            var endAt = assignment.EndAt!.Value;
            var deviceName = assignment.MonitoringDevice?.Title ?? $"دستگاه #{assignment.MonitoringDeviceId}";
            await notifier.NotifyAsync(
                eventType: "monitoring.expired",
                values: new Dictionary<string, string?>
                {
                    ["DeviceName"] = deviceName,
                    ["EndDate"] = PersianDateHelper.ToPersianDateTimeString(endAt, false)
                },
                idempotencyKey: $"monitoring.expired:{assignment.Id}:{endAt:yyyyMMdd}",
                fallbackSeverity: NotificationSeverity.Critical,
                fallbackTitle: "سرویس پایش دستگاه منقضی شد",
                fallbackBody: $"سرویس پایش دستگاه {deviceName} منقضی شده است؛ برای ادامه پایش اقدام به تمدید کنید.",
                fallbackUrl: "/admin/monitoring/renewal-requests",
                workshopId: assignment.WorkshopId);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Monitoring expiry check: {Expiring} expiring, {Expired} expired", expiring.Count, expired.Count);
    }
}
