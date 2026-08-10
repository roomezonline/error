using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/invoice-catalog")]
public sealed class InvoiceCatalogController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public InvoiceCatalogController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [Authorize(Policy = "perm:admin.receipts.invoice")]
    [HttpGet]
    public async Task<ActionResult<List<CatalogItemDto>>> GetAll(
        [FromQuery] int? workshopId = null,
        [FromQuery] int? deviceTypeId = null,
        [FromQuery] bool includeAll = false)
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

        var query = _db.WorkshopInvoiceCatalogItems
            .AsNoTracking()
            .Where(x => x.WorkshopId == targetWorkshopId)
            .AsQueryable();

        if (!includeAll && deviceTypeId.HasValue && deviceTypeId.Value > 0)
        {
            var dtid = deviceTypeId.Value;
            query = query.Where(x => x.DeviceTypeId == null || x.DeviceTypeId == dtid);
        }

        var list = await query
            .OrderByDescending(x => x.DeviceTypeId.HasValue)
            .ThenBy(x => x.DeviceTypeId)
            .ThenBy(x => x.Title)
            .Select(x => new CatalogItemDto
            {
                Id = x.Id,
                Title = x.Title,
                IsActive = x.IsActive,
                SendSms = x.SendSms,
                DeviceTypeId = x.DeviceTypeId
            })
            .ToListAsync();

        return Ok(list);
    }

    [Authorize(Policy = "perm:admin.receipts.invoice.catalog")]
    [HttpPost]
    public async Task<ActionResult<CatalogItemDto>> Create([FromQuery] int? workshopId, CatalogItemCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("عنوان الزامی است");

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
        
        if (request.DeviceTypeId.HasValue && request.DeviceTypeId.Value > 0)
        {
            var ok = await _db.DeviceTypes.AsNoTracking().AnyAsync(x => x.Id == request.DeviceTypeId.Value);
            if (!ok) return BadRequest("نوع دستگاه معتبر نیست");
        }

        var entity = new WorkshopInvoiceCatalogItem
        {
            WorkshopId = targetWorkshopId,
            DeviceTypeId = request.DeviceTypeId,
            Title = request.Title.Trim(),
            IsActive = request.IsActive,
            SendSms = request.SendSms
        };

        _db.WorkshopInvoiceCatalogItems.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new CatalogItemDto
        {
            Id = entity.Id,
            Title = entity.Title,
            IsActive = entity.IsActive,
            SendSms = entity.SendSms,
            DeviceTypeId = entity.DeviceTypeId
        });
    }

    [Authorize(Policy = "perm:admin.receipts.invoice")]
    [HttpGet("{id:int}/check-usage")]
    public async Task<ActionResult<bool>> CheckUsage(int id, [FromQuery] int? workshopId = null)
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

        var catalogItem = await _db.WorkshopInvoiceCatalogItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkshopId == targetWorkshopId);

        if (catalogItem == null) return NotFound();

        // Check if any invoice item references this catalog item by ID
        var isUsed = await _db.CustomerReceiptBillings
            .AsNoTracking()
            .AnyAsync(b => b.Items.Any(i => i.CatalogItemId == id));

        return Ok(isUsed);
    }

    [Authorize(Policy = "perm:admin.receipts.invoice.catalog")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CatalogItemDto>> Update(int id, CatalogItemCreateRequest request, [FromQuery] int? workshopId = null)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest("عنوان الزامی است");

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
        
        if (request.DeviceTypeId.HasValue && request.DeviceTypeId.Value > 0)
        {
            var ok = await _db.DeviceTypes.AsNoTracking().AnyAsync(x => x.Id == request.DeviceTypeId.Value);
            if (!ok) return BadRequest("نوع دستگاه معتبر نیست");
        }

        var entity = await _db.WorkshopInvoiceCatalogItems
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkshopId == targetWorkshopId);

        if (entity == null) return NotFound();

        // Update only the properties, keeping the ID unchanged
        entity.Title = request.Title.Trim();
        entity.IsActive = request.IsActive;
        entity.SendSms = request.SendSms;
        entity.DeviceTypeId = request.DeviceTypeId;

        await _db.SaveChangesAsync();

        return Ok(new CatalogItemDto
        {
            Id = entity.Id,
            Title = entity.Title,
            IsActive = entity.IsActive,
            SendSms = entity.SendSms,
            DeviceTypeId = entity.DeviceTypeId
        });
    }

    [Authorize(Policy = "perm:admin.receipts.invoice.catalog")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int? workshopId = null)
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

        var entity = await _db.WorkshopInvoiceCatalogItems
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkshopId == targetWorkshopId);

        if (entity == null) return NotFound();

        // Check if item is used in any invoice by CatalogItemId
        var isUsed = await _db.CustomerReceiptBillings
            .AsNoTracking()
            .AnyAsync(b => b.Items.Any(i => i.CatalogItemId == id));

        if (isUsed)
            return BadRequest("این آیتم در فاکتوری استفاده شده و نمی‌تواند حذف شود");

        _db.WorkshopInvoiceCatalogItems.Remove(entity);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
