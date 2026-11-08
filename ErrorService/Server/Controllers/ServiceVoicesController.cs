using ErrorService.Server.Data;
using ErrorService.Server.Models;
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

    private static readonly string[] AllowedExtensions =
        { ".webm", ".ogg", ".oga", ".mp4", ".m4a", ".mp3", ".wav", ".aac" };

    private static readonly Dictionary<string, string> ExtensionByMime = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/webm"] = ".webm",
        ["audio/ogg"] = ".ogg",
        ["audio/opus"] = ".ogg",
        ["audio/mp4"] = ".m4a",
        ["audio/m4a"] = ".m4a",
        ["audio/aac"] = ".aac",
        ["audio/mpeg"] = ".mp3",
        ["audio/mp3"] = ".mp3",
        ["audio/wav"] = ".wav",
        ["audio/x-wav"] = ".wav",
        ["audio/wave"] = ".wav"
    };

    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ServiceVoicesController(ErrorServiceDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private string WebRoot => _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");

    private string VoicesRoot => Path.Combine(WebRoot, "uploads", "service-voices");

    [HttpGet]
    public async Task<ActionResult<List<ServiceVoiceActionDto>>> List(CancellationToken ct)
    {
        var items = await _db.ServiceVoiceActions.AsNoTracking()
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id)
            .Select(x => new ServiceVoiceActionDto
            {
                Id = x.Id,
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

        ServiceVoiceAction? entity = null;
        if (req.Id > 0)
        {
            entity = await _db.ServiceVoiceActions.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
            if (entity == null)
                return NotFound("اکشن یافت نشد.");
        }

        var keyTaken = await _db.ServiceVoiceActions
            .AnyAsync(x => x.Key == key && (entity == null || x.Id != entity.Id), ct);
        if (keyTaken)
            return Conflict("کلید اکشن قبلاً استفاده شده است.");

        var now = DateTimeOffset.UtcNow;
        if (entity == null)
        {
            entity = new ServiceVoiceAction { CreatedAt = now };
            _db.ServiceVoiceActions.Add(entity);
        }

        entity.Key = key;
        entity.TitleFa = title;
        entity.IsActive = req.IsActive;
        entity.RepeatCount = Math.Clamp(req.RepeatCount, 1, 50);
        entity.Priority = Math.Clamp(req.Priority, 0, 1000);
        entity.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return Ok(Map(entity));
    }

    [HttpPost("{id:int}/toggle")]
    public async Task<ActionResult<ServiceVoiceActionDto>> Toggle(int id, CancellationToken ct)
    {
        var entity = await _db.ServiceVoiceActions.FirstOrDefaultAsync(x => x.Id == id, ct);
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

        var entity = await _db.ServiceVoiceActions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null)
            return NotFound("اکشن یافت نشد.");

        var ext = ResolveExtension(file);
        if (ext == null)
            return BadRequest("فرمت صدا مجاز نیست. فرمت‌های مجاز: webm, ogg, m4a, mp4, mp3, wav, aac");

        Directory.CreateDirectory(VoicesRoot);
        var fileName = $"{SanitizeForFile(entity.Key)}-{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(VoicesRoot, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, ct);
        }

        DeleteAudioFile(entity.AudioUrl);

        entity.AudioUrl = $"/uploads/service-voices/{fileName}";
        entity.AudioSizeBytes = file.Length;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(Map(entity));
    }

    [HttpDelete("{id:int}/audio")]
    public async Task<ActionResult<ServiceVoiceActionDto>> DeleteAudio(int id, CancellationToken ct)
    {
        var entity = await _db.ServiceVoiceActions.FirstOrDefaultAsync(x => x.Id == id, ct);
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

    private static ServiceVoiceActionDto Map(ServiceVoiceAction x) => new()
    {
        Id = x.Id,
        Key = x.Key,
        TitleFa = x.TitleFa,
        IsActive = x.IsActive,
        RepeatCount = x.RepeatCount,
        Priority = x.Priority,
        AudioUrl = x.AudioUrl,
        AudioSizeBytes = x.AudioSizeBytes,
        UpdatedAt = x.UpdatedAt
    };

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
