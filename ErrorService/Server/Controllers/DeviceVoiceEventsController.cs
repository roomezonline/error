using ErrorService.Server.Data;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

/// <summary>
/// تحویل رویدادهای صوتی به دستگاه (ESP32) با long-poll.
/// دستگاه با cursor (AfterId) فقط رویدادهای جدید همان دستگاه را می‌گیرد؛
/// رویدادهای کهنه (بیش از ۲ دقیقه) یا بدون اکشن فعال نادیده گرفته می‌شوند.
/// </summary>
[ApiController]
[Route("api/device/voice-events")]
[AllowAnonymous]
public sealed class DeviceVoiceEventsController : ControllerBase
{
    private const int MaxWaitSeconds = 25;
    private const int BatchSize = 10;

    private readonly ErrorServiceDbContext _db;
    private readonly string? _configuredBaseUrl;

    public DeviceVoiceEventsController(ErrorServiceDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuredBaseUrl = configuration["Site:BaseUrl"];
    }

    /// <summary>فقط برای مقدار اولیهٔ cursor پس از فلش/پاک‌شدن حافظه.</summary>
    [HttpGet("cursor")]
    public async Task<ActionResult<DeviceVoiceEventsDto>> Cursor(
        [FromQuery] string? code, CancellationToken ct)
    {
        var device = await FindDeviceAsync(code, ct);
        if (device == null) return InvalidDevice(code);

        var max = await _db.DeviceVoiceEvents.AsNoTracking()
            .Where(e => e.MonitoringDeviceId == device.Id)
            .MaxAsync(e => (long?)e.Id, ct) ?? 0;

        return Ok(new DeviceVoiceEventsDto { AfterId = max });
    }

    /// <summary>
    /// دریافت رویدادهای بعد از afterId. اگر رویدادی نباشد، درخواست تا wait ثانیه
    /// باز می‌ماند (long-poll) تا به‌محض وقوع تغییر، صدا بدون تأخیر پخش شود.
    /// </summary>
    [HttpGet("next")]
    public async Task<ActionResult<DeviceVoiceEventsDto>> Next(
        [FromQuery] string? code,
        [FromQuery] long afterId = 0,
        [FromQuery] int wait = 20,
        CancellationToken ct = default)
    {
        var device = await FindDeviceAsync(code, ct);
        if (device == null) return InvalidDevice(code);

        var waitSec = Math.Clamp(wait, 0, MaxWaitSeconds);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(waitSec);
        var baseUrl = ResolveBaseUrl();

        while (!ct.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            var rows = await _db.DeviceVoiceEvents.AsNoTracking()
                .Where(e => e.MonitoringDeviceId == device.Id &&
                            e.Id > afterId &&
                            e.ExpiresAt > now)
                .OrderBy(e => e.Id)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (rows.Count > 0)
            {
                var batchCursor = rows[^1].Id;
                var keys = rows.Select(e => e.ActionKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

                var actions = await _db.ServiceVoiceActions.AsNoTracking()
                    .Where(a => a.MonitoringDeviceId == device.Id &&
                                a.IsActive &&
                                a.AudioUrl != null && a.AudioUrl != "" &&
                                keys.Contains(a.Key))
                    .Select(a => new
                    {
                        a.Key,
                        a.TitleFa,
                        a.RepeatCount,
                        a.Priority,
                        a.AudioUrl,
                        a.UpdatedAt
                    })
                    .ToListAsync(ct);

                var actionByKey = actions
                    .GroupBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var events = new List<DeviceVoiceEventDto>();
                foreach (var row in rows)
                {
                    if (!actionByKey.TryGetValue(row.ActionKey, out var action)) continue;

                    events.Add(new DeviceVoiceEventDto
                    {
                        Id = row.Id,
                        ActionKey = row.ActionKey,
                        TitleFa = string.IsNullOrWhiteSpace(action.TitleFa)
                            ? (ServiceVoiceEventKeys.TitleOf(row.ActionKey) ?? row.ActionKey)
                            : action.TitleFa,
                        RepeatCount = Math.Clamp(action.RepeatCount, 1, 50),
                        Priority = action.Priority,
                        AudioUrl = ToAbsoluteUrl(baseUrl, action.AudioUrl),
                        Format = Path.GetExtension(action.AudioUrl ?? string.Empty).TrimStart('.').ToLowerInvariant(),
                        Version = action.UpdatedAt.ToUnixTimeSeconds(),
                        IsTest = row.IsTest,
                        CreatedAt = row.CreatedAt
                    });
                }

                return Ok(new DeviceVoiceEventsDto
                {
                    AfterId = batchCursor,
                    Count = events.Count,
                    Events = events
                });
            }

            // همهٔ رویدادهای بازهٔ cursor منقضی/بدون اکشن هستند؛ cursor را جلو ببر.
            var maxId = await _db.DeviceVoiceEvents.AsNoTracking()
                .Where(e => e.MonitoringDeviceId == device.Id && e.Id > afterId)
                .MaxAsync(e => (long?)e.Id, ct) ?? afterId;

            if (maxId > afterId)
            {
                afterId = maxId;
                continue;
            }

            if (waitSec <= 0 || DateTimeOffset.UtcNow >= deadline)
                return Ok(new DeviceVoiceEventsDto { AfterId = afterId });

            await Task.Delay(700, ct);
        }

        return Ok(new DeviceVoiceEventsDto { AfterId = afterId });
    }

    private async Task<DeviceInfo?> FindDeviceAsync(string? code, CancellationToken ct)
    {
        var deviceCode = (code ?? string.Empty).Trim();
        if (deviceCode.Length == 0) return null;

        return await _db.MonitoringDevices.AsNoTracking()
            .Where(x => x.DeviceNumber == deviceCode && x.IsActive)
            .Select(x => new DeviceInfo(x.Id, x.DeviceNumber))
            .FirstOrDefaultAsync(ct);
    }

    private ActionResult InvalidDevice(string? code)
    {
        var deviceCode = (code ?? string.Empty).Trim();
        if (deviceCode.Length == 0)
            return BadRequest("کد دستگاه الزامی است؛ مقدار code را در آدرس قرار دهید.");

        return NotFound($"دستگاه فعالی با کد «{deviceCode}» یافت نشد.");
    }

    private string ResolveBaseUrl()
    {
        var configured = (_configuredBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (configured.Length > 0) return configured;

        return $"{Request.Scheme}://{Request.Host}";
    }

    private static string ToAbsoluteUrl(string baseUrl, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        var value = path.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return value;

        return baseUrl + (value.StartsWith('/') ? value : "/" + value);
    }

    private sealed record DeviceInfo(int Id, string DeviceNumber);
}
