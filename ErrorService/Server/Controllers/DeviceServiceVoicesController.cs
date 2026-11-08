using ErrorService.Server.Data;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

/// <summary>
/// فهرست عمومی و فقط‌خواندنی آواها برای دستگاه‌های سخت‌افزاری (ESP32-C3).
/// دستگاه با کد خودش (DeviceNumber) این فهرست را می‌خواند و فقط اکشن‌های
/// همان دستگاه را دریافت می‌کند. فقط اکشن‌های فعال و دارای فایل برمی‌گردند.
/// نمونه: GET /api/device/service-voices?code=AVA-001
/// </summary>
[ApiController]
[Route("api/device/service-voices")]
[AllowAnonymous]
public sealed class DeviceServiceVoicesController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly string? _configuredBaseUrl;

    public DeviceServiceVoicesController(ErrorServiceDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuredBaseUrl = configuration["Site:BaseUrl"];
    }

    [HttpGet]
    public async Task<ActionResult<DeviceServiceVoicesDto>> Get(
        [FromQuery] string? code, CancellationToken ct)
    {
        var deviceCode = (code ?? string.Empty).Trim();
        if (deviceCode.Length == 0)
            return BadRequest("کد دستگاه الزامی است؛ مقدار code را در آدرس قرار دهید.");

        var device = await _db.MonitoringDevices.AsNoTracking()
            .Where(x => x.DeviceNumber == deviceCode)
            .Select(x => new { x.Id, x.Title, x.DeviceNumber, x.IsActive })
            .FirstOrDefaultAsync(ct);

        if (device == null)
            return NotFound($"دستگاهی با کد «{deviceCode}» یافت نشد.");

        if (!device.IsActive)
            return NotFound($"دستگاه «{deviceCode}» غیرفعال است.");

        var rows = await _db.ServiceVoiceActions.AsNoTracking()
            .Where(x => x.MonitoringDeviceId == device.Id &&
                        x.IsActive && x.AudioUrl != null && x.AudioUrl != "" &&
                        x.AudioUrl.EndsWith(".mp3"))
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Key,
                x.TitleFa,
                x.RepeatCount,
                x.Priority,
                x.AudioUrl,
                x.AudioSizeBytes,
                x.UpdatedAt
            })
            .ToListAsync(ct);

        var baseUrl = ResolveBaseUrl();
        var actions = rows.Select(x => new DeviceServiceVoiceDto
        {
            Id = x.Id,
            Key = x.Key,
            TitleFa = x.TitleFa,
            RepeatCount = Math.Clamp(x.RepeatCount, 1, 50),
            Priority = x.Priority,
            AudioUrl = ToAbsoluteUrl(baseUrl, x.AudioUrl),
            Format = Path.GetExtension(x.AudioUrl ?? string.Empty).TrimStart('.').ToLowerInvariant(),
            AudioSizeBytes = x.AudioSizeBytes,
            Version = x.UpdatedAt.ToUnixTimeSeconds()
        }).ToList();

        return Ok(new DeviceServiceVoicesDto
        {
            DeviceNumber = device.DeviceNumber,
            DeviceTitle = device.Title,
            // بزرگ‌ترین زمان ویرایش، مبنای «نسخهٔ فهرست» است؛ دستگاه فقط وقتی
            // این عدد عوض شود فهرست را جایگزین می‌کند.
            Version = actions.Count == 0 ? 0 : actions.Max(x => x.Version),
            Count = actions.Count,
            Actions = actions
        });
    }

    private string ResolveBaseUrl()
    {
        var configured = (_configuredBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (configured.Length > 0) return configured;

        // اگر Site:BaseUrl تنظیم نشده باشد، همان میزبان درخواست استفاده می‌شود.
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
}
