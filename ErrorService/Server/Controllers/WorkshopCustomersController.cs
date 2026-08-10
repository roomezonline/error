using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/workshop-customers")]
public sealed class WorkshopCustomersController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public WorkshopCustomersController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [Authorize(Policy = "perm:admin.workshopcustomers.view")]
    [HttpGet("search")]
    public async Task<ActionResult<List<WorkshopCustomerDto>>> Search([FromQuery] int? workshopId = null, [FromQuery] string? q = null, [FromQuery] int skip = 0, [FromQuery] int limit = 30)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        int targetWorkshopId;

        if (isSuperAdmin)
        {
            if (!workshopId.HasValue || workshopId.Value <= 0)
                return BadRequest("انتخاب کارگاه الزامی است");
            targetWorkshopId = workshopId.Value;
        }
        else
        {
            targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
        }

        var term = (q ?? string.Empty).Trim();

        var query = _db.WorkshopCustomers
            .AsNoTracking()
            .Where(x => x.WorkshopId == targetWorkshopId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x =>
                x.FirstName.Contains(term) ||
                x.LastName.Contains(term) ||
                x.Mobile.Contains(term));
        }

        var ordered = query
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt);

        var paginated = limit > 0
            ? ordered.Skip(skip).Take(limit)
            : ordered;

        var list = await paginated
            .Select(x => new WorkshopCustomerDto
            {
                Id = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                Mobile = x.Mobile,
                Address = x.Address
            })
            .ToListAsync();

        return Ok(list);
    }

    [Authorize(Policy = "perm:admin.workshopcustomers.manage")]
    [HttpPost]
    public async Task<ActionResult<WorkshopCustomerDto>> Create([FromQuery] int? workshopId, [FromBody] WorkshopCustomerUpsertRequest request)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        int targetWorkshopId;

        if (isSuperAdmin)
        {
            if (!workshopId.HasValue || workshopId.Value <= 0)
                return BadRequest("انتخاب کارگاه الزامی است");
            targetWorkshopId = workshopId.Value;
        }
        else
        {
            targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
        }

        var mobile = (request.Mobile ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(mobile)) return BadRequest("شماره موبایل الزامی است");

        var exists = await _db.WorkshopCustomers.AnyAsync(x => x.WorkshopId == targetWorkshopId && x.Mobile == mobile);
        if (exists) return BadRequest("این شماره موبایل قبلاً برای این کارگاه ثبت شده است");

        var entity = new WorkshopCustomer
        {
            WorkshopId = targetWorkshopId,
            FirstName = (request.FirstName ?? string.Empty).Trim(),
            LastName = (request.LastName ?? string.Empty).Trim(),
            Mobile = mobile,
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.WorkshopCustomers.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new WorkshopCustomerDto
        {
            Id = entity.Id,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            Mobile = entity.Mobile,
            Address = entity.Address,
            IsBadPayer = entity.IsBadPayer
        });
    }

    [Authorize(Policy = "perm:admin.workshopcustomers.view")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkshopCustomerDto>> GetById(int id, [FromQuery] int? workshopId = null)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        int targetWorkshopId;

        if (isSuperAdmin)
        {
            if (!workshopId.HasValue || workshopId.Value <= 0)
                return BadRequest("انتخاب کارگاه الزامی است");
            targetWorkshopId = workshopId.Value;
        }
        else
        {
            targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
        }

        var entity = await _db.WorkshopCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkshopId == targetWorkshopId);

        if (entity == null) return NotFound();

        return Ok(new WorkshopCustomerDto
        {
            Id = entity.Id,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            Mobile = entity.Mobile,
            Address = entity.Address,
            IsBadPayer = entity.IsBadPayer
        });
    }

    [Authorize(Policy = "perm:admin.workshopcustomers.manage")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<WorkshopCustomerDto>> Update(int id, [FromQuery] int? workshopId, [FromBody] WorkshopCustomerUpdateRequest request)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        int targetWorkshopId;

        if (isSuperAdmin)
        {
            if (!workshopId.HasValue || workshopId.Value <= 0)
                return BadRequest("انتخاب کارگاه الزامی است");
            targetWorkshopId = workshopId.Value;
        }
        else
        {
            targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
        }

        var entity = await _db.WorkshopCustomers
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkshopId == targetWorkshopId);

        if (entity == null) return NotFound();

        var newMobile = (request.Mobile ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(newMobile)) return BadRequest("شماره موبایل الزامی است");

        // Check if new mobile is different from current mobile
        if (newMobile != entity.Mobile)
        {
            // Check if new mobile already exists in the workshop (excluding current customer)
            var exists = await _db.WorkshopCustomers.AnyAsync(x => x.WorkshopId == targetWorkshopId && x.Mobile == newMobile && x.Id != id);
            if (exists) return BadRequest("این شماره موبایل قبلاً برای این کارگاه ثبت شده است");

            entity.Mobile = newMobile;
        }

        entity.FirstName = (request.FirstName ?? string.Empty).Trim();
        entity.LastName = (request.LastName ?? string.Empty).Trim();
        entity.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new WorkshopCustomerDto
        {
            Id = entity.Id,
            WorkshopId = entity.WorkshopId,
            FirstName = entity.FirstName,
            LastName = entity.LastName,
            Mobile = entity.Mobile,
            Address = entity.Address,
            IsBadPayer = entity.IsBadPayer
        });
    }

    [Authorize(Policy = "perm:admin.workshopcustomers.manage")]
    [HttpPatch("{id:int}/bad-payer")]
    public async Task<ActionResult> UpdateBadPayerStatus(int id, [FromQuery] int? workshopId, [FromBody] UpdateBadPayerRequest request)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        int targetWorkshopId;

        if (isSuperAdmin)
        {
            if (!workshopId.HasValue || workshopId.Value <= 0)
                return BadRequest("انتخاب کارگاه الزامی است");
            targetWorkshopId = workshopId.Value;
        }
        else
        {
            targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
        }

        var entity = await _db.WorkshopCustomers
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkshopId == targetWorkshopId);

        if (entity == null) return NotFound();

        entity.IsBadPayer = request.IsBadPayer;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return Ok();
    }
}
