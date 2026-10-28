using System.Text;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

/// <summary>
/// پس از پایان اعتبار تخصیص دستگاه و مهلت ۲۴ ساعته، دستگاه را خاموش کرده و
/// اتصال مانیتورینگ باز را می‌بندد و آرشیو می‌کند. تخصیص و تاریخ پایان آن تغییر نمی‌کند.
/// </summary>
public sealed class MonitoringGraceEnforcementService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan GracePeriod = TimeSpan.FromHours(24);
    private static readonly TimeSpan CommandAckWindow = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _services;
    private readonly ILogger<MonitoringGraceEnforcementService> _logger;

    public MonitoringGraceEnforcementService(IServiceProvider services, ILogger<MonitoringGraceEnforcementService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MonitoringGraceEnforcementService is running");

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
                _logger.LogError(ex, "Error while enforcing monitoring credit expiry");
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
        var now = DateTimeOffset.UtcNow;

        var openConnections = await db.MonitoringReceiptConnections
            .Include(c => c.MonitoringDevice)
            .Where(c => c.EndedAt == null)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        if (openConnections.Count == 0)
            return;

        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitoring-data");

        foreach (var connection in openConnections)
        {
            ct.ThrowIfCancellationRequested();

            var device = connection.MonitoringDevice;
            if (device == null)
                continue;

            var assignment = await db.MonitoringDeviceAssignments
                .AsNoTracking()
                .Where(a => a.MonitoringDeviceId == device.Id
                         && a.WorkshopId == connection.WorkshopId
                         && a.StartAt <= now)
                .OrderByDescending(a => a.StartAt)
                .FirstOrDefaultAsync(ct);

            // بدون تخصیص، وضعیت روشن نیست؛ برای جلوگیری از بستن ناخواسته دست نمی‌زنیم.
            if (assignment == null)
                continue;

            if (assignment.EndAt == null || assignment.EndAt.Value > now)
            {
                if (connection.GracePeriodEndAt != null)
                {
                    connection.GracePeriodEndAt = null;
                    await db.SaveChangesAsync(ct);
                }

                continue;
            }

            var endAt = assignment.EndAt.Value;
            var graceEnd = endAt.Add(GracePeriod);
            if (connection.GracePeriodEndAt == null || connection.GracePeriodEndAt.Value < endAt)
            {
                connection.GracePeriodEndAt = graceEnd;
                await db.SaveChangesAsync(ct);
            }

            if (now < graceEnd)
                continue;

            if (!await DeviceReportedOffAsync(baseDir, device.DeviceNumber, ct))
            {
                await RequestDeviceOffAsync(baseDir, device.DeviceNumber, ct);
                continue;
            }

            await db.Entry(connection).ReloadAsync(ct);
            if (connection.EndedAt != null)
                continue;

            const string reason = "پایان خودکار: اعتبار دستگاه پس از مهلت ۲۴ ساعته تمدید نشد";
            await MonitoringConnectionLifecycle.CloseAndArchiveAsync(db, connection, device.DeviceNumber, reason);

            _logger.LogInformation(
                "Monitoring connection {ConnectionId} for device {DeviceCode} closed after credit expiry.",
                connection.Id, device.DeviceNumber);
        }
    }

    private static async Task<bool> DeviceReportedOffAsync(string baseDir, string deviceCode, CancellationToken ct)
    {
        var commandFile = Path.Combine(baseDir, deviceCode, "command", $"device_{deviceCode}.txt");
        if (!File.Exists(commandFile))
        {
            await RequestDeviceOffAsync(baseDir, deviceCode, ct);
            return false;
        }

        var content = (await File.ReadAllTextAsync(commandFile, ct)).Trim();
        if (content != "off")
        {
            await RequestDeviceOffAsync(baseDir, deviceCode, ct);
            return false;
        }

        var ackedOrTimedOut = DateTime.UtcNow - File.GetLastWriteTimeUtc(commandFile) >= CommandAckWindow;
        return ackedOrTimedOut || await ReadPowerStateAsync(baseDir, deviceCode, ct) == false;
    }

    private static async Task RequestDeviceOffAsync(string baseDir, string deviceCode, CancellationToken ct)
    {
        var dir = Path.Combine(baseDir, deviceCode, "command");
        Directory.CreateDirectory(dir);

        var file = Path.Combine(dir, $"device_{deviceCode}.txt");
        if (File.Exists(file) && (await File.ReadAllTextAsync(file, ct)).Trim() == "off")
            return;

        await File.WriteAllTextAsync(file, "off", Encoding.UTF8, ct);
    }

    /// <summary>true = روشن، false = خاموش/نامشخص.</summary>
    private static async Task<bool> ReadPowerStateAsync(string baseDir, string deviceCode, CancellationToken ct)
    {
        var file = Path.Combine(baseDir, deviceCode, "server", $"device_{deviceCode}.txt");
        if (!File.Exists(file))
            return false;

        foreach (var line in await File.ReadAllLinesAsync(file, ct))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("priz=", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = trimmed["priz=".Length..].Trim();
            return value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
