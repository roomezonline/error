using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/monitoring-devices")]
public sealed class MonitoringDevicesController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public MonitoringDevicesController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [Authorize(Policy = "perm:admin.monitoring.devices.manage")]
    [HttpGet]
    public async Task<ActionResult<List<MonitoringDeviceDto>>> GetDevices([FromQuery] bool onlyFree = false)
    {

        var now = DateTimeOffset.UtcNow;
        var query = _db.MonitoringDevices.AsNoTracking();

        if (onlyFree)
        {
            var assignedDeviceIds = await _db.MonitoringDeviceAssignments
                .Where(x => x.StartAt <= now && (x.EndAt == null || x.EndAt > now))
                .Select(x => x.MonitoringDeviceId)
                .ToListAsync();

            query = query.Where(x => !assignedDeviceIds.Contains(x.Id) && x.IsActive);
        }

        var list = await query
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Title)
            .Select(x => new MonitoringDeviceDto
            {
                Id = x.Id,
                Title = x.Title,
                DeviceNumber = x.DeviceNumber,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    [Authorize(Policy = "perm:admin.monitoring.view")]
    [HttpGet("workshop-devices")]
    public async Task<ActionResult<List<MonitoringDeviceDto>>> GetWorkshopDevices([FromQuery] int workshopId)
    {
        if (workshopId <= 0) return BadRequest("کارگاه معتبر نیست");

        var now = DateTimeOffset.UtcNow;

        var assignedDeviceIds = await _db.MonitoringDeviceAssignments
            .AsNoTracking()
            .Where(x => x.WorkshopId == workshopId && x.StartAt <= now && (x.EndAt == null || x.EndAt > now))
            .Select(x => x.MonitoringDeviceId)
            .ToListAsync();

        // Exclude devices already connected to any active receipt
        var busyDeviceIds = await _db.MonitoringReceiptConnections
            .AsNoTracking()
            .Where(x => x.EndedAt == null && assignedDeviceIds.Contains(x.MonitoringDeviceId))
            .Select(x => x.MonitoringDeviceId)
            .ToListAsync();

        var freeDeviceIds = assignedDeviceIds.Except(busyDeviceIds).ToList();

        var list = await _db.MonitoringDevices
            .AsNoTracking()
            .Where(d => freeDeviceIds.Contains(d.Id) && d.IsActive)
            .OrderBy(d => d.Title)
            .Select(d => new MonitoringDeviceDto
            {
                Id = d.Id,
                Title = d.Title,
                DeviceNumber = d.DeviceNumber,
                IsActive = d.IsActive,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    [Authorize(Policy = "perm:admin.monitoring.devices.manage")]
    [HttpPost]
    public async Task<ActionResult<MonitoringDeviceDto>> CreateDevice([FromBody] MonitoringDeviceUpsertRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest("عنوان الزامی است");

        if (string.IsNullOrWhiteSpace(req.DeviceNumber))
            return BadRequest("شماره دستگاه الزامی است");

        var trimmedDeviceNumber = req.DeviceNumber.Trim();
        if (await _db.MonitoringDevices.AnyAsync(x => x.DeviceNumber == trimmedDeviceNumber))
            return BadRequest("کد دستگاه تکراری است و قبلاً ثبت شده است");

        var entity = new MonitoringDevice
        {
            Title = req.Title.Trim(),
            DeviceNumber = trimmedDeviceNumber,
            IsActive = req.IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.MonitoringDevices.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new MonitoringDeviceDto
        {
            Id = entity.Id,
            Title = entity.Title,
            DeviceNumber = entity.DeviceNumber,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        });
    }

    [Authorize(Policy = "perm:admin.monitoring.devices.manage")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<MonitoringDeviceDto>> UpdateDevice(int id, [FromBody] MonitoringDeviceUpsertRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest("عنوان الزامی است");

        if (string.IsNullOrWhiteSpace(req.DeviceNumber))
            return BadRequest("شماره دستگاه الزامی است");

        var entity = await _db.MonitoringDevices.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        var trimmedDeviceNumber = req.DeviceNumber.Trim();
        if (await _db.MonitoringDevices.AnyAsync(x => x.DeviceNumber == trimmedDeviceNumber && x.Id != id))
            return BadRequest("کد دستگاه تکراری است و قبلاً ثبت شده است");

        entity.Title = req.Title.Trim();
        entity.DeviceNumber = trimmedDeviceNumber;
        entity.IsActive = req.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new MonitoringDeviceDto
        {
            Id = entity.Id,
            Title = entity.Title,
            DeviceNumber = entity.DeviceNumber,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        });
    }

    [Authorize(Policy = "perm:admin.monitoring.devices.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteDevice(int id)
    {
        var entity = await _db.MonitoringDevices.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        _db.MonitoringDevices.Remove(entity);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.monitoring.devices.manage")]
    [HttpPatch("{id:int}/toggle")]
    public async Task<IActionResult> ToggleDeviceStatus(int id)
    {
        var entity = await _db.MonitoringDevices.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        entity.IsActive = !entity.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(entity.IsActive);
    }

    [Authorize(Policy = "perm:admin.monitoring.view")]
    [HttpGet("assignments")]
    public async Task<ActionResult<List<MonitoringDeviceAssignmentDto>>> GetAssignments([FromQuery] int? deviceId = null, [FromQuery] int? workshopId = null)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        
        if (!isSuperAdmin)
        {
            var userWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (userWorkshopId <= 0)
                return Forbid();
            if (workshopId.HasValue && workshopId.Value > 0 && workshopId.Value != userWorkshopId)
                return Forbid();
            workshopId = userWorkshopId;
        }

        var now = DateTimeOffset.UtcNow;
        var q = _db.MonitoringDeviceAssignments
            .AsNoTracking()
            .Include(x => x.MonitoringDevice)
            .Include(x => x.Workshop)
            .AsQueryable();

        if (deviceId.HasValue && deviceId.Value > 0)
            q = q.Where(x => x.MonitoringDeviceId == deviceId.Value);

        if (workshopId.HasValue && workshopId.Value > 0)
            q = q.Where(x => x.WorkshopId == workshopId.Value);

        var rawList = await q
            .OrderByDescending(x => x.StartAt)
            .ToListAsync();

        var list = rawList.Select(x => new MonitoringDeviceAssignmentDto
        {
            Id = x.Id,
            MonitoringDeviceId = x.MonitoringDeviceId,
            MonitoringDeviceTitle = x.MonitoringDevice.Title,
            MonitoringDeviceNumber = x.MonitoringDevice.DeviceNumber,
            WorkshopId = x.WorkshopId,
            WorkshopName = x.Workshop.WorkshopName,
            StartAt = x.StartAt,
            EndAt = x.EndAt,
            IsActiveNow = x.StartAt <= now && (x.EndAt == null || x.EndAt > now),
            StartDateFa = PersianDateHelper.ToPersianDateTimeString(x.StartAt, false),
            EndDateFa = x.EndAt.HasValue ? PersianDateHelper.ToPersianDateTimeString(x.EndAt.Value, false) : "نامحدود"
        }).ToList();

        return Ok(list);
    }

    [Authorize(Policy = "perm:admin.monitoring.assignments.manage")]
    [HttpPost("assignments")]
    public async Task<ActionResult<MonitoringDeviceAssignmentDto>> CreateAssignment([FromBody] MonitoringDeviceAssignRequest req)
    {
        if (!TryParseFaDate(req.StartDateFa, out var start))
            return BadRequest("تاریخ شروع معتبر نیست");

        DateTimeOffset? end = null;
        if (!string.IsNullOrWhiteSpace(req.EndDateFa))
        {
            if (!TryParseFaDate(req.EndDateFa, out var endParsed))
                return BadRequest("تاریخ پایان معتبر نیست");
            end = endParsed;
        }

        if (end.HasValue && end.Value <= start)
            return BadRequest("تاریخ پایان باید بعد از تاریخ شروع باشد");

        var device = await _db.MonitoringDevices.FirstOrDefaultAsync(x => x.Id == req.MonitoringDeviceId);
        if (device == null) return BadRequest("دستگاه یافت نشد");

        var ws = await _db.Workshops.FirstOrDefaultAsync(x => x.Id == req.WorkshopId);
        if (ws == null) return BadRequest("کارگاه یافت نشد");

        var entity = new MonitoringDeviceAssignment
        {
            MonitoringDeviceId = req.MonitoringDeviceId,
            WorkshopId = req.WorkshopId,
            StartAt = start,
            EndAt = end,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.MonitoringDeviceAssignments.Add(entity);
        await _db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        return Ok(new MonitoringDeviceAssignmentDto
        {
            Id = entity.Id,
            MonitoringDeviceId = entity.MonitoringDeviceId,
            MonitoringDeviceTitle = device.Title,
            MonitoringDeviceNumber = device.DeviceNumber,
            WorkshopId = entity.WorkshopId,
            WorkshopName = ws.WorkshopName,
            StartAt = entity.StartAt,
            EndAt = entity.EndAt,
            IsActiveNow = entity.StartAt <= now && (entity.EndAt == null || entity.EndAt > now)
        });
    }

    [Authorize(Policy = "perm:admin.monitoring.assignments.manage")]
    [HttpPut("assignments/{id:int}")]
    public async Task<ActionResult<MonitoringDeviceAssignmentDto>> UpdateAssignment(int id, [FromBody] MonitoringDeviceAssignRequest req)
    {
        var entity = await _db.MonitoringDeviceAssignments.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound("تخصیص یافت نشد");

        if (!TryParseFaDate(req.StartDateFa, out var start))
            return BadRequest("تاریخ شروع معتبر نیست");

        DateTimeOffset? end = null;
        if (!string.IsNullOrWhiteSpace(req.EndDateFa))
        {
            if (!TryParseFaDate(req.EndDateFa, out var endParsed))
                return BadRequest("تاریخ پایان معتبر نیست");
            end = endParsed;
        }

        if (end.HasValue && end.Value <= start)
            return BadRequest("تاریخ پایان باید بعد از تاریخ شروع باشد");

        var device = await _db.MonitoringDevices.FirstOrDefaultAsync(x => x.Id == req.MonitoringDeviceId);
        if (device == null) return BadRequest("دستگاه یافت نشد");

        var ws = await _db.Workshops.FirstOrDefaultAsync(x => x.Id == req.WorkshopId);
        if (ws == null) return BadRequest("کارگاه یافت نشد");

        entity.MonitoringDeviceId = req.MonitoringDeviceId;
        entity.WorkshopId = req.WorkshopId;
        entity.StartAt = start;
        entity.EndAt = end;

        await _db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        return Ok(new MonitoringDeviceAssignmentDto
        {
            Id = entity.Id,
            MonitoringDeviceId = entity.MonitoringDeviceId,
            MonitoringDeviceTitle = device.Title,
            MonitoringDeviceNumber = device.DeviceNumber,
            WorkshopId = entity.WorkshopId,
            WorkshopName = ws.WorkshopName,
            StartAt = entity.StartAt,
            EndAt = entity.EndAt,
            IsActiveNow = entity.StartAt <= now && (entity.EndAt == null || entity.EndAt > now)
        });
    }

    [Authorize(Policy = "perm:admin.monitoring.assignments.manage")]
    [HttpDelete("assignments/{id:int}")]
    public async Task<IActionResult> DeleteAssignment(int id)
    {
        var entity = await _db.MonitoringDeviceAssignments.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        _db.MonitoringDeviceAssignments.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.monitoring.view")]
    [HttpGet("my-devices")]
    public async Task<ActionResult<List<MyDeviceDto>>> GetMyDevices([FromQuery] int? workshopId = null)
    {
        var isSuperAdmin = User.IsInRole("super_admin");

        if (!isSuperAdmin)
        {
            var userWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (userWorkshopId <= 0) return Forbid();
            if (workshopId.HasValue && workshopId.Value > 0 && workshopId.Value != userWorkshopId)
                return Forbid();
            workshopId = userWorkshopId;
        }

        var now = DateTimeOffset.UtcNow;
        var q = _db.MonitoringDeviceAssignments
            .AsNoTracking()
            .Include(x => x.MonitoringDevice)
            .Include(x => x.Workshop)
            .AsQueryable();

        if (workshopId.HasValue && workshopId.Value > 0)
            q = q.Where(x => x.WorkshopId == workshopId.Value);

        var list = await q
            .OrderByDescending(x => x.StartAt)
            .ToListAsync();

        var pendingAssignmentIds = await _db.MonitoringRenewalRequests
            .Where(r => r.Status == RenewalRequestStatus.Pending)
            .Select(r => r.AssignmentId)
            .ToListAsync();

        return Ok(list.Select(x =>
        {
            var isActive = x.StartAt <= now && (x.EndAt == null || x.EndAt > now);
            int? remainingDays = null;
            bool isExpiringSoon = false;
            if (x.EndAt.HasValue)
            {
                var diff = (x.EndAt.Value - now).TotalDays;
                if (diff > 0)
                {
                    remainingDays = (int)Math.Ceiling(diff);
                    isExpiringSoon = remainingDays <= 30;
                }
                else
                {
                    remainingDays = 0;
                }
            }

            return new MyDeviceDto
            {
                AssignmentId = x.Id,
                DeviceId = x.MonitoringDeviceId,
                DeviceTitle = x.MonitoringDevice.Title,
                DeviceNumber = x.MonitoringDevice.DeviceNumber,
                StartAt = x.StartAt,
                EndAt = x.EndAt,
                StartDateFa = PersianDateHelper.ToPersianDateTimeString(x.StartAt, false),
                EndDateFa = x.EndAt.HasValue ? PersianDateHelper.ToPersianDateTimeString(x.EndAt.Value, false) : "نامحدود",
                IsActiveNow = isActive,
                RemainingDays = remainingDays,
                IsExpiringSoon = isExpiringSoon,
                HasPendingRenewal = pendingAssignmentIds.Contains(x.Id)
            };
        }).ToList());
    }

    [Authorize(Policy = "perm:admin.monitoring.view")]
    [HttpGet("expiring-summary")]
    public async Task<ActionResult<ExpiringSummaryDto>> GetExpiringSummary([FromQuery] int? workshopId = null)
    {
        var isSuperAdmin = User.IsInRole("super_admin");

        if (!isSuperAdmin)
        {
            var userWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (userWorkshopId <= 0) return Forbid();
            workshopId = userWorkshopId;
        }

        var now = DateTimeOffset.UtcNow;
        var q = _db.MonitoringDeviceAssignments
            .AsNoTracking()
            .Include(x => x.MonitoringDevice)
            .AsQueryable();

        if (workshopId.HasValue && workshopId.Value > 0)
            q = q.Where(x => x.WorkshopId == workshopId.Value);

        var all = await q.ToListAsync();

        var expired = all.Where(x => x.EndAt.HasValue && x.EndAt.Value <= now).ToList();
        var expiringSoon = all.Where(x =>
            x.EndAt.HasValue &&
            x.EndAt.Value > now &&
            (x.EndAt.Value - now).TotalDays <= 30).ToList();

        return Ok(new ExpiringSummaryDto
        {
            ExpiredCount = expired.Count,
            ExpiringSoonCount = expiringSoon.Count,
            ExpiredDevices = expired.Select(x => DeviceToExpiringItem(x)).ToList(),
            ExpiringSoonDevices = expiringSoon.Select(x => DeviceToExpiringItem(x)).ToList()
        });
    }

    private static ExpiringDeviceItem DeviceToExpiringItem(MonitoringDeviceAssignment a)
    {
        var device = a.MonitoringDevice;
        return new ExpiringDeviceItem
        {
            AssignmentId = a.Id,
            DeviceTitle = device?.Title ?? "—",
            DeviceNumber = device?.DeviceNumber ?? "—",
            EndDateFa = a.EndAt.HasValue ? PersianDateHelper.ToPersianDateTimeString(a.EndAt.Value, false) : "—",
            RemainingDays = a.EndAt.HasValue ? (int)(a.EndAt.Value - DateTimeOffset.UtcNow).TotalDays : null
        };
    }

    private static bool TryParseFaDate(string? fa, out DateTimeOffset value)
    {
        value = default;
        var s = (fa ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(s)) return false;

        var parts = s.Split(new[] { '/', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var y)) return false;
        if (!int.TryParse(parts[1], out var m)) return false;
        if (!int.TryParse(parts[2], out var d)) return false;

        try
        {
            value = PersianDateHelper.FromPersianDate(y, m, d);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
