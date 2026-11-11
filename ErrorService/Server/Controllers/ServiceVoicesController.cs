using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

/// <summary>
/// مدیریت اکشن‌های صوتی «آوای ارورسرویس» — دسترسی پیش‌فرض فقط سوپر ادمین.
/// فایل صدا در wwwroot/uploads/service-voices ذخیره می‌شود.
/// </summary>
[ApiController]
[Route("api/admin/service-voices")]
[Authorize(Policy = "perm:admin.servicevoices.manage")]
public sealed class ServiceVoicesController : ControllerBase
{
    private const long MaxAudioBytes = 20_000_000;

    // فرمت واحد و قابل پخش روی ESP32-C3 فقط MP3 است.
    private static readonly string[] AllowedExtensions = { ".mp3" };

    private static readonly Dictionary<string, string> ExtensionByMime = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/mpeg"] = ".mp3",
        ["audio/mp3"] = ".mp3",
        ["audio/mpeg3"] = ".mp3",
        ["audio/x-mpeg-3"] = ".mp3"
    };

    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly DeviceVoiceEventService _voiceEvents;

    public ServiceVoicesController(
        ErrorServiceDbContext db,
        IWebHostEnvironment env,
        DeviceVoiceEventService voiceEvents)
    {
        _db = db;
        _env = env;
        _voiceEvents = voiceEvents;
    }

    private string WebRoot => _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");

    private string VoicesRoot => Path.Combine(WebRoot, "uploads", "service-voices");

    [HttpGet]
    public async Task<ActionResult<List<ServiceVoiceActionDto>>> List(
        [FromQuery] int? deviceId, CancellationToken ct)
    {
        var query = _db.ServiceVoiceActions.AsNoTracking();

        if (deviceId is > 0)
            query = query.Where(x => x.MonitoringDeviceId == deviceId);

        var items = await query
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id)
            .Select(x => new ServiceVoiceActionDto
            {
                Id = x.Id,
                DeviceId = x.MonitoringDeviceId,
                DeviceTitle = x.MonitoringDevice != null ? x.MonitoringDevice.Title : null,
                DeviceNumber = x.MonitoringDevice != null ? x.MonitoringDevice.DeviceNumber : null,
                Key = x.Key,
                TitleFa = x.TitleFa,
                IsActive = x.IsActive,
                RepeatCount = x.RepeatCount,
                Priority = x.Priority,
                AudioUrl = x.AudioUrl,
                AudioSizeBytes = x.AudioSizeBytes,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>
    /// دستگاه‌های فعالی که در حال حاضر به یک کارگاه متصل هستند؛ مبنای انتخاب دستگاه در فرم آواها.
    /// </summary>
    [HttpGet("devices")]
    public async Task<ActionResult<List<ServiceVoiceDeviceDto>>> ListDevices(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var assignments = await _db.MonitoringDeviceAssignments.AsNoTracking()
            .Where(x => x.StartAt <= now && (x.EndAt == null || x.EndAt > now))
            .Select(x => new
            {
                x.MonitoringDeviceId,
                DeviceTitle = x.MonitoringDevice.Title,
                x.MonitoringDevice.DeviceNumber,
                x.MonitoringDevice.IsActive,
                WorkshopTitle = x.Workshop.WorkshopName
            })
            .ToListAsync(ct);

        var counts = await _db.ServiceVoiceActions.AsNoTracking()
            .Where(x => x.MonitoringDeviceId != null)
            .GroupBy(x => x.MonitoringDeviceId)
            .Select(g => new { DeviceId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var countByDevice = counts
            .Where(x => x.DeviceId.HasValue)
            .ToDictionary(x => x.DeviceId!.Value, x => x.Count);

        var devices = assignments
            .Where(x => x.IsActive)
            .GroupBy(x => x.MonitoringDeviceId)
            .Select(g =>
            {
                var first = g.First();
                var workshops = g.Select(x => x.WorkshopTitle)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                return new ServiceVoiceDeviceDto
                {
                    Id = g.Key,
                    Title = first.DeviceTitle,
                    DeviceNumber = first.DeviceNumber,
                    WorkshopTitle = workshops.Count > 0 ? string.Join(" / ", workshops) : null,
                    IsActive = true,
                    ActionsCount = countByDevice.TryGetValue(g.Key, out var c) ? c : 0
                };
            })
            .OrderBy(x => x.WorkshopTitle)
            .ThenBy(x => x.Title)
            .ToList();

        return Ok(devices);
    }

    /// <summary>
    /// آخرین رویدادهای صوتی یک دستگاه برای بازبینی در پنل: هر ردیف نشان می‌دهد
    /// کدام تغییر پایش رخ داده، چه اکشنی انتخاب شده و آیا صدا به دستگاه رسیده است.
    /// هر رویداد به رسید مشتری/اتصال پایش همان لحظه نسبت داده می‌شود تا فهرست
    /// بر اساس شمارهٔ رسید دسته‌بندی شود.
    /// </summary>
    [HttpGet("events")]
    public async Task<ActionResult<List<ServiceVoiceEventLogDto>>> RecentEvents(
        [FromQuery] int deviceId, [FromQuery] int take = 60, CancellationToken ct = default)
    {
        if (deviceId <= 0)
            return BadRequest("شناسه دستگاه نامعتبر است.");

        take = Math.Clamp(take, 1, 200);
        var now = DateTimeOffset.UtcNow;

        var events = await _db.DeviceVoiceEvents.AsNoTracking()
            .Where(x => x.MonitoringDeviceId == deviceId)
            .OrderByDescending(x => x.Id)
            .Take(take)
            .Select(x => new ServiceVoiceEventLogDto
            {
                Id = x.Id,
                ActionKey = x.ActionKey,
                SourceType = x.SourceType,
                OldValue = x.OldValue,
                NewValue = x.NewValue,
                DescriptionFa = x.DescriptionFa,
                IsTest = x.IsTest,
                IsExpired = x.ExpiresAt <= now,
                DeliveredAt = x.DeliveredAt,
                IsDelivered = x.DeliveredAt != null,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        if (events.Count == 0)
            return Ok(events);

        var actions = await _db.ServiceVoiceActions.AsNoTracking()
            .Where(a => a.MonitoringDeviceId == deviceId)
            .Select(a => new { a.Key, a.TitleFa, a.AudioUrl, a.IsActive })
            .ToListAsync(ct);

        var actionByKey = new Dictionary<string, (string TitleFa, string? AudioUrl, bool IsActive)>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in actions)
            actionByKey.TryAdd(action.Key, (action.TitleFa, action.AudioUrl, action.IsActive));

        // بازه‌های اتصال دستگاه به رسید مشتری؛ هر رویداد به اتصالی نسبت داده می‌شود
        // که در لحظهٔ وقوع آن باز بوده است (شامل بازهٔ مهلت پس از قطع).
        var oldest = events.Min(x => x.CreatedAt);
        var connections = await _db.MonitoringReceiptConnections.AsNoTracking()
            .Where(c => c.MonitoringDeviceId == deviceId &&
                        c.CreatedAt <= now &&
                        (c.EndedAt == null || c.GracePeriodEndAt >= oldest || c.EndedAt >= oldest))
            .Select(c => new
            {
                c.Id,
                c.CustomerReceiptId,
                c.CreatedAt,
                c.EndedAt,
                c.GracePeriodEndAt,
                CustomerFirstName = c.CustomerReceipt.Customer.FirstName,
                CustomerLastName = c.CustomerReceipt.Customer.LastName
            })
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        var connectionWindows = connections
            .Select(c => new
            {
                c.Id,
                c.CustomerReceiptId,
                Start = c.CreatedAt,
                End = c.EndedAt == null ? (DateTimeOffset?)null : (c.GracePeriodEndAt ?? c.EndedAt),
                CustomerName = $"{c.CustomerFirstName} {c.CustomerLastName}".Trim()
            })
            .ToList();

        foreach (var ev in events)
        {
            ev.EventTitleFa = ServiceVoiceEventKeys.TitleOf(ev.ActionKey) ?? ev.ActionKey;

            if (actionByKey.TryGetValue(ev.ActionKey, out var action))
            {
                ev.ActionTitleFa = action.TitleFa;
                ev.AudioUrl = action.AudioUrl;
                ev.ActionIsActive = action.IsActive;
                ev.HasAudio = !string.IsNullOrWhiteSpace(action.AudioUrl)
                              && action.AudioUrl.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
            }

            var connection = connectionWindows
                .Where(c => c.Start <= ev.CreatedAt && (c.End == null || c.End >= ev.CreatedAt))
                .OrderByDescending(c => c.Start)
                .FirstOrDefault();

            if (connection != null)
            {
                ev.ReceiptConnectionId = connection.Id;
                ev.CustomerReceiptId = connection.CustomerReceiptId;
                ev.ReceiptIsOpen = connection.End == null;
                ev.CustomerName = connection.CustomerName.Length == 0 ? null : connection.CustomerName;
            }
        }

        return Ok(events);
    }

    /// <summary>حذف یک رویداد صوتی از گزارش دستگاه.</summary>
    [HttpDelete("events/{id:int}")]
    public async Task<IActionResult> DeleteEvent(int id, CancellationToken ct)
    {
        var entity = await _db.DeviceVoiceEvents.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)
            return NotFound("رویداد یافت نشد.");

        _db.DeviceVoiceEvents.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>پاک‌کردن همهٔ رویدادهای صوتی یک دستگاه از گزارش.</summary>
    [HttpDelete("events")]
    public async Task<ActionResult<object>> ClearEvents([FromQuery] int deviceId, CancellationToken ct)
    {
        if (deviceId <= 0)
            return BadRequest("شناسه دستگاه نامعتبر است.");

        var removed = await _db.DeviceVoiceEvents
            .Where(x => x.MonitoringDeviceId == deviceId)
            .ExecuteDeleteAsync(ct);

        return Ok(new { removed });
    }

    [HttpPost]
    public async Task<ActionResult<ServiceVoiceActionDto>> Save(
        [FromBody] ServiceVoiceActionUpsertRequest req, CancellationToken ct)
    {
        var key = NormalizeKey(req.Key);
        if (key == null)
            return BadRequest("کلید اکشن باید ۲ تا ۴۰ کاراکتر و شامل حروف انگلیسی کوچک، عدد، خط تیره یا زیرخط باشد.");

        var title = (req.TitleFa ?? string.Empty).Trim();
        if (title.Length == 0)
            return BadRequest("نام اکشن الزامی است.");
        if (title.Length > 120)
            title = title[..120];

        if (req.MonitoringDeviceId <= 0)
            return BadRequest("انتخاب دستگاه فعال کارگاه الزامی است.");

        var device = await _db.MonitoringDevices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.MonitoringDeviceId, ct);
        if (device == null)
            return BadRequest("دستگاه انتخابی یافت نشد.");
        if (!device.IsActive)
            return BadRequest("دستگاه انتخابی غیرفعال است.");

        ServiceVoiceAction? entity = null;
        if (req.Id > 0)
        {
            entity = await _db.ServiceVoiceActions.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
            if (entity == null)
                return NotFound("اکشن یافت نشد.");
        }

        // کلید اکشن فقط در محدودهٔ همان دستگاه باید یکتا باشد
        var keyTaken = await _db.ServiceVoiceActions
            .AnyAsync(x => x.MonitoringDeviceId == req.MonitoringDeviceId &&
                           x.Key == key && (entity == null || x.Id != entity.Id), ct);
        if (keyTaken)
            return Conflict("این کلید برای همین دستگاه قبلاً استفاده شده است.");

        var now = DateTimeOffset.UtcNow;
        if (entity == null)
        {
            entity = new ServiceVoiceAction { CreatedAt = now };
            _db.ServiceVoiceActions.Add(entity);
        }

        entity.MonitoringDeviceId = req.MonitoringDeviceId;
        entity.Key = key;
        entity.TitleFa = title;
        entity.IsActive = req.IsActive;
        entity.RepeatCount = Math.Clamp(req.RepeatCount, 1, 50);
        entity.Priority = Math.Clamp(req.Priority, 0, 1000);
        entity.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return Ok(Map(entity, device));
    }

    [HttpPost("{id:int}/toggle")]
    public async Task<ActionResult<ServiceVoiceActionDto>> Toggle(int id, CancellationToken ct)
    {
        var entity = await _db.ServiceVoiceActions
            .Include(x => x.MonitoringDevice)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)
            return NotFound("اکشن یافت نشد.");

        entity.IsActive = !entity.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }

    [HttpPost("{id:int}/audio")]
    [RequestSizeLimit(MaxAudioBytes + 1_000_000)]
    public async Task<ActionResult<ServiceVoiceActionDto>> UploadAudio(
        int id, [FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length <= 0)
            return BadRequest("فایل صوتی خالی است یا دریافت نشد.");
        if (file.Length > MaxAudioBytes)
            return BadRequest("حجم فایل صوتی باید کمتر از ۲۰ مگابایت باشد.");

        var entity = await _db.ServiceVoiceActions
            .Include(x => x.MonitoringDevice)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)
            return NotFound("اکشن یافت نشد.");

        var ext = ResolveExtension(file);
        if (ext == null)
            return BadRequest("فقط فایل MP3 مجاز است. ضبط جدید در پنل به‌صورت خودکار به MP3 تبدیل می‌شود.");

        if (!await IsMp3Async(file, ct))
            return BadRequest("فایل ارسالی MP3 معتبر نیست. لطفاً دوباره ضبط کنید (تبدیل خودکار در پنل انجام می‌شود).");

        Directory.CreateDirectory(VoicesRoot);
        var fileName = $"{SanitizeForFile(entity.Key)}-{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(VoicesRoot, fileName);

        await using (var source = file.OpenReadStream())
        await using (var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            if (source.CanSeek) source.Position = 0;
            await source.CopyToAsync(target, ct);
        }

        DeleteAudioFile(entity.AudioUrl);

        entity.AudioUrl = $"/uploads/service-voices/{fileName}";
        entity.AudioSizeBytes = file.Length;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(Map(entity));
    }

    /// <summary>
    /// ارسال رویداد آزمایشی به دستگاه؛ اگر دستگاه آنلاین باشد همان لحظه پخش می‌شود.
    /// </summary>
    [HttpPost("{id:int}/test")]
    public async Task<ActionResult<ServiceVoiceTestResultDto>> Test(int id, CancellationToken ct)
    {
        var entity = await _db.ServiceVoiceActions
            .Include(x => x.MonitoringDevice)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (entity == null)
            return NotFound("اکشن یافت نشد.");

        if (entity.MonitoringDevice is not { IsActive: true })
            return BadRequest("دستگاه این اکشن فعال نیست یا به کارگاه متصل نیست.");

        if (!entity.IsActive)
            return BadRequest("این اکشن غیرفعال است؛ برای پخش آزمایشی ابتدا آن را فعال کنید.");

        if (string.IsNullOrWhiteSpace(entity.AudioUrl) ||
            !entity.AudioUrl.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            return BadRequest("برای این اکشن فایل MP3 ذخیره نشده است؛ ابتدا صدا را ضبط و ذخیره کنید.");

        var now = DateTimeOffset.UtcNow;
        var ev = _voiceEvents.AddTestEvent(_db, entity.MonitoringDevice, entity, now);
        await _db.SaveChangesAsync(ct);

        return Ok(new ServiceVoiceTestResultDto
        {
            Ok = true,
            EventId = ev.Id,
            Message = "درخواست پخش آزمایشی برای دستگاه ارسال شد."
        });
    }

    [HttpDelete("{id:int}/audio")]
    public async Task<ActionResult<ServiceVoiceActionDto>> DeleteAudio(int id, CancellationToken ct)
    {
        var entity = await _db.ServiceVoiceActions
            .Include(x => x.MonitoringDevice)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)
            return NotFound("اکشن یافت نشد.");

        DeleteAudioFile(entity.AudioUrl);
        entity.AudioUrl = null;
        entity.AudioSizeBytes = 0;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.ServiceVoiceActions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)
            return NotFound("اکشن یافت نشد.");

        DeleteAudioFile(entity.AudioUrl);
        _db.ServiceVoiceActions.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static ServiceVoiceActionDto Map(ServiceVoiceAction x, MonitoringDevice? device = null)
    {
        var dev = x.MonitoringDevice ?? device;
        return new ServiceVoiceActionDto
        {
            Id = x.Id,
            DeviceId = x.MonitoringDeviceId ?? dev?.Id,
            DeviceTitle = dev?.Title,
            DeviceNumber = dev?.DeviceNumber,
            Key = x.Key,
            TitleFa = x.TitleFa,
            IsActive = x.IsActive,
            RepeatCount = x.RepeatCount,
            Priority = x.Priority,
            AudioUrl = x.AudioUrl,
            AudioSizeBytes = x.AudioSizeBytes,
            UpdatedAt = x.UpdatedAt
        };
    }

    private static string? NormalizeKey(string? raw)
    {
        var key = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (key.StartsWith('@')) key = key[1..];
        if (key.Length is < 2 or > 40) return null;

        foreach (var c in key)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')
                return null;
        }

        return key;
    }

    private static string? ResolveExtension(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        if (AllowedExtensions.Contains(ext)) return ext;

        var mime = (file.ContentType ?? string.Empty).Split(';')[0].Trim();
        return ExtensionByMime.TryGetValue(mime, out var mapped) ? mapped : null;
    }

    /// <summary>بررسی سبک هدر MP3: ID3 یا frame sync. جلوی فایل اشتباه با پسوند mp3 را می‌گیرد.</summary>
    private static async Task<bool> IsMp3Async(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[3];
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);
        if (read < 2) return false;

        // ID3v2
        if (read >= 3 && header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33)
            return true;

        // MPEG audio frame sync (11 bits set)
        return header[0] == 0xFF && (header[1] & 0xE0) == 0xE0;
    }

    private static string SanitizeForFile(string key)
    {
        var chars = key.Select(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray();
        var safe = new string(chars);
        return safe.Length > 40 ? safe[..40] : safe;
    }

    /// <summary>حذف فایل قبلی فقط اگر واقعاً داخل پوشه آواها باشد</summary>
    private void DeleteAudioFile(string? audioUrl)
    {
        if (string.IsNullOrWhiteSpace(audioUrl)) return;

        var name = Path.GetFileName(audioUrl);
        if (string.IsNullOrWhiteSpace(name)) return;

        var root = Path.GetFullPath(VoicesRoot) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(VoicesRoot, name));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            if (System.IO.File.Exists(full))
                System.IO.File.Delete(full);
        }
        catch
        {
            // حذف فایل نباید ذخیره رکورد را متوقف کند
        }
    }
}
