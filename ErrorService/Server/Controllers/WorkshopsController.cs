using System.IO;
using ErrorService.Server.Data;
using ErrorService.Server.Infrastructure;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LocationsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public LocationsController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet("provinces")]
    public async Task<ActionResult<List<ProvinceDto>>> GetProvinces()
    {
        return await _db.Provinces
            .OrderBy(x => x.Name)
            .Select(x => new ProvinceDto { Id = x.Id, Name = x.Name })
            .ToListAsync();
    }

    [HttpGet("cities/{provinceId:int}")]
    public async Task<ActionResult<List<CityDto>>> GetCities(int provinceId)
    {
        return await _db.Cities
            .Where(x => x.ProvinceId == provinceId)
            .OrderBy(x => x.Name)
            .Select(x => new CityDto { Id = x.Id, Name = x.Name, ProvinceId = x.ProvinceId })
            .ToListAsync();
    }
}

[ApiController]
[Route("api/[controller]")]
public sealed class WorkshopsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public WorkshopsController(ErrorServiceDbContext db, IWebHostEnvironment env, IConfiguration configuration)
    {
        _db = db;
        _env = env;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkshopDto>>> Get([FromQuery] bool includeDeleted = false)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        int? workshopIdFilter = null;

        if (!isSuperAdmin)
        {
            var wsIdClaim = User.FindFirst("workshop_id")?.Value;
            if (int.TryParse(wsIdClaim, out var wsId))
                workshopIdFilter = wsId;
            else
                return Unauthorized("دسترسی به کارگاه نامشخص است");
        }

        var query = _db.Workshops
            .Include(x => x.Province)
            .Include(x => x.City)
            .AsQueryable();

        // کارگاه‌های حذف‌نرم‌شده به‌صورت پیش‌فرض پنهان‌اند؛ فقط سوپرادمین با includeDeleted می‌تواند ببیند.
        if (!(isSuperAdmin && includeDeleted))
            query = query.Where(x => !x.IsDeleted);

        if (workshopIdFilter.HasValue)
            query = query.Where(x => x.Id == workshopIdFilter.Value);

        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new WorkshopDto
            {
                Id = x.Id,
                WorkshopName = x.WorkshopName,
                OwnerFullName = x.OwnerFullName,
                MobileNumber = x.MobileNumber,
                ProvinceId = x.ProvinceId,
                ProvinceName = x.Province.Name,
                CityId = x.CityId,
                CityName = x.City.Name,
                Address = x.Address,
                IsActive = x.IsActive,
                IsDeleted = x.IsDeleted,
                DeletedAt = x.DeletedAt,
                ImageUrl = x.ImageUrl,
                ShowInCustomersRow = x.ShowInCustomersRow,
                CreatedAt = x.CreatedAt,
                MaxMonitoringConnections = x.MaxMonitoringConnections,
                MonitoringDurationHours = x.MonitoringDurationHours
            })
            .ToListAsync();

        await AttachAssignedDevicesAsync(list);

        return Ok(list);
    }

    /// <summary>دستگاه‌های مانیتورینگ فعالِ تعلق‌گرفته به هر کارگاه را به لیست اضافه می‌کند.</summary>
    private async Task AttachAssignedDevicesAsync(List<WorkshopDto> list)
    {
        if (list.Count == 0) return;

        var ids = list.Select(x => x.Id).ToList();
        var now = DateTimeOffset.UtcNow;

        var devices = await _db.MonitoringDeviceAssignments
            .AsNoTracking()
            .Where(a => ids.Contains(a.WorkshopId) && a.StartAt <= now && (a.EndAt == null || a.EndAt > now))
            .OrderByDescending(a => a.StartAt)
            .Select(a => new { a.WorkshopId, a.MonitoringDeviceId, a.MonitoringDevice.DeviceNumber, a.MonitoringDevice.Title })
            .ToListAsync();

        foreach (var w in list)
        {
            w.Devices = devices
                .Where(d => d.WorkshopId == w.Id)
                .Select(d => new WorkshopDeviceInfo
                {
                    DeviceId = d.MonitoringDeviceId,
                    DeviceNumber = d.DeviceNumber,
                    Title = d.Title
                })
                .ToList();
        }
    }

    [HttpGet("customers-row")]
    public async Task<ActionResult<List<WorkshopDto>>> GetCustomersRow()
    {
        return await _db.Workshops
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted && x.ShowInCustomersRow)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new WorkshopDto
            {
                Id = x.Id,
                WorkshopName = x.WorkshopName,
                OwnerFullName = x.OwnerFullName,
                MobileNumber = x.MobileNumber,
                ProvinceId = x.ProvinceId,
                ProvinceName = x.Province.Name,
                CityId = x.CityId,
                CityName = x.City.Name,
                Address = x.Address,
                IsActive = x.IsActive,
                IsDeleted = x.IsDeleted,
                DeletedAt = x.DeletedAt,
                ImageUrl = x.ImageUrl,
                ShowInCustomersRow = x.ShowInCustomersRow,
                CreatedAt = x.CreatedAt,
                MaxMonitoringConnections = x.MaxMonitoringConnections,
                MonitoringDurationHours = x.MonitoringDurationHours
            })
            .ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkshopDto>> GetById(int id)
    {
        var x = await _db.Workshops
            .Include(x => x.Province)
            .Include(x => x.City)
            .FirstOrDefaultAsync(w => w.Id == id);

        if (x == null) return NotFound();

        var dto = new WorkshopDto
        {
            Id = x.Id,
            WorkshopName = x.WorkshopName,
            OwnerFullName = x.OwnerFullName,
            MobileNumber = x.MobileNumber,
            ProvinceId = x.ProvinceId,
            ProvinceName = x.Province.Name,
            CityId = x.CityId,
            CityName = x.City.Name,
            Address = x.Address,
            IsActive = x.IsActive,
            IsDeleted = x.IsDeleted,
            DeletedAt = x.DeletedAt,
            ImageUrl = x.ImageUrl,
            ShowInCustomersRow = x.ShowInCustomersRow,
            CreatedAt = x.CreatedAt,
            MaxMonitoringConnections = x.MaxMonitoringConnections,
            MonitoringDurationHours = x.MonitoringDurationHours
        };

        var now = DateTimeOffset.UtcNow;
        dto.Devices = await _db.MonitoringDeviceAssignments
            .AsNoTracking()
            .Where(a => a.WorkshopId == id && a.StartAt <= now && (a.EndAt == null || a.EndAt > now))
            .OrderByDescending(a => a.StartAt)
            .Select(a => new WorkshopDeviceInfo
            {
                DeviceId = a.MonitoringDeviceId,
                DeviceNumber = a.MonitoringDevice.DeviceNumber,
                Title = a.MonitoringDevice.Title
            })
            .ToListAsync();

        return dto;
    }

    [Authorize(Policy = "perm:admin.workshops.manage")]
    [HttpPost]
    public async Task<ActionResult<WorkshopDto>> Create(WorkshopUpsertRequest req)
    {
        var entity = new Workshop
        {
            WorkshopName = req.WorkshopName.Trim(),
            OwnerFullName = req.OwnerFullName.Trim(),
            MobileNumber = req.MobileNumber.Trim(),
            ProvinceId = req.ProvinceId,
            CityId = req.CityId,
            Address = req.Address.Trim(),
            IsActive = req.IsActive,
            ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl.Trim(),
            ShowInCustomersRow = req.ShowInCustomersRow,
            MaxMonitoringConnections = req.MaxMonitoringConnections,
            MonitoringDurationHours = req.MonitoringDurationHours,
            CreatedAt = DateTime.Now
        };

        _db.Workshops.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new WorkshopDto { Id = entity.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, WorkshopUpsertRequest req)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        var hasEditOwn = User.HasClaim("perm", "admin.workshops.editown");
        var workshopIdClaim = User.FindFirst("workshop_id")?.Value;

        if (!isSuperAdmin)
        {
            if (!hasEditOwn)
                return Forbid("شما مجوز ویرایش کارگاه را ندارید");

            if (!int.TryParse(workshopIdClaim, out var userWsId) || userWsId != id)
                return Forbid("شما فقط می‌توانید کارگاه خود را ویرایش کنید");
        }

        var entity = await _db.Workshops.FindAsync(id);
        if (entity == null) return NotFound();

        var wasActive = entity.IsActive;
        entity.WorkshopName = req.WorkshopName.Trim();
        entity.OwnerFullName = req.OwnerFullName.Trim();
        entity.MobileNumber = req.MobileNumber.Trim();
        entity.ProvinceId = req.ProvinceId;
        entity.CityId = req.CityId;
        entity.Address = req.Address.Trim();
        entity.IsActive = req.IsActive;
        FileCleanupHelper.DeleteOldFileIfChanged(entity.ImageUrl, req.ImageUrl?.Trim(), _env);
        entity.ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl.Trim();
        entity.ShowInCustomersRow = req.ShowInCustomersRow;
        entity.MaxMonitoringConnections = req.MaxMonitoringConnections;
        entity.MonitoringDurationHours = req.MonitoringDurationHours;

        await _db.SaveChangesAsync();

        // با غیرفعال شدن کارگاه، اتصال‌های مانیتورینگ باز آن بسته می‌شوند تا دستگاه‌ها آزاد شوند.
        if (wasActive && !entity.IsActive)
            await CloseWorkshopOpenConnectionsAsync(entity.Id);

        return NoContent();
    }

    [HttpPost("upload")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<string>> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        var hasEditOwn = User.HasClaim("perm", "admin.workshops.editown");
        if (!isSuperAdmin && !hasEditOwn)
            return Forbid();

        if (file == null || file.Length <= 0)
            return BadRequest("فایل خالی است یا دریافت نشد");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".bin";

        var safeExt = ext.ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
        if (!allowed.Contains(safeExt))
            return BadRequest("فرمت فایل مجاز نیست. فقط jpg, jpeg, png, webp, svg");

        var relativeFolder = _configuration["Uploads:WorkshopsRelativePath"];
        if (string.IsNullOrWhiteSpace(relativeFolder))
            relativeFolder = "uploads/workshops";

        relativeFolder = relativeFolder.Trim().TrimStart('~').TrimStart('/').TrimEnd('/');

        var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        if (!Directory.Exists(wwwroot))
            Directory.CreateDirectory(wwwroot);

        var uploadsRoot = Path.Combine(wwwroot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{safeExt}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using (var outStream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(outStream, cancellationToken);
        }

        var publicUrl = "/" + relativeFolder.Replace("\\", "/") + "/" + fileName;
        return Ok(publicUrl);
    }

    [Authorize(Policy = "perm:admin.workshops.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Workshops.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();
        if (entity.IsDeleted) return NoContent();

        var now = DateTimeOffset.UtcNow;

        // حذف نرم: کارگاه پنهان می‌شود اما رکورد و تمام متعلقاتش (رسید، مشتری، اتصال، پرسنل و…) حفظ می‌گردد
        // تا در صورت اشتباه قابل بازگردانی باشد و ارجاع‌های دیتابیس یتیم نشوند.
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.IsActive = false;

        await _db.SaveChangesAsync();

        // پرسنل این کارگاه هم پنهان می‌شوند (هم‌زمان با کارگاه قابل بازگردانی‌اند).
        await SoftDeleteWorkshopUsersAsync(entity.Id, now);

        // اتصال‌های مانیتورینگ باز بسته می‌شوند تا دستگاه‌ها آزاد شوند.
        await CloseWorkshopOpenConnectionsAsync(entity.Id);

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.workshops.manage")]
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var entity = await _db.Workshops.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        entity.IsDeleted = false;
        entity.DeletedAt = null;
        entity.IsActive = true;

        await _db.SaveChangesAsync();

        // پرسنلی که همراه همین کارگاه حذف شده بود، بازگردانده می‌شود.
        var users = await _db.WorkshopUsers
            .Where(u => u.WorkshopId == id && u.IsDeleted)
            .ToListAsync();
        foreach (var u in users)
        {
            u.IsDeleted = false;
            u.DeletedAt = null;
        }
        if (users.Count > 0)
            await _db.SaveChangesAsync();

        return Ok(new { restored = true });
    }

    [Authorize(Policy = "perm:admin.workshops.manage")]
    [HttpPatch("{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var entity = await _db.Workshops.FindAsync(id);
        if (entity == null) return NotFound();

        entity.IsActive = !entity.IsActive;
        await _db.SaveChangesAsync();

        if (!entity.IsActive)
            await CloseWorkshopOpenConnectionsAsync(entity.Id);

        return Ok(entity.IsActive);
    }

    private async Task SoftDeleteWorkshopUsersAsync(int workshopId, DateTimeOffset now)
    {
        var users = await _db.WorkshopUsers
            .Where(u => u.WorkshopId == workshopId && !u.IsDeleted)
            .ToListAsync();
        foreach (var u in users)
        {
            u.IsDeleted = true;
            u.DeletedAt = now;
            u.IsActive = false;
        }
        if (users.Count > 0)
            await _db.SaveChangesAsync();
    }

    /// <summary>با غیرفعال شدن کارگاه، اتصال‌های مانیتورینگ باز آن بسته می‌شوند تا دستگاه‌ها آزاد شوند.</summary>
    private async Task CloseWorkshopOpenConnectionsAsync(int workshopId)
    {
        var open = await _db.MonitoringReceiptConnections
            .Where(c => c.WorkshopId == workshopId && c.EndedAt == null)
            .ToListAsync();
        if (open.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var c in open)
        {
            c.EndedAt = now;
            c.EndReason = "پایان خودکار: کارگاه غیرفعال/حذف شد.";
        }
        await _db.SaveChangesAsync();
    }
}
