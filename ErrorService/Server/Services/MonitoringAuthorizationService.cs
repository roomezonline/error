using System.Security.Claims;
using ErrorService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

public sealed class MonitoringAuthorizationService
{
    private readonly ErrorServiceDbContext _db;

    public MonitoringAuthorizationService(ErrorServiceDbContext db)
    {
        _db = db;
    }

    private static int? TryGetWorkshopId(ClaimsPrincipal user)
    {
        var idStr = user.FindFirst("workshop_id")?.Value;
        if (int.TryParse(idStr, out var id)) return id;
        return null;
    }

    public async Task<bool> CanAccessMonitoring(ClaimsPrincipal user, int monitoringId)
    {
        if (user.IsInRole("super_admin"))
            return true;

        var workshopId = TryGetWorkshopId(user);
        if (workshopId == null) return false;

        var connection = await _db.MonitoringReceiptConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == monitoringId);

        return connection != null && connection.WorkshopId == workshopId.Value;
    }

    public async Task<bool> CanAccessDeviceCode(ClaimsPrincipal user, string deviceCode)
    {
        if (user.IsInRole("super_admin"))
            return true;

        var workshopId = TryGetWorkshopId(user);
        if (workshopId == null) return false;

        var device = await _db.MonitoringDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DeviceNumber == deviceCode);

        if (device == null)
            return false;

        var now = DateTimeOffset.UtcNow;
        return await _db.MonitoringDeviceAssignments
            .AsNoTracking()
            .AnyAsync(a => a.MonitoringDeviceId == device.Id
                        && a.WorkshopId == workshopId.Value
                        && a.StartAt <= now
                        && (a.EndAt == null || a.EndAt > now));
    }

    public async Task<bool> CanAccessChangeLog(ClaimsPrincipal user, int changeLogId)
    {
        if (user.IsInRole("super_admin"))
            return true;

        var workshopId = TryGetWorkshopId(user);
        if (workshopId == null) return false;

        var log = await _db.MonitoringDeviceChangeLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == changeLogId);

        return log != null && log.WorkshopId == workshopId.Value;
    }

    public async Task<bool> CanAccessAlert(ClaimsPrincipal user, int alertId)
    {
        if (user.IsInRole("super_admin"))
            return true;

        var workshopId = TryGetWorkshopId(user);
        if (workshopId == null) return false;

        var alert = await _db.MonitoringAlerts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == alertId);

        if (alert == null)
            return false;

        if (alert.MonitoringId.HasValue)
        {
            var connection = await _db.MonitoringReceiptConnections
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == alert.MonitoringId.Value);
            return connection != null && connection.WorkshopId == workshopId.Value;
        }

        return await CanAccessDeviceCode(user, alert.DeviceCode);
    }
}
