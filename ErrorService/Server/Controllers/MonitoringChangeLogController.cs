using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/monitoring/change-log")]
[Authorize]
public class MonitoringChangeLogController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly MonitoringAuthorizationService _authz;

    public MonitoringChangeLogController(ErrorServiceDbContext db, MonitoringAuthorizationService authz)
    {
        _db = db;
        _authz = authz;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? code,
        [FromQuery] int? monitoringId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (monitoringId.HasValue && monitoringId.Value > 0)
        {
            if (!await _authz.CanAccessMonitoring(User, monitoringId.Value))
                return Forbid();
        }
        else if (!string.IsNullOrWhiteSpace(code))
        {
            if (!await _authz.CanAccessDeviceCode(User, code))
                return Forbid();
        }

        var query = _db.MonitoringDeviceChangeLogs.AsQueryable();

        if (monitoringId.HasValue && monitoringId.Value > 0)
            query = query.Where(x => x.MonitoringId == monitoringId.Value);
        else if (!string.IsNullOrWhiteSpace(code))
            query = query.Where(x => x.DeviceCode == code);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.DeviceCode,
                x.ChangeType,
                x.ChangeDescription,
                x.DataSnapshot,
                x.CustomerReceiptId,
                x.WorkshopId,
                x.CreatedAtFa
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        if (!await _authz.CanAccessChangeLog(User, id))
            return Forbid();

        var log = await _db.MonitoringDeviceChangeLogs
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.DeviceCode,
                x.ChangeType,
                x.OldValue,
                x.NewValue,
                x.ChangeDescription,
                x.DataSnapshot,
                x.CustomerReceiptId,
                x.WorkshopId,
                x.CreatedAtFa
            })
            .FirstOrDefaultAsync();

        if (log == null) return NotFound();
        return Ok(log);
    }
}
