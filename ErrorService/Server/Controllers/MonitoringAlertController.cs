using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/monitoring/alert")]
[Authorize]
public class MonitoringAlertController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly MonitoringAuthorizationService _authz;

    public MonitoringAlertController(ErrorServiceDbContext db, MonitoringAuthorizationService authz)
    {
        _db = db;
        _authz = authz;
    }

    [HttpGet]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] string code,
        [FromQuery] string state = "all",
        [FromQuery] int? monitoringId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("code is required");

        if (monitoringId.HasValue && monitoringId.Value > 0)
        {
            if (!await _authz.CanAccessMonitoring(User, monitoringId.Value))
                return Forbid();
        }
        else
        {
            if (!await _authz.CanAccessDeviceCode(User, code))
                return Forbid();
        }

        var query = _db.MonitoringAlerts.AsQueryable();

        if (monitoringId.HasValue && monitoringId.Value > 0)
            query = query.Where(a => a.MonitoringId == monitoringId.Value);
        else
            query = query.Where(a => a.DeviceCode == code);

        if (state == "active")
            query = query.Where(a => a.State == true);
        else if (state == "resolved")
            query = query.Where(a => a.State == false);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.Time)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AlertDto
            {
                Id = a.Id,
                DeviceCode = a.DeviceCode,
                MonitoringId = a.MonitoringId,
                Type = a.Type,
                Num = a.Num,
                Message = a.Message,
                Command = a.Command,
                Time = a.Time,
                State = a.State
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetActiveCount([FromQuery] string code, [FromQuery] int? monitoringId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("code is required");

        if (monitoringId.HasValue && monitoringId.Value > 0)
        {
            if (!await _authz.CanAccessMonitoring(User, monitoringId.Value))
                return Forbid();
        }
        else
        {
            if (!await _authz.CanAccessDeviceCode(User, code))
                return Forbid();
        }

        var query = _db.MonitoringAlerts.Where(a => a.State == true);

        if (monitoringId.HasValue && monitoringId.Value > 0)
            query = query.Where(a => a.MonitoringId == monitoringId.Value);
        else
            query = query.Where(a => a.DeviceCode == code);

        var count = await query.CountAsync();

        return Ok(new { count });
    }

    [HttpPost("{id}/resolve")]
    public async Task<IActionResult> ResolveAlert(int id)
    {
        if (!await _authz.CanAccessAlert(User, id))
            return Forbid();

        var alert = await _db.MonitoringAlerts.FindAsync(id);
        if (alert == null)
            return NotFound();

        alert.State = false;
        await _db.SaveChangesAsync();

        return Ok(new { message = "ok" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAlert(int id)
    {
        if (!await _authz.CanAccessAlert(User, id))
            return Forbid();

        var alert = await _db.MonitoringAlerts.FindAsync(id);
        if (alert == null)
            return NotFound();

        _db.MonitoringAlerts.Remove(alert);
        await _db.SaveChangesAsync();

        return Ok(new { message = "ok" });
    }

    [Authorize]
    [HttpGet("workshop-count")]
    public async Task<ActionResult<int>> GetWorkshopActiveCount([FromQuery] int? workshopId = null)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        int? targetWorkshopId;

        if (isSuperAdmin)
        {
            targetWorkshopId = workshopId;
        }
        else
        {
            targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
        }

        if (targetWorkshopId == null || targetWorkshopId <= 0)
            return Ok(new { count = 0 });

        var count = await _db.MonitoringAlerts
            .AsNoTracking()
            .Where(a => a.State)
            .Where(a => _db.MonitoringReceiptConnections
                .Any(mrc => mrc.MonitoringDevice!.DeviceNumber == a.DeviceCode
                    && mrc.EndedAt == null && mrc.WorkshopId == targetWorkshopId))
            .CountAsync();

        return Ok(new { count });
    }
}
