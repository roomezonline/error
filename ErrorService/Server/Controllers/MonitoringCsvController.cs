using System.Globalization;
using System.Text;
using ErrorService.Server.Data;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/monitoring/records/csv")]
[Authorize(Policy = "perm:admin.monitoring.view")]
public sealed class MonitoringCsvController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public MonitoringCsvController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    private bool IsSuperAdmin() => User.IsInRole("super_admin");

    [HttpGet("{monitoringId:int}")]
    public async Task<IActionResult> ExportCsv(int monitoringId)
    {
        int? workshopId = null;
        int? deviceId = null;
        DateTime? startedAtRaw = null;

        var connection = await _db.MonitoringReceiptConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == monitoringId);

        if (connection != null)
        {
            workshopId = connection.WorkshopId;
            deviceId = connection.MonitoringDeviceId;
            startedAtRaw = connection.CreatedAt.DateTime;

            if (!IsSuperAdmin())
            {
                var targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
                if (workshopId != targetWorkshopId)
                    return Forbid();
            }
        }
        else
        {
            var archive = await _db.MonitoringDataArchives
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.MonitoringReceiptConnectionId == monitoringId);

            if (archive == null) return NotFound("مانیتورینگ یافت نشد");

            workshopId = archive.WorkshopId;
            startedAtRaw = archive.StartedAt;

            if (!IsSuperAdmin())
            {
                var targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
                if (workshopId != targetWorkshopId)
                    return Forbid();
            }
        }

        // Fetch connection metadata for CSV headers
        string? customerName = null;
        string? customerMobile = null;
        string? deviceTitle = null;
        string? deviceNumber = null;

        if (connection != null)
        {
            var fullConnection = await _db.MonitoringReceiptConnections
                .AsNoTracking()
                .Include(x => x.MonitoringDevice)
                .Include(x => x.CustomerReceipt).ThenInclude(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == monitoringId);

            if (fullConnection != null)
            {
                deviceTitle = fullConnection.MonitoringDevice?.Title;
                deviceNumber = fullConnection.MonitoringDevice?.DeviceNumber;
                var customer = fullConnection.CustomerReceipt?.Customer;
                if (customer != null)
                {
                    customerName = $"{customer.FirstName} {customer.LastName}".Trim();
                    customerMobile = customer.Mobile;
                }
            }
        }
        else
        {
            var archiveWithDetails = await _db.MonitoringDataArchives
                .AsNoTracking()
                .Include(a => a.MonitoringReceiptConnection)
                    .ThenInclude(x => x.MonitoringDevice)
                .Include(a => a.MonitoringReceiptConnection)
                    .ThenInclude(x => x.CustomerReceipt).ThenInclude(x => x.Customer)
                .FirstOrDefaultAsync(a => a.MonitoringReceiptConnectionId == monitoringId);

            if (archiveWithDetails?.MonitoringReceiptConnection != null)
            {
                var conn = archiveWithDetails.MonitoringReceiptConnection;
                deviceTitle = conn.MonitoringDevice?.Title;
                deviceNumber = conn.MonitoringDevice?.DeviceNumber;
                var customer = conn.CustomerReceipt?.Customer;
                if (customer != null)
                {
                    customerName = $"{customer.FirstName} {customer.LastName}".Trim();
                    customerMobile = customer.Mobile;
                }
            }
        }

        var records = await _db.MonitoringDataRecords
            .AsNoTracking()
            .Where(r => r.MonitoringId == monitoringId)
            .OrderBy(r => r.Timestamp)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine($"# ConnectionId: {monitoringId}");
        if (connection != null)
            sb.AppendLine($"# CustomerReceiptId: {connection.CustomerReceiptId}");
        else
        {
            var archiveReceiptId = await _db.MonitoringDataArchives
                .AsNoTracking()
                .Where(a => a.MonitoringReceiptConnectionId == monitoringId)
                .Select(a => a.CustomerReceiptId)
                .FirstOrDefaultAsync();
            if (archiveReceiptId.HasValue)
                sb.AppendLine($"# CustomerReceiptId: {archiveReceiptId.Value}");
        }
        if (customerName != null) sb.AppendLine($"# CustomerName: {customerName}");
        if (customerMobile != null) sb.AppendLine($"# CustomerMobile: {customerMobile}");
        if (deviceTitle != null) sb.AppendLine($"# MonitoringDeviceTitle: {deviceTitle}");
        if (deviceNumber != null) sb.AppendLine($"# MonitoringDeviceNumber: {deviceNumber}");
        sb.AppendLine("Timestamp,TimestampFa,TemperatureRef,TemperatureFreez,MotorState,Bargh,Element1,Element2,Fdc1,Fac1,Jaryan,Power,Kw,SumKw,State,Note");

        foreach (var r in records)
        {
            var ts = r.Timestamp.ToString("O");
            var t1 = r.TemperatureRef?.ToString(CultureInfo.InvariantCulture) ?? "";
            var t2 = r.TemperatureFreez?.ToString(CultureInfo.InvariantCulture) ?? "";
            var mot = r.MotorState ? "True" : "False";
            var brg = r.Bargh ? "True" : "False";
            var e1 = r.Element1 ? "True" : "False";
            var e2 = r.Element2 ? "True" : "False";
            var dc1 = r.Fdc1 ? "True" : "False";
            var ac1 = r.Fac1 ? "True" : "False";
            var j = r.Jaryan?.ToString(CultureInfo.InvariantCulture) ?? "";
            var p = r.Power?.ToString(CultureInfo.InvariantCulture) ?? "";
            var kw = r.Kw?.ToString(CultureInfo.InvariantCulture) ?? "";
            var skw = r.SumKw?.ToString(CultureInfo.InvariantCulture) ?? "";

            sb.AppendLine($"{ts},{r.TimestampFa},{t1},{t2},{mot},{brg},{e1},{e2},{dc1},{ac1},{j},{p},{kw},{skw},{r.State},{r.Note}");
        }

        string deviceCode;
        if (connection != null)
        {
            var device = await _db.MonitoringDevices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == connection.MonitoringDeviceId);
            deviceCode = device?.DeviceNumber ?? "unknown";
        }
        else
        {
            var archive = await _db.MonitoringDataArchives
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.MonitoringReceiptConnectionId == monitoringId);
            deviceCode = archive?.DeviceCode ?? "unknown";
            startedAtRaw ??= archive?.StartedAt;
        }

        var startedAt = (startedAtRaw ?? DateTime.UtcNow).ToString("yyyyMMdd");
        var endedAt = DateTime.UtcNow.ToString("yyyyMMdd");
        var fileName = $"monitoring_{deviceCode}_{startedAt}_{endedAt}.csv";

        // Mark archive as downloaded
        var archiveToUpdate = await _db.MonitoringDataArchives
            .FirstOrDefaultAsync(a => a.MonitoringReceiptConnectionId == monitoringId);
        if (archiveToUpdate != null)
        {
            archiveToUpdate.Downloaded = true;
            archiveToUpdate.DownloadedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }
}
