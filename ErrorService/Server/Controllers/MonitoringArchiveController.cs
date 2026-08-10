using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/monitoring/archives")]
[Authorize(Policy = "perm:admin.monitoring.view")]
public sealed class MonitoringArchiveController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public MonitoringArchiveController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    private bool IsSuperAdmin() => User.IsInRole("super_admin");

    [HttpGet]
    public async Task<ActionResult<List<MonitoringArchiveDto>>> GetAll([FromQuery] int? workshopId = null)
    {
        var isSuperAdmin = IsSuperAdmin();
        int? targetWorkshopId;

        if (!isSuperAdmin)
        {
            targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (targetWorkshopId == null) return Forbid();
        }
        else
        {
            targetWorkshopId = workshopId;
        }

        var q = _db.MonitoringDataArchives
            .AsNoTracking()
            .Include(a => a.Workshop)
            .Include(a => a.MonitoringReceiptConnection)
                .ThenInclude(c => c.CustomerReceipt)
                .ThenInclude(r => r.Customer)
            .Include(a => a.MonitoringReceiptConnection)
                .ThenInclude(c => c.CustomerReceipt)
                .ThenInclude(r => r.DeviceType)
            .AsQueryable();

        if (targetWorkshopId.HasValue && targetWorkshopId.Value > 0)
            q = q.Where(a => a.WorkshopId == targetWorkshopId.Value);

        var now = DateTime.UtcNow;

        var raw = await q
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var result = raw.Select(a =>
        {
            var age = (now - a.CreatedAt).Days;
            var receipt = a.MonitoringReceiptConnection?.CustomerReceipt;
            var customer = receipt?.Customer;
            return new MonitoringArchiveDto
            {
                Id = a.Id,
                WorkshopId = a.WorkshopId,
                WorkshopName = a.Workshop.WorkshopName,
                MonitoringReceiptConnectionId = a.MonitoringReceiptConnectionId,
                DeviceCode = a.DeviceCode,
                CustomerFullName = customer != null ? $"{customer.FirstName} {customer.LastName}" : "",
                CustomerMobile = customer?.Mobile ?? "",
                CustomerDeviceTitle = receipt?.DeviceType?.Name ?? "",
                StartedAtFa = PersianDateHelper.ToPersianDateTimeString(a.StartedAt, includeTime: true),
                EndedAtFa = PersianDateHelper.ToPersianDateTimeString(a.EndedAt, includeTime: true),
                RecordCount = a.RecordCount,
                EstimatedBytes = a.EstimatedBytes,
                EstimatedSizeText = FormatSize(a.EstimatedBytes),
                Downloaded = a.Downloaded,
                DownloadedAtFa = a.DownloadedAt.HasValue
                    ? PersianDateHelper.ToPersianDateTimeString(a.DownloadedAt.Value, includeTime: true)
                    : null,
                CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(a.CreatedAt, includeTime: true),
                AgeInDays = age
            };
        }).ToList();

        return Ok(result);
    }

    [HttpGet("count-pending")]
    public async Task<ActionResult<PendingArchiveCountDto>> GetPendingCount()
    {
        var isSuperAdmin = IsSuperAdmin();
        int? targetWorkshopId;

        if (!isSuperAdmin)
        {
            targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (targetWorkshopId == null) return Ok(new PendingArchiveCountDto { Count = 0 });
        }
        else
        {
            targetWorkshopId = null;
        }

        var q = _db.MonitoringDataArchives
            .AsNoTracking()
            .Where(a => !a.Downloaded)
            .AsQueryable();

        if (targetWorkshopId.HasValue)
            q = q.Where(a => a.WorkshopId == targetWorkshopId.Value);

        var count = await q.CountAsync();
        return Ok(new PendingArchiveCountDto { Count = count });
    }

    [Authorize(Policy = "perm:admin.monitoring.connections.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var archive = await _db.MonitoringDataArchives
            .FirstOrDefaultAsync(a => a.Id == id);

        if (archive == null) return NotFound();

        if (!IsSuperAdmin())
        {
            var targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (archive.WorkshopId != targetWorkshopId)
                return Forbid();
        }

        var records = await _db.MonitoringDataRecords
            .Where(r => r.MonitoringId == archive.MonitoringReceiptConnectionId)
            .ToListAsync();
        _db.MonitoringDataRecords.RemoveRange(records);

        _db.MonitoringDataArchives.Remove(archive);

        // If the associated connection has already ended, remove it too to free capacity
        var connection = await _db.MonitoringReceiptConnections
            .FirstOrDefaultAsync(c => c.Id == archive.MonitoringReceiptConnectionId && c.EndedAt != null);
        if (connection != null)
            _db.MonitoringReceiptConnections.Remove(connection);

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.monitoring.connections.manage")]
    [HttpPut("{id:int}/reset-download")]
    public async Task<IActionResult> ResetDownload(int id)
    {
        var archive = await _db.MonitoringDataArchives
            .FirstOrDefaultAsync(a => a.Id == id);

        if (archive == null) return NotFound();

        archive.Downloaded = false;
        archive.DownloadedAt = null;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
