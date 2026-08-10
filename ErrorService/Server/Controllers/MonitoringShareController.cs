using System.Text;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using ErrorService.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/monitoring/share")]
public class MonitoringShareController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly MonitoringAuthorizationService _authz;

    public MonitoringShareController(ErrorServiceDbContext db, MonitoringAuthorizationService authz)
    {
        _db = db;
        _authz = authz;
    }

    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] ShareCreateRequest request)
    {
        if (request.MonitoringId <= 0)
            return BadRequest(new { error = "monitoringId is required" });

        var connection = await _db.MonitoringReceiptConnections
            .Include(x => x.MonitoringDevice)
            .FirstOrDefaultAsync(x => x.Id == request.MonitoringId);

        if (connection == null)
            return NotFound(new { error = "Monitoring connection not found" });

        if (!await _authz.CanAccessMonitoring(User, request.MonitoringId))
            return Forbid();

        var token = Guid.NewGuid().ToString("N");

        var shareLink = new MonitoringShareLink
        {
            MonitoringId = connection.Id,
            WorkshopId = connection.WorkshopId,
            CustomerReceiptId = connection.CustomerReceiptId,
            DeviceCode = connection.MonitoringDevice?.DeviceNumber ?? "",
            Token = token,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.MonitoringShareLinks.Add(shareLink);
        await _db.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var url = $"{baseUrl}/monitoring/shared?token={token}";

        return Ok(new { token, url });
    }

    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke([FromBody] ShareRevokeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { error = "token is required" });

        var shareLink = await _db.MonitoringShareLinks
            .FirstOrDefaultAsync(x => x.Token == request.Token && x.IsActive);

        if (shareLink == null)
            return NotFound(new { error = "Share link not found or already revoked" });

        if (!await _authz.CanAccessMonitoring(User, shareLink.MonitoringId))
            return Forbid();

        shareLink.IsActive = false;
        shareLink.RevokedAt = DateTime.UtcNow;

        var activeSessions = await _db.ShareLinkViewerSessions
            .Where(x => x.ShareLinkId == shareLink.Id && x.IsActive)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var s in activeSessions)
        {
            s.IsActive = false;
            s.ExitedAt = now;
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = "Share link revoked" });
    }

    [HttpGet("validate")]
    [AllowAnonymous]
    public async Task<IActionResult> Validate([FromQuery] string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { isValid = false, error = "token is required" });

        var shareLink = await _db.MonitoringShareLinks
            .FirstOrDefaultAsync(x => x.Token == token);

        if (shareLink == null)
            return Ok(new { isValid = false });

        var connection = await _db.MonitoringReceiptConnections
            .Include(x => x.MonitoringDevice)
            .FirstOrDefaultAsync(x => x.Id == shareLink.MonitoringId);

        var deviceCode = connection?.MonitoringDevice?.DeviceNumber ?? "";

        return Ok(new
        {
            isValid = shareLink.IsActive,
            revokedAt = shareLink.RevokedAt,
            monitoringId = shareLink.MonitoringId,
            deviceCode
        });
    }

    [HttpGet("data")]
    [AllowAnonymous]
    public async Task<IActionResult> GetData([FromQuery] string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "token is required" });

        var shareLink = await _db.MonitoringShareLinks
            .FirstOrDefaultAsync(x => x.Token == token && x.IsActive);

        if (shareLink == null)
            return NotFound(new { error = "Share link not found or inactive" });

        var connection = await _db.MonitoringReceiptConnections
            .Include(x => x.MonitoringDevice)
            .FirstOrDefaultAsync(x => x.Id == shareLink.MonitoringId);

        if (connection == null)
            return NotFound(new { error = "Monitoring connection not found" });

        var deviceCode = connection.MonitoringDevice?.DeviceNumber ?? shareLink.MonitoringId.ToString();

        var data = ReadFromFile(deviceCode);
        data.Code = deviceCode;
        return Ok(data);
    }

    [HttpGet("records-history")]
    [AllowAnonymous]
    public async Task<ActionResult<List<MonitoringRecordPoint>>> GetRecordsHistory(
        [FromQuery] string? token,
        [FromQuery] string? field,
        [FromQuery] int count = 50)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(field))
            return BadRequest();

        var shareLink = await _db.MonitoringShareLinks
            .FirstOrDefaultAsync(x => x.Token == token && x.IsActive);

        if (shareLink == null)
            return NotFound(new { error = "Share link not found or inactive" });

        var query = _db.MonitoringDataRecords.AsQueryable();

        if (shareLink.MonitoringId > 0)
            query = query.Where(x => x.MonitoringId == shareLink.MonitoringId);
        else if (!string.IsNullOrWhiteSpace(shareLink.DeviceCode))
            query = query.Where(x => x.DeviceCode == shareLink.DeviceCode);

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

    [HttpGet("connection-info")]
    [AllowAnonymous]
    public async Task<IActionResult> GetConnectionInfo([FromQuery] string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "token is required" });

        var shareLink = await _db.MonitoringShareLinks
            .FirstOrDefaultAsync(x => x.Token == token && x.IsActive);

        if (shareLink == null)
            return NotFound(new { error = "Share link not found or inactive" });

        var connection = await _db.MonitoringReceiptConnections
            .Include(x => x.MonitoringDevice)
            .Include(x => x.CustomerReceipt)
                .ThenInclude(x => x.Customer)
            .Include(x => x.Workshop)
            .FirstOrDefaultAsync(x => x.Id == shareLink.MonitoringId);

        if (connection == null)
            return NotFound(new { error = "Monitoring connection not found" });

            return Ok(new
            {
                connection.Id,
                connection.CustomerReceiptId,
                CustomerName = connection.CustomerReceipt?.Customer?.FirstName + " " + connection.CustomerReceipt?.Customer?.LastName,
                CustomerMobile = connection.CustomerReceipt?.Customer?.Mobile ?? "",
                DeviceTitle = connection.MonitoringDevice?.Title ?? "",
                DeviceCode = connection.MonitoringDevice?.DeviceNumber ?? "",
                WorkshopName = connection.Workshop?.WorkshopName ?? "",
                CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(connection.CreatedAt.ToLocalTime(), true),
                CreatedByUserName = connection.CreatedByUserName ?? "",
                connection.EndedAt,
                EndReason = connection.EndReason ?? "",
                GracePeriodEndAt = connection.GracePeriodEndAt?.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
    }

    [HttpGet("change-log")]
    [AllowAnonymous]
    public async Task<IActionResult> GetChangeLog(
        [FromQuery] string? token,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest();

        var shareLink = await _db.MonitoringShareLinks
            .FirstOrDefaultAsync(x => x.Token == token && x.IsActive);

        if (shareLink == null)
            return NotFound(new { error = "Share link not found or inactive" });

        var query = _db.MonitoringDeviceChangeLogs.AsQueryable();

        if (shareLink.MonitoringId > 0)
            query = query.Where(x => x.MonitoringId == shareLink.MonitoringId);
        else if (!string.IsNullOrWhiteSpace(shareLink.DeviceCode))
            query = query.Where(x => x.DeviceCode == shareLink.DeviceCode);

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

    [HttpPost("viewer-enter")]
    [AllowAnonymous]
    public async Task<IActionResult> ViewerEnter([FromBody] ViewerEnterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { error = "token is required" });

        var shareLink = await _db.MonitoringShareLinks
            .FirstOrDefaultAsync(x => x.Token == request.Token && x.IsActive);

        if (shareLink == null)
            return NotFound(new { error = "Share link not found or inactive" });

        var sessionId = Guid.NewGuid().ToString("N");
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers["User-Agent"].ToString();

        var session = new ShareLinkViewerSession
        {
            ShareLinkId = shareLink.Id,
            SessionId = sessionId,
            IpAddress = ip,
            UserAgent = ua,
            DeviceInfo = request.DeviceInfo,
            EnteredAt = DateTime.UtcNow,
            LastPingAt = DateTime.UtcNow
        };

        _db.ShareLinkViewerSessions.Add(session);
        await _db.SaveChangesAsync();

        return Ok(new { sessionId });
    }

    [HttpPost("viewer-ping")]
    [AllowAnonymous]
    public async Task<IActionResult> ViewerPing([FromBody] ViewerPingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
            return BadRequest(new { error = "sessionId is required" });

        var session = await _db.ShareLinkViewerSessions
            .FirstOrDefaultAsync(x => x.SessionId == request.SessionId && x.IsActive);

        if (session == null)
            return NotFound(new { error = "Session not found or inactive" });

        session.LastPingAt = DateTime.UtcNow;
        session.PingCount++;
        await _db.SaveChangesAsync();

        return Ok(new { status = "ok" });
    }

    [HttpPost("viewer-exit")]
    [AllowAnonymous]
    public async Task<IActionResult> ViewerExit([FromBody] ViewerExitRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
            return BadRequest(new { error = "sessionId is required" });

        var session = await _db.ShareLinkViewerSessions
            .FirstOrDefaultAsync(x => x.SessionId == request.SessionId && x.IsActive);

        if (session == null)
            return NotFound(new { error = "Session not found or inactive" });

        session.ExitedAt = DateTime.UtcNow;
        session.IsActive = false;
        await _db.SaveChangesAsync();

        return Ok(new { status = "ok" });
    }

    [HttpGet("active")]
    [Authorize]
    public async Task<IActionResult> GetActive([FromQuery] int monitoringId)
    {
        if (monitoringId <= 0)
            return BadRequest(new { error = "monitoringId is required" });

        if (!await _authz.CanAccessMonitoring(User, monitoringId))
            return Forbid();

        var shareLink = await _db.MonitoringShareLinks
            .FirstOrDefaultAsync(x => x.MonitoringId == monitoringId && x.IsActive);

        if (shareLink == null)
            return Ok(new { hasActiveLink = false });

        return Ok(new
        {
            hasActiveLink = true,
            token = shareLink.Token,
            createdAt = shareLink.CreatedAt
        });
    }

    [HttpGet("viewers")]
    [Authorize]
    public async Task<IActionResult> GetViewers([FromQuery] int monitoringId)
    {
        if (monitoringId <= 0)
            return BadRequest(new { error = "monitoringId is required" });

        if (!await _authz.CanAccessMonitoring(User, monitoringId))
            return Forbid();

        var shareLink = await _db.MonitoringShareLinks
            .FirstOrDefaultAsync(x => x.MonitoringId == monitoringId && x.IsActive);

        if (shareLink == null)
            return Ok(new { active = Array.Empty<object>(), recent = Array.Empty<object>(), totalActive = 0 });

        var cutoff = DateTime.UtcNow.AddSeconds(-30);
        var recentCutoff = DateTime.UtcNow.AddHours(-1);

        var sessions = await _db.ShareLinkViewerSessions
            .Where(x => x.ShareLinkId == shareLink.Id && x.LastPingAt > recentCutoff)
            .OrderByDescending(x => x.LastPingAt)
            .Select(x => new
            {
                x.SessionId,
                x.IpAddress,
                x.DeviceInfo,
                x.UserAgent,
                x.EnteredAt,
                x.LastPingAt,
                x.ExitedAt,
                x.IsActive,
                IsOnline = x.IsActive && x.LastPingAt > cutoff
            })
            .ToListAsync();

        return Ok(new
        {
            active = sessions.Where(s => s.IsOnline).ToList(),
            recent = sessions.Where(s => !s.IsOnline).ToList(),
            totalActive = sessions.Count(s => s.IsOnline)
        });
    }

    private static MonitoringDeviceData ReadFromFile(string code)
    {
        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitoring-data");
        var filePath = Path.Combine(baseDir, code, "server", $"device_{code}.txt");

        var result = new MonitoringDeviceData { Code = code };

        if (!System.IO.File.Exists(filePath))
        {
            result.Priz = "off";
            result.MotorState = "0";
            result.Element1 = "0";
            result.Element2 = "0";
            result.Temp1 = "0";
            result.Temp2 = "0";
            result.Temp3 = "0";
            result.Jaryan = "0";
            result.Voltage = "0";
            result.Tavan = "0";
            result.Timestamp = DateTime.MinValue;
            result.TimestampFa = "";
            return result;
        }

        var lines = System.IO.File.ReadAllLines(filePath, Encoding.UTF8);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0) continue;
            var key = trimmed[..eqIdx].Trim().ToLowerInvariant();
            var val = trimmed[(eqIdx + 1)..].Trim();

            switch (key)
            {
                case "priz": result.Priz = val; break;
                case "motor_state": result.MotorState = val; break;
                case "element1": result.Element1 = val; break;
                case "element2": result.Element2 = val; break;
                case "temp1": result.Temp1 = val; break;
                case "temp2": result.Temp2 = val; break;
                case "temp3": result.Temp3 = val; break;
                case "jaryan": result.Jaryan = val; break;
                case "cnt_m": result.CntM = val; break;
                case "cnt_e1": result.CntE1 = val; break;
                case "cnt_e2": result.CntE2 = val; break;
                case "current_type": result.CurrentType = val; break;
                case "inverter": result.Inverter = val; break;
                case "voltage": result.Voltage = val; break;
                case "tavan": result.Tavan = val; break;
                case "freq": result.Freq = val; break;
                case "dc1": result.Dc1 = val; break;
                case "dc2": result.Dc2 = val; break;
                case "ac1": result.Ac1 = val; break;
                case "ac2": result.Ac2 = val; break;
                case "device_type": result.DeviceType = val; break;
                case "e1_start_fridge": result.E1StartFridge = val; break;
                case "e1_stop_fridge": result.E1StopFridge = val; break;
                case "e1_start_freezer": result.E1StartFreezer = val; break;
                case "e1_stop_freezer": result.E1StopFreezer = val; break;
                case "e2_start_fridge": result.E2StartFridge = val; break;
                case "e2_stop_fridge": result.E2StopFridge = val; break;
                case "e2_start_freezer": result.E2StartFreezer = val; break;
                case "e2_stop_freezer": result.E2StopFreezer = val; break;
                case "timestamp":
                    if (DateTime.TryParse(val, out var ts))
                    {
                        result.Timestamp = ts;
                        result.TimestampFa = PersianDateHelper.ToPersianDateTimeString(ts, true);
                    }
                    break;
            }
        }

        result.FileWriteTime = System.IO.File.GetLastWriteTimeUtc(filePath);
        result.FileWriteTimeFa = PersianDateHelper.ToPersianDateTimeString(result.FileWriteTime.ToLocalTime(), true);

        return result;
    }
}

public sealed class ShareCreateRequest
{
    public int MonitoringId { get; set; }
}

public sealed class ShareRevokeRequest
{
    public string Token { get; set; } = "";
}

public sealed class ViewerEnterRequest
{
    public string Token { get; set; } = "";
    public string? DeviceInfo { get; set; }
}

public sealed class ViewerPingRequest
{
    public string SessionId { get; set; } = "";
}

public sealed class ViewerExitRequest
{
    public string SessionId { get; set; } = "";
}
