using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "perm:admin.access")]
public class SystemEventsController : ControllerBase
{
    private readonly SystemEventService _eventService;

    public SystemEventsController(SystemEventService eventService)
    {
        _eventService = eventService;
    }

    [HttpGet("logs")]
    public async Task<ActionResult<List<SystemEventLogDto>>> GetLogs([FromQuery] EventLogFilterDto filter)
    {
        var logs = await _eventService.GetLogsAsync(filter);
        return Ok(logs);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<SystemHealthSummaryDto>> GetSummary()
    {
        var summary = await _eventService.GetHealthSummaryAsync();
        return Ok(summary);
    }

    [HttpPost("resolve/{id}")]
    public async Task<IActionResult> ResolveEvent(int id)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(userIdStr, out var userId))
        {
            await _eventService.ResolveEventAsync(id, userId);
            return Ok();
        }
        return Unauthorized();
    }

    [HttpPost("client-log")]
    [AllowAnonymous]
    public async Task<IActionResult> LogClientError([FromBody] SystemEventLog log)
    {
        // امن‌سازی لاگ‌های دریافتی از کلاینت
        log.Category = EventCategory.Frontend;
        log.OccurredAt = DateTimeOffset.UtcNow;
        log.ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        log.UserAgent = Request.Headers["User-Agent"];
        
        await _eventService.LogEventAsync(log);
        return Ok();
    }
}
