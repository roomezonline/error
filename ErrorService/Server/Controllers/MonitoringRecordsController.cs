using ErrorService.Server.Data;
using ErrorService.Server.Services;
using ErrorService.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/monitoring/records-history")]
[Authorize]
public class MonitoringRecordsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly MonitoringAuthorizationService _authz;

    public MonitoringRecordsController(ErrorServiceDbContext db, MonitoringAuthorizationService authz)
    {
        _db = db;
        _authz = authz;
    }

    [HttpGet]
    public async Task<ActionResult<List<MonitoringRecordPoint>>> GetHistory(
        [FromQuery] string code,
        [FromQuery] string field,
        [FromQuery] int? monitoringId = null,
        [FromQuery] int count = 50)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(field))
            return BadRequest();

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

        var query = _db.MonitoringDataRecords.AsQueryable();

        if (monitoringId.HasValue && monitoringId.Value > 0)
            query = query.Where(x => x.MonitoringId == monitoringId.Value);
        else
            query = query.Where(x => x.DeviceCode == code);

        query = query.OrderByDescending(x => x.Timestamp).Take(count);

        List<MonitoringRecordPoint> items;

        switch (field.ToLowerInvariant())
        {
            case "fridge":
                items = await query.Select(x => new MonitoringRecordPoint { Timestamp = x.Timestamp, Value = x.TemperatureRef }).ToListAsync();
                break;
            case "freezer":
                items = await query.Select(x => new MonitoringRecordPoint { Timestamp = x.Timestamp, Value = x.TemperatureFreez }).ToListAsync();
                break;
            case "ambient":
                items = await query.Select(x => new MonitoringRecordPoint { Timestamp = x.Timestamp, Value = x.TemperatureEnv }).ToListAsync();
                break;
            case "current":
                items = await query.Select(x => new MonitoringRecordPoint { Timestamp = x.Timestamp, Value = x.Jaryan }).ToListAsync();
                break;
            case "power":
                items = await query.Select(x => new MonitoringRecordPoint { Timestamp = x.Timestamp, Value = x.Power }).ToListAsync();
                break;
            case "motor":
                items = await query.Select(x => new MonitoringRecordPoint { Timestamp = x.Timestamp, Value = x.MotorState ? 1.0 : 0.0 }).ToListAsync();
                break;
            case "h1":
                items = await query.Select(x => new MonitoringRecordPoint { Timestamp = x.Timestamp, Value = x.Element1 ? 1.0 : 0.0 }).ToListAsync();
                break;
            case "h2":
                items = await query.Select(x => new MonitoringRecordPoint { Timestamp = x.Timestamp, Value = x.Element2 ? 1.0 : 0.0 }).ToListAsync();
                break;
            default:
                items = new List<MonitoringRecordPoint>();
                break;
        }

        items.Reverse();
        return Ok(items);
    }
}
