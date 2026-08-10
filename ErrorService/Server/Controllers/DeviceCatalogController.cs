using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/device-types")]
[Authorize(Policy = "perm:admin.technical.devices.view")]
public sealed class DeviceTypesController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public DeviceTypesController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [AllowAnonymous]
    [OutputCache(Duration = 1800, VaryByQueryKeys = new[] { "*" })]
    public async Task<ActionResult<List<DeviceTypeDto>>> Get([FromQuery] string? q = null)
    {
        var query = _db.DeviceTypes
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x => x.Name.Contains(term));
        }

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new DeviceTypeDto
            {
                Id = x.Id,
                Name = x.Name,
                SortOrder = x.SortOrder
            })
            .ToListAsync();
    }

    [HttpPost]
    [Authorize(Policy = "perm:admin.technical.devices.manage")]
    public async Task<ActionResult<DeviceTypeDto>> Create(DeviceTypeDto dto)
    {
        var entity = new DeviceType
        {
            Name = dto.Name?.Trim() ?? string.Empty,
            SortOrder = dto.SortOrder
        };

        _db.DeviceTypes.Add(entity);
        await _db.SaveChangesAsync();

        dto.Id = entity.Id;
        dto.Name = entity.Name;
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "perm:admin.technical.devices.manage")]
    public async Task<IActionResult> Update(int id, DeviceTypeDto dto)
    {
        if (id != dto.Id) return BadRequest("شناسه نوع دستگاه مطابقت ندارد");

        var entity = await _db.DeviceTypes.FindAsync(id);
        if (entity == null) return NotFound("نوع دستگاه یافت نشد");

        entity.Name = dto.Name?.Trim() ?? string.Empty;
        entity.SortOrder = dto.SortOrder;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "perm:admin.technical.devices.manage")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.DeviceTypes
            .Include(x => x.Brands)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null) return NotFound("نوع دستگاه یافت نشد");

        if (entity.Brands.Any())
        {
            return BadRequest("این نوع دستگاه دارای برند است و نمی‌توان آن را حذف کرد. ابتدا برندها را حذف کنید.");
        }

        _db.DeviceTypes.Remove(entity);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}

[ApiController]
[Route("api/device-brands")]
[Authorize(Policy = "perm:admin.technical.brands.view")]
public sealed class DeviceBrandsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public DeviceBrandsController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [AllowAnonymous]
    [OutputCache(Duration = 1800, VaryByQueryKeys = new[] { "*" })]
    public async Task<ActionResult<List<DeviceBrandDto>>> Get(
        [FromQuery] int? deviceTypeId = null,
        [FromQuery] string? q = null)
    {
        var query = _db.DeviceBrands
            .AsNoTracking()
            .AsQueryable();

        if (deviceTypeId.HasValue && deviceTypeId.Value > 0)
        {
            query = query.Where(x => x.DeviceTypeId == deviceTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x => x.Name.Contains(term));
        }

        return await query
            .Include(x => x.DeviceType)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new DeviceBrandDto
            {
                Id = x.Id,
                Name = x.Name,
                DeviceTypeId = x.DeviceTypeId,
                DeviceTypeName = x.DeviceType.Name,
                SortOrder = x.SortOrder
            })
            .ToListAsync();
    }

    [HttpPost]
    [Authorize(Policy = "perm:admin.technical.brands.manage")]
    public async Task<ActionResult<DeviceBrandDto>> Create(DeviceBrandUpsertRequest req)
    {
        var deviceType = await _db.DeviceTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.DeviceTypeId);
        if (deviceType == null) return BadRequest("نوع دستگاه معتبر نیست");

        var entity = new DeviceBrand
        {
            Name = req.Name?.Trim() ?? string.Empty,
            DeviceTypeId = req.DeviceTypeId,
            SortOrder = req.SortOrder
        };

        _db.DeviceBrands.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new DeviceBrandDto
        {
            Id = entity.Id,
            Name = entity.Name,
            DeviceTypeId = entity.DeviceTypeId,
            DeviceTypeName = deviceType.Name,
            SortOrder = entity.SortOrder
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "perm:admin.technical.brands.manage")]
    public async Task<IActionResult> Update(int id, DeviceBrandUpsertRequest req)
    {
        var entity = await _db.DeviceBrands.FindAsync(id);
        if (entity == null) return NotFound("برند یافت نشد");

        var deviceType = await _db.DeviceTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.DeviceTypeId);
        if (deviceType == null) return BadRequest("نوع دستگاه معتبر نیست");

        entity.Name = req.Name?.Trim() ?? string.Empty;
        entity.DeviceTypeId = req.DeviceTypeId;
        entity.SortOrder = req.SortOrder;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "perm:admin.technical.brands.manage")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.DeviceBrands.FindAsync(id);
        if (entity == null) return NotFound("برند یافت نشد");

        _db.DeviceBrands.Remove(entity);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
