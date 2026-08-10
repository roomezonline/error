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
    public async Task<ActionResult<List<WorkshopDto>>> Get()
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

        if (workshopIdFilter.HasValue)
            query = query.Where(x => x.Id == workshopIdFilter.Value);

        return await query
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
                ImageUrl = x.ImageUrl,
                ShowInCustomersRow = x.ShowInCustomersRow,
                CreatedAt = x.CreatedAt,
                MaxMonitoringConnections = x.MaxMonitoringConnections
            })
            .ToListAsync();
    }

    [HttpGet("customers-row")]
    public async Task<ActionResult<List<WorkshopDto>>> GetCustomersRow()
    {
        return await _db.Workshops
            .AsNoTracking()
            .Where(x => x.IsActive && x.ShowInCustomersRow)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new WorkshopDto
            {
                Id = x.Id,
                WorkshopName = x.WorkshopName,
                OwnerFullName = x.OwnerFullName,
                MobileNumber = x.MobileNumber,
                ProvinceId = x.ProvinceId,
                CityId = x.CityId,
                Address = x.Address,
                IsActive = x.IsActive,
                ImageUrl = x.ImageUrl,
                ShowInCustomersRow = x.ShowInCustomersRow,
                CreatedAt = x.CreatedAt,
                MaxMonitoringConnections = x.MaxMonitoringConnections
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

        return new WorkshopDto
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
            ImageUrl = x.ImageUrl,
            ShowInCustomersRow = x.ShowInCustomersRow,
            CreatedAt = x.CreatedAt,
            MaxMonitoringConnections = x.MaxMonitoringConnections
        };
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

        await _db.SaveChangesAsync();
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
        var entity = await _db.Workshops.FindAsync(id);
        if (entity == null) return NotFound();

        _db.Workshops.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.workshops.manage")]
    [HttpPatch("{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var entity = await _db.Workshops.FindAsync(id);
        if (entity == null) return NotFound();

        entity.IsActive = !entity.IsActive;
        await _db.SaveChangesAsync();
        return Ok(entity.IsActive);
    }
}
