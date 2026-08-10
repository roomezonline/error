using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/sensor-finder")]
public sealed class SensorFinderController : ControllerBase
{
    private const int MaxImagesPerRecord = 5;

    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public SensorFinderController(ErrorServiceDbContext db, IWebHostEnvironment env, IConfiguration configuration)
    {
        _db = db;
        _env = env;
        _configuration = configuration;
    }

    private string GetSensorsRelativeFolder()
    {
        var relativeFolder = _configuration["Uploads:SensorsRelativePath"];
        if (string.IsNullOrWhiteSpace(relativeFolder)) relativeFolder = "uploads/sensors";
        return relativeFolder.Trim().TrimStart('~').TrimStart('/').TrimEnd('/');
    }

    // Public endpoints
    [HttpGet("devices")]
    [OutputCache(Duration = 3600)]
    public async Task<ActionResult<List<SensorFinderDeviceDto>>> GetDevices()
    {
        return await _db.SensorFinderDevices
            .OrderBy(x => x.Name)
            .Select(x => new SensorFinderDeviceDto { Id = x.Id, Name = x.Name })
            .ToListAsync();
    }

    [HttpGet("records")]
    [OutputCache(Duration = 3600, VaryByQueryKeys = new[] { "deviceId" })]
    public async Task<ActionResult<List<SensorFinderRecordDto>>> SearchRecords([FromQuery] int deviceId)
    {
        if (deviceId <= 0) return new List<SensorFinderRecordDto>();

        return await _db.SensorFinderRecords
            .AsNoTracking()
            .Where(x => x.DeviceId == deviceId)
            .Include(x => x.Device)
            .Include(x => x.Images)
            .OrderBy(x => x.SensorName)
            .Select(x => new SensorFinderRecordDto
            {
                Id = x.Id,
                DeviceId = x.DeviceId,
                DeviceName = x.Device.Name,
                SensorName = x.SensorName,
                SensorType = x.SensorType,
                SizeText = x.SizeText,
                Notes = x.Notes,
                Images = x.Images
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.Id)
                    .Select(i => new SensorFinderImageDto { Id = i.Id, Url = i.Url, Title = i.Title, SortOrder = i.SortOrder })
                    .ToList()
            })
            .ToListAsync();
    }

    [HttpGet("records/recent")]
    [OutputCache(Duration = 600)]
    public async Task<ActionResult<IEnumerable<object>>> GetRecentRecords([FromQuery] int take = 3)
    {
        return await _db.SensorFinderRecords
            .AsNoTracking()
            .Include(x => x.Device)
            .Include(x => x.Images)
            .OrderByDescending(x => x.Id)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                DeviceName = x.Device.Name,
                x.SensorName,
                x.SensorType,
                x.SizeText,
                ImageUrl = x.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
            })
            .ToListAsync();
    }

    [HttpGet("records/count")]
    [OutputCache(Duration = 600)]
    public async Task<ActionResult<int>> GetRecordCount()
    {
        return await _db.SensorFinderRecords.CountAsync();
    }

    [HttpGet("records/{id:int}")]
    [OutputCache(Duration = 3600)]
    public async Task<ActionResult<SensorFinderRecordDto>> GetRecordById(int id)
    {
        var x = await _db.SensorFinderRecords
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Include(r => r.Device)
            .Include(r => r.Images)
            .Select(r => new SensorFinderRecordDto
            {
                Id = r.Id,
                DeviceId = r.DeviceId,
                DeviceName = r.Device.Name,
                SensorName = r.SensorName,
                SensorType = r.SensorType,
                SizeText = r.SizeText,
                Notes = r.Notes,
                Images = r.Images
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.Id)
                    .Select(i => new SensorFinderImageDto { Id = i.Id, Url = i.Url, Title = i.Title, SortOrder = i.SortOrder })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (x == null) return NotFound();
        return Ok(x);
    }

    [HttpGet("records/by-device/{deviceId:int}")]
    [OutputCache(Duration = 3600)]
    public async Task<ActionResult<List<SensorFinderRecordDto>>> GetRecordsByDevice(int deviceId)
    {
        return await _db.SensorFinderRecords
            .AsNoTracking()
            .Where(x => x.DeviceId == deviceId)
            .Include(x => x.Device)
            .Include(x => x.Images)
            .OrderBy(x => x.SensorName)
            .Select(x => new SensorFinderRecordDto
            {
                Id = x.Id,
                DeviceId = x.DeviceId,
                DeviceName = x.Device.Name,
                SensorName = x.SensorName,
                SensorType = x.SensorType,
                SizeText = x.SizeText,
                Notes = x.Notes,
                Images = x.Images
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.Id)
                    .Select(i => new SensorFinderImageDto { Id = i.Id, Url = i.Url, Title = i.Title, SortOrder = i.SortOrder })
                    .ToList()
            })
            .ToListAsync();
    }

    // Admin endpoints (no auth applied in project patterns)
    [Authorize(Policy = "perm:admin.technical.sensorfinder.manage")]
    [HttpGet("admin/devices")]
    public async Task<ActionResult<List<SensorFinderDeviceDto>>> AdminGetDevices()
    {
        return await GetDevices();
    }

    [Authorize(Policy = "perm:admin.technical.sensorfinder.manage")]
    [HttpPost("admin/devices")]
    public async Task<ActionResult<SensorFinderDeviceDto>> AdminCreateDevice(SensorFinderDeviceUpsertRequest request)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("نام دستگاه الزامی است");

        var exists = await _db.SensorFinderDevices.AnyAsync(x => x.Name == name);
        if (exists) return BadRequest("این دستگاه قبلاً ثبت شده است");

        var entity = new SensorFinderDevice { Name = name };
        _db.SensorFinderDevices.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new SensorFinderDeviceDto { Id = entity.Id, Name = entity.Name });
    }

    [Authorize(Policy = "perm:admin.technical.sensorfinder.manage")]
    [HttpPut("admin/devices/{id:int}")]
    public async Task<IActionResult> AdminUpdateDevice(int id, SensorFinderDeviceUpsertRequest request)
    {
        var entity = await _db.SensorFinderDevices.FindAsync(id);
        if (entity == null) return NotFound();

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("نام دستگاه الزامی است");

        var exists = await _db.SensorFinderDevices.AnyAsync(x => x.Id != id && x.Name == name);
        if (exists) return BadRequest("این نام دستگاه تکراری است");

        entity.Name = name;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.technical.sensorfinder.manage")]
    [HttpDelete("admin/devices/{id:int}")]
    public async Task<IActionResult> AdminDeleteDevice(int id)
    {
        var entity = await _db.SensorFinderDevices
            .Include(x => x.Records)
            .ThenInclude(r => r.Images)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null) return NotFound();

        if (entity.Records.Any())
        {
            return BadRequest("این دستگاه دارای رکورد سنسور است و قابل حذف نیست");
        }

        _db.SensorFinderDevices.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.technical.sensorfinder.manage")]
    [HttpGet("admin/records")]
    public async Task<ActionResult<List<SensorFinderRecordDto>>> AdminGetRecords([FromQuery] int? deviceId = null)
    {
        var query = _db.SensorFinderRecords.AsNoTracking().AsQueryable();
        if (deviceId.HasValue && deviceId.Value > 0)
        {
            query = query.Where(x => x.DeviceId == deviceId.Value);
        }

        return await query
            .Include(x => x.Device)
            .Include(x => x.Images)
            .OrderByDescending(x => x.Id)
            .Select(x => new SensorFinderRecordDto
            {
                Id = x.Id,
                DeviceId = x.DeviceId,
                DeviceName = x.Device.Name,
                SensorName = x.SensorName,
                SensorType = x.SensorType,
                SizeText = x.SizeText,
                Notes = x.Notes,
                Images = x.Images
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.Id)
                    .Select(i => new SensorFinderImageDto { Id = i.Id, Url = i.Url, Title = i.Title, SortOrder = i.SortOrder })
                    .ToList()
            })
            .ToListAsync();
    }

    [Authorize(Policy = "perm:admin.technical.sensorfinder.manage")]
    [HttpPost("admin/records")]
    public async Task<ActionResult<SensorFinderRecordDto>> AdminCreateRecord(SensorFinderRecordUpsertRequest request)
    {
        if (!await _db.SensorFinderDevices.AnyAsync(x => x.Id == request.DeviceId))
            return BadRequest("دستگاه نامعتبر است");

        var entity = new SensorFinderRecord
        {
            DeviceId = request.DeviceId,
            SensorName = (request.SensorName ?? string.Empty).Trim(),
            SensorType = request.SensorType?.Trim(),
            SizeText = request.SizeText?.Trim(),
            Notes = request.Notes?.Trim()
        };

        if (string.IsNullOrWhiteSpace(entity.SensorName))
            return BadRequest("نام سنسور الزامی است");

        _db.SensorFinderRecords.Add(entity);
        await _db.SaveChangesAsync();

        var dto = await _db.SensorFinderRecords
            .AsNoTracking()
            .Where(x => x.Id == entity.Id)
            .Include(x => x.Device)
            .Select(x => new SensorFinderRecordDto
            {
                Id = x.Id,
                DeviceId = x.DeviceId,
                DeviceName = x.Device.Name,
                SensorName = x.SensorName,
                SensorType = x.SensorType,
                SizeText = x.SizeText,
                Notes = x.Notes,
                Images = new List<SensorFinderImageDto>()
            })
            .FirstAsync();

        return Ok(dto);
    }

    [Authorize(Policy = "perm:admin.technical.sensorfinder.manage")]
    [HttpPut("admin/records/{id:int}")]
    public async Task<IActionResult> AdminUpdateRecord(int id, SensorFinderRecordUpsertRequest request)
    {
        var entity = await _db.SensorFinderRecords.FindAsync(id);
        if (entity == null) return NotFound();

        if (!await _db.SensorFinderDevices.AnyAsync(x => x.Id == request.DeviceId))
            return BadRequest("دستگاه نامعتبر است");

        entity.DeviceId = request.DeviceId;
        entity.SensorName = (request.SensorName ?? string.Empty).Trim();
        entity.SensorType = request.SensorType?.Trim();
        entity.SizeText = request.SizeText?.Trim();
        entity.Notes = request.Notes?.Trim();

        if (string.IsNullOrWhiteSpace(entity.SensorName))
            return BadRequest("نام سنسور الزامی است");

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.technical.sensorfinder.manage")]
    [HttpDelete("admin/records/{id:int}")]
    public async Task<IActionResult> AdminDeleteRecord(int id)
    {
        var entity = await _db.SensorFinderRecords
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null) return NotFound();

        _db.SensorFinderRecords.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.technical.sensorfinder.manage")]
    [HttpPost("admin/records/{recordId:int}/upload")]
    public async Task<ActionResult<SensorFinderImageDto>> AdminUploadImage(int recordId, [FromForm] IFormFile file, [FromForm] string? title)
    {
        var record = await _db.SensorFinderRecords
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == recordId);

        if (record == null) return NotFound("رکورد سنسور یافت نشد");

        if (record.Images.Count >= MaxImagesPerRecord)
            return BadRequest($"حداکثر {MaxImagesPerRecord} عکس مجاز است");

        if (file == null || file.Length == 0) return BadRequest("فایل ارسال نشده است");

        const long maxBytes = 5 * 1024 * 1024;
        if (file.Length > maxBytes) return BadRequest("حجم فایل زیاد است (حداکثر 5MB)");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var relativeFolder = GetSensorsRelativeFolder();
        var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var uploadsRoot = Path.Combine(wwwroot, relativeFolder);
        if (!Directory.Exists(uploadsRoot)) Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{recordId}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using (var outStream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(outStream);
        }

        var maxSort = record.Images.Any() ? record.Images.Max(x => x.SortOrder) : 0;
        var img = new SensorFinderImage
        {
            RecordId = recordId,
            Url = $"/{relativeFolder}/{fileName}",
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            SortOrder = maxSort + 1
        };

        _db.SensorFinderImages.Add(img);
        await _db.SaveChangesAsync();

        return Ok(new SensorFinderImageDto { Id = img.Id, Url = img.Url, Title = img.Title, SortOrder = img.SortOrder });
    }

    [Authorize(Policy = "perm:admin.technical.sensorfinder.manage")]
    [HttpDelete("admin/images/{id:int}")]
    public async Task<IActionResult> AdminDeleteImage(int id)
    {
        var entity = await _db.SensorFinderImages.FindAsync(id);
        if (entity == null) return NotFound();

        _db.SensorFinderImages.Remove(entity);
        await _db.SaveChangesAsync();

        // best-effort delete file
        try
        {
            if (!string.IsNullOrWhiteSpace(entity.Url))
            {
                var relativeFolder = GetSensorsRelativeFolder();
                var expectedPrefix = "/" + relativeFolder.TrimEnd('/') + "/";
                if (entity.Url.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var relative = entity.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                    var fullPath = Path.Combine(wwwroot, relative);
                    if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                }
            }
        }
        catch
        {
            // ignore
        }

        return NoContent();
    }
}
