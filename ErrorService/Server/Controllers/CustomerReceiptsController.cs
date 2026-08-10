using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/customer-receipts")]
public sealed class CustomerReceiptsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ISmsService _smsService;

    public CustomerReceiptsController(ErrorServiceDbContext db, IWebHostEnvironment env, IConfiguration configuration, ISmsService smsService)
    {
        _db = db;
        _env = env;
        _configuration = configuration;
        _smsService = smsService;
    }

    [Authorize(Policy = "perm:admin.receipts.view")]
    [HttpGet]
    public async Task<ActionResult<List<CustomerReceiptDto>>> GetAll([FromQuery] int? workshopId = null)
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

        var list = await _db.CustomerReceipts
            .AsNoTracking()
            .Where(x => x.WorkshopId == targetWorkshopId)
            .OrderByDescending(x => x.RegisteredAt)
            .Take(200)
            .Select(x => new CustomerReceiptDto
            {
                Id = x.Id,
                WorkshopId = x.WorkshopId,
                WorkshopName = x.Workshop.WorkshopName,
                CustomerId = x.CustomerId,
                CustomerFirstName = x.Customer.FirstName,
                CustomerLastName = x.Customer.LastName,
                CustomerMobile = x.Customer.Mobile,
                CustomerAddress = x.Customer.Address,
                CustomerIsBadPayer = x.Customer.IsBadPayer,
                DeviceTypeId = x.DeviceTypeId,
                DeviceTypeName = x.DeviceType.Name,
                DeviceBrandId = x.DeviceBrandId,
                DeviceBrandName = x.DeviceBrand != null ? x.DeviceBrand.Name : null,
                ProblemDescription = x.ProblemDescription,
                ReceiptImageUrl = x.ReceiptImageUrl,
                RegisteredAt = x.RegisteredAt,
                Status = x.Status,
                TechnicianId = x.TechnicianId,
                TechnicianName = x.Technician != null ? x.Technician.FullName : null,
                CreatedByUserId = x.CreatedByUserId,
                CreatedByUserName = x.CreatedByUser != null ? x.CreatedByUser.FullName : null,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                CommitmentStatus = x.DigitalCommitment != null ? x.DigitalCommitment.Status : null,
                HasActiveMonitoring = x.MonitoringReceiptConnections.Any(m => m.EndedAt == null),
                MonitoringHistoryCount = x.MonitoringReceiptConnections.Count
            })
            .ToListAsync();

        return Ok(list);
    }

    [Authorize(Policy = "perm:admin.receipts.view")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerReceiptDto>> GetById(int id)
    {
        var x = await _db.CustomerReceipts
            .AsNoTracking()
            .Include(x => x.Workshop)
            .Include(x => x.Customer)
            .Include(x => x.DeviceType)
            .Include(x => x.DeviceBrand)
            .Include(x => x.Technician)
            .Include(x => x.CreatedByUser)
            .Include(x => x.DigitalCommitment)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (x == null) return NotFound();

        var isSuperAdmin = User.IsInRole("super_admin");
        if (!isSuperAdmin)
        {
            var targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (x.WorkshopId != targetWorkshopId)
                return Forbid();
        }

        return Ok(new CustomerReceiptDto
        {
            Id = x.Id,
                WorkshopId = x.WorkshopId,
                WorkshopName = x.Workshop.WorkshopName,
                CustomerId = x.CustomerId,
            CustomerFirstName = x.Customer.FirstName,
            CustomerLastName = x.Customer.LastName,
            CustomerMobile = x.Customer.Mobile,
            CustomerAddress = x.Customer.Address,
            CustomerIsBadPayer = x.Customer.IsBadPayer,
            DeviceTypeId = x.DeviceTypeId,
            DeviceTypeName = x.DeviceType.Name,
            DeviceBrandId = x.DeviceBrandId,
            DeviceBrandName = x.DeviceBrand != null ? x.DeviceBrand.Name : null,
            ProblemDescription = x.ProblemDescription,
            ReceiptImageUrl = x.ReceiptImageUrl,
            RegisteredAt = x.RegisteredAt,
            Status = x.Status,
            TechnicianId = x.TechnicianId,
            TechnicianName = x.Technician != null ? x.Technician.FullName : null,
            CreatedByUserId = x.CreatedByUserId,
            CreatedByUserName = x.CreatedByUser != null ? x.CreatedByUser.FullName : null,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            CommitmentStatus = x.DigitalCommitment != null ? x.DigitalCommitment.Status : null
        });
    }

    [Authorize(Policy = "perm:admin.receipts.manage")]
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromQuery] int? workshopId, ReceiptStatusUpdateRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Status))
            return BadRequest("وضعیت الزامی است");

        var normalizedStatus = request.Status.Trim();
        if (!CustomerReceiptStatuses.All.Contains(normalizedStatus))
            return BadRequest("وضعیت معتبر نیست");

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

        var entity = await _db.CustomerReceipts
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkshopId == targetWorkshopId);

        if (entity == null) return NotFound();

        entity.Status = normalizedStatus;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.receipts.invoice")]
    [HttpGet("{id:int}/billing")]
    public async Task<ActionResult<CustomerReceiptBillingDto>> GetBilling(int id)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        if (!isSuperAdmin)
        {
            var targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
            var receiptOk = await _db.CustomerReceipts.AsNoTracking().AnyAsync(x => x.Id == id && x.WorkshopId == targetWorkshopId);
            if (!receiptOk) return NotFound();
        }

        var billing = await _db.CustomerReceiptBillings
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.CustomerReceiptId == id)
                .Select(x => new CustomerReceiptBillingDto
                {
                    CustomerReceiptId = x.CustomerReceiptId,
                    BillTotal = x.BillTotal,
                    Discount = x.Discount,
                    Prepaid = x.Prepaid,
                    Paid = x.Paid,
                    SelectedBankAccountId = x.SelectedBankAccountId,
                    UpdatedAt = x.UpdatedAt,
                    Items = x.Items.Select(i => new InvoiceItemDto
                    {
                        Title = i.Title,
                        ActionDescription = i.ActionDescription,
                        UnitPrice = i.UnitPrice,
                        Quantity = i.Quantity,
                        TechnicianId = i.TechnicianId
                    }).ToList()
                })
                .FirstOrDefaultAsync();

        if (billing == null)
        {
            return Ok(new CustomerReceiptBillingDto
            {
                CustomerReceiptId = id,
                BillTotal = 0,
                Discount = 0,
                Prepaid = 0,
                Paid = 0,
                SelectedBankAccountId = null,
                UpdatedAt = DateTimeOffset.UtcNow,
                Items = new()
            });
        }

        return Ok(billing);
    }

    [HttpPost("billing-batch")]
    public async Task<ActionResult<Dictionary<int, CustomerReceiptBillingDto>>> GetBillingBatch([FromBody] List<int> ids)
    {
        if (ids == null || ids.Count == 0)
            return Ok(new Dictionary<int, CustomerReceiptBillingDto>());

        var isSuperAdmin = User.IsInRole("super_admin");
        IQueryable<CustomerReceipt> receiptQuery = _db.CustomerReceipts.AsNoTracking();

        if (!isSuperAdmin)
        {
            var targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
            receiptQuery = receiptQuery.Where(x => x.WorkshopId == targetWorkshopId);
        }

        var allowedIds = await receiptQuery
            .Where(x => ids.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        if (allowedIds.Count == 0)
            return Ok(new Dictionary<int, CustomerReceiptBillingDto>());

        var billings = await _db.CustomerReceiptBillings
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => allowedIds.Contains(x.CustomerReceiptId))
            .Select(x => new CustomerReceiptBillingDto
            {
                CustomerReceiptId = x.CustomerReceiptId,
                BillTotal = x.BillTotal,
                Discount = x.Discount,
                Prepaid = x.Prepaid,
                Paid = x.Paid,
                SelectedBankAccountId = x.SelectedBankAccountId,
                UpdatedAt = x.UpdatedAt,
                Items = x.Items.Select(i => new InvoiceItemDto
                {
                    Title = i.Title,
                    ActionDescription = i.ActionDescription,
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity,
                    TechnicianId = i.TechnicianId
                }).ToList()
            })
            .ToDictionaryAsync(x => x.CustomerReceiptId);

        // Fill missing IDs with empty billing
        var result = new Dictionary<int, CustomerReceiptBillingDto>();
        foreach (var id in allowedIds)
        {
            if (billings.TryGetValue(id, out var b))
                result[id] = b;
            else
                result[id] = new CustomerReceiptBillingDto
                {
                    CustomerReceiptId = id,
                    BillTotal = 0,
                    Discount = 0,
                    Prepaid = 0,
                    Paid = 0,
                    SelectedBankAccountId = null,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Items = new()
                };
        }

        return Ok(result);
    }

    [Authorize(Policy = "perm:admin.receipts.manage")]
    [HttpPut("{id:int}/billing")]
    public async Task<IActionResult> UpsertBilling(int id, [FromQuery] int? workshopId, CustomerReceiptBillingUpsertRequest request)
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

        var receipt = await _db.CustomerReceipts
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkshopId == targetWorkshopId);
        if (receipt == null) return NotFound();

        var billing = await _db.CustomerReceiptBillings
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.CustomerReceiptId == id);

        if (billing == null)
        {
            billing = new CustomerReceiptBilling
            {
                CustomerReceiptId = id,
                BillTotal = request.BillTotal,
                Discount = request.Discount,
                Prepaid = request.Prepaid,
                Paid = request.Paid,
                SelectedBankAccountId = request.SelectedBankAccountId,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.CustomerReceiptBillings.Add(billing);
        }
        else
        {
            billing.BillTotal = request.BillTotal;
            billing.Discount = request.Discount;
            billing.Prepaid = request.Prepaid;
            billing.Paid = request.Paid;
            billing.SelectedBankAccountId = request.SelectedBankAccountId;
            billing.UpdatedAt = DateTimeOffset.UtcNow;
            
            // Remove old items
            _db.RemoveRange(billing.Items);
        }

        // Add new items
        if (request.Items != null && request.Items.Any())
        {
            foreach (var item in request.Items)
            {
                billing.Items.Add(new CustomerReceiptInvoiceItem
                {
                    Title = item.Title,
                    ActionDescription = item.ActionDescription,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    TechnicianId = item.TechnicianId
                });
            }
        }

        receipt.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.receipts.manage")]
    [HttpPost]
    public async Task<ActionResult<CustomerReceiptDto>> Create(
        [FromQuery] int? workshopId,
        [FromForm] int customerId,
        [FromForm] int deviceTypeId,
        [FromForm] int? deviceBrandId,
        [FromForm] string problemDescription,
        [FromForm] string? status,
        [FromForm] int? technicianId,
        [FromForm] DateTimeOffset? registeredAt,
        [FromForm] IFormFile? file,
        [FromForm] string? receiptImageUrl)
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

        if (customerId <= 0) return BadRequest("انتخاب مشتری الزامی است");
        if (deviceTypeId <= 0) return BadRequest("انتخاب نوع دستگاه الزامی است");
        if (string.IsNullOrWhiteSpace(problemDescription)) return BadRequest("توضیحات مشکل الزامی است");

        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? CustomerReceiptStatuses.Pending : status.Trim();
        if (!CustomerReceiptStatuses.All.Contains(normalizedStatus))
            return BadRequest("وضعیت معتبر نیست");

        var customer = await _db.WorkshopCustomers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId && x.WorkshopId == targetWorkshopId);
        if (customer == null) return BadRequest("مشتری معتبر نیست");

        var deviceType = await _db.DeviceTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == deviceTypeId);
        if (deviceType == null) return BadRequest("نوع دستگاه معتبر نیست");

        if (deviceBrandId.HasValue)
        {
            var brandOk = await _db.DeviceBrands
                .AsNoTracking()
                .AnyAsync(x => x.Id == deviceBrandId.Value
                              && x.DeviceTypeId == deviceTypeId);
            if (!brandOk) return BadRequest("برند معتبر نیست");
        }

        if (technicianId.HasValue)
        {
            var techOk = await _db.WorkshopUsers.AsNoTracking().AnyAsync(x => x.Id == technicianId.Value && x.WorkshopId == targetWorkshopId && x.IsTechnician);
            if (!techOk) return BadRequest("تکنسین معتبر نیست");
        }

        int? creatorUserId = null;
        var workshopUserIdStr = User.FindFirst("workshop_user_id")?.Value;
        
        if (!string.IsNullOrWhiteSpace(workshopUserIdStr) && int.TryParse(workshopUserIdStr, out var workshopUserId))
        {
            creatorUserId = workshopUserId;
        }
        else
        {
            // Fallback for super_admin or other users without workshop_user_id claim
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userIdStr) && int.TryParse(userIdStr, out var userId))
                creatorUserId = userId;
        }

        string? savedUrl = null;
        if (!string.IsNullOrWhiteSpace(receiptImageUrl))
        {
            savedUrl = receiptImageUrl;
        }
        else if (file != null)
        {
            if (file.Length == 0) return BadRequest("فایل نامعتبر است");
            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest("فقط تصویر مجاز است");

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "customer-receipts", targetWorkshopId.ToString());
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            savedUrl = $"/uploads/customer-receipts/{targetWorkshopId}/{fileName}";
        }

        var entity = new CustomerReceipt
        {
            WorkshopId = targetWorkshopId,
            CustomerId = customerId,
            DeviceTypeId = deviceTypeId,
            DeviceBrandId = deviceBrandId,
            ProblemDescription = problemDescription.Trim(),
            ReceiptImageUrl = savedUrl,
            Status = normalizedStatus,
            RegisteredAt = registeredAt ?? DateTimeOffset.UtcNow,
            TechnicianId = technicianId,
            CreatedByUserId = creatorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.CustomerReceipts.Add(entity);
        await _db.SaveChangesAsync();

        var workshop = await _db.Workshops.AsNoTracking().FirstOrDefaultAsync(w => w.Id == entity.WorkshopId);

        return Ok(new CustomerReceiptDto
        {
            Id = entity.Id,
            WorkshopId = entity.WorkshopId,
            WorkshopName = workshop?.WorkshopName ?? string.Empty,
            CustomerId = customer.Id,
            CustomerFirstName = customer.FirstName,
            CustomerLastName = customer.LastName,
            CustomerMobile = customer.Mobile,
            CustomerAddress = customer.Address,
            DeviceTypeId = deviceType.Id,
            DeviceTypeName = deviceType.Name,
            DeviceBrandId = entity.DeviceBrandId,
            DeviceBrandName = entity.DeviceBrandId.HasValue
                ? await _db.DeviceBrands.AsNoTracking().Where(x => x.Id == entity.DeviceBrandId.Value).Select(x => x.Name).FirstOrDefaultAsync()
                : null,
            ProblemDescription = entity.ProblemDescription,
            ReceiptImageUrl = entity.ReceiptImageUrl,
            RegisteredAt = entity.RegisteredAt,
            Status = entity.Status,
            TechnicianId = entity.TechnicianId,
            TechnicianName = entity.TechnicianId.HasValue
                ? await _db.WorkshopUsers.AsNoTracking().Where(x => x.Id == entity.TechnicianId.Value).Select(x => x.FullName).FirstOrDefaultAsync()
                : null,
            CreatedByUserId = entity.CreatedByUserId,
            CreatedByUserName = entity.CreatedByUserId.HasValue
                ? await _db.WorkshopUsers.AsNoTracking().Where(x => x.Id == entity.CreatedByUserId.Value).Select(x => x.FullName).FirstOrDefaultAsync()
                : null,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        });
    }

    [Authorize(Policy = "perm:admin.receipts.view")]
    [HttpGet("by-customer/{mobile}")]
    public async Task<ActionResult<List<CustomerReceiptDto>>> GetByCustomerMobile(string mobile, [FromQuery] int? workshopId = null)
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

        var list = await _db.CustomerReceipts
            .AsNoTracking()
            .Where(x => x.WorkshopId == targetWorkshopId && x.Customer.Mobile == mobile)
            .Include(x => x.Customer)
            .Include(x => x.DeviceType)
            .Include(x => x.DeviceBrand)
            .Include(x => x.Technician)
            .Include(x => x.CreatedByUser)
            .OrderByDescending(x => x.RegisteredAt)
            .Select(x => new CustomerReceiptDto
            {
                Id = x.Id,
                WorkshopId = x.WorkshopId,
                CustomerId = x.CustomerId,
                CustomerFirstName = x.Customer.FirstName,
                CustomerLastName = x.Customer.LastName,
                CustomerMobile = x.Customer.Mobile,
                CustomerAddress = x.Customer.Address,
                CustomerIsBadPayer = x.Customer.IsBadPayer,
                DeviceTypeId = x.DeviceTypeId,
                DeviceTypeName = x.DeviceType.Name,
                DeviceBrandId = x.DeviceBrandId,
                DeviceBrandName = x.DeviceBrand != null ? x.DeviceBrand.Name : null,
                ProblemDescription = x.ProblemDescription,
                ReceiptImageUrl = x.ReceiptImageUrl,
                RegisteredAt = x.RegisteredAt,
                Status = x.Status,
                TechnicianId = x.TechnicianId,
                TechnicianName = x.Technician != null ? x.Technician.FullName : null,
                CreatedByUserId = x.CreatedByUserId,
                CreatedByUserName = x.CreatedByUser != null ? x.CreatedByUser.FullName : null,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    [Authorize(Policy = "perm:admin.receipts.view")]
    [HttpGet("customer-financial-summary/{mobile}")]
    public async Task<ActionResult<CustomerFinancialSummaryDto>> GetCustomerFinancialSummary(string mobile, [FromQuery] int? workshopId = null)
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

        var receipts = await _db.CustomerReceipts
            .AsNoTracking()
            .Where(x => x.WorkshopId == targetWorkshopId && x.Customer.Mobile == mobile)
            .Include(x => x.Billing)
            .ToListAsync();

        var summary = new CustomerFinancialSummaryDto
        {
            TotalBill = receipts.Sum(x => x.Billing?.BillTotal ?? 0),
            TotalDiscount = receipts.Sum(x => x.Billing?.Discount ?? 0),
            TotalPrepaid = receipts.Sum(x => x.Billing?.Prepaid ?? 0),
            TotalPaid = receipts.Sum(x => x.Billing?.Paid ?? 0),
            TotalRemaining = receipts.Sum(x => (x.Billing?.BillTotal ?? 0) - (x.Billing?.Discount ?? 0) - (x.Billing?.Prepaid ?? 0) - (x.Billing?.Paid ?? 0)),
            TotalReceipts = receipts.Count
        };

        return Ok(summary);
    }

    [Authorize(Policy = "perm:admin.receipts.manage")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CustomerReceiptDto>> Update(
        int id,
        [FromQuery] int? workshopId,
        [FromForm] int customerId,
        [FromForm] int deviceTypeId,
        [FromForm] int? deviceBrandId,
        [FromForm] string problemDescription,
        [FromForm] string? status,
        [FromForm] int? technicianId,
        [FromForm] DateTimeOffset registeredAt,
        [FromForm] IFormFile? file,
        [FromForm] string? receiptImageUrl)
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

        if (customerId <= 0) return BadRequest("انتخاب مشتری الزامی است");
        if (deviceTypeId <= 0) return BadRequest("انتخاب نوع دستگاه الزامی است");
        if (string.IsNullOrWhiteSpace(problemDescription)) return BadRequest("توضیحات مشکل الزامی است");

        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? CustomerReceiptStatuses.Pending : status.Trim();
        if (!CustomerReceiptStatuses.All.Contains(normalizedStatus))
            return BadRequest("وضعیت معتبر نیست");

        var entity = await _db.CustomerReceipts
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkshopId == targetWorkshopId);

        if (entity == null) return NotFound();

        var customer = await _db.WorkshopCustomers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId && x.WorkshopId == targetWorkshopId);
        if (customer == null) return BadRequest("مشتری معتبر نیست");

        var deviceType = await _db.DeviceTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == deviceTypeId);
        if (deviceType == null) return BadRequest("نوع دستگاه معتبر نیست");

        if (deviceBrandId.HasValue)
        {
            var brandOk = await _db.DeviceBrands
                .AsNoTracking()
                .AnyAsync(x => x.Id == deviceBrandId.Value
                              && x.DeviceTypeId == deviceTypeId);
            if (!brandOk) return BadRequest("برند معتبر نیست");
        }

        if (technicianId.HasValue)
        {
            var techOk = await _db.WorkshopUsers.AsNoTracking().AnyAsync(x => x.Id == technicianId.Value && x.WorkshopId == targetWorkshopId && x.IsTechnician);
            if (!techOk) return BadRequest("تکنسین معتبر نیست");
        }

        string? savedUrl = entity.ReceiptImageUrl;
        if (!string.IsNullOrWhiteSpace(receiptImageUrl))
        {
            savedUrl = receiptImageUrl;
        }
        else if (file != null)
        {
            if (file.Length == 0) return BadRequest("فایل نامعتبر است");
            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return BadRequest("فقط تصویر مجاز است");

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "customer-receipts", targetWorkshopId.ToString());
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            savedUrl = $"/uploads/customer-receipts/{targetWorkshopId}/{fileName}";
        }

        entity.CustomerId = customerId;
        entity.DeviceTypeId = deviceTypeId;
        entity.DeviceBrandId = deviceBrandId;
        entity.ProblemDescription = problemDescription.Trim();
        entity.Status = normalizedStatus;
        entity.RegisteredAt = registeredAt;
        entity.TechnicianId = technicianId;
        entity.ReceiptImageUrl = savedUrl;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        var brandName = entity.DeviceBrandId.HasValue
            ? await _db.DeviceBrands.AsNoTracking().Where(x => x.Id == entity.DeviceBrandId.Value).Select(x => x.Name).FirstOrDefaultAsync()
            : null;

        return Ok(new CustomerReceiptDto
        {
            Id = entity.Id,
            WorkshopId = entity.WorkshopId,
            CustomerId = customer.Id,
            CustomerFirstName = customer.FirstName,
            CustomerLastName = customer.LastName,
            CustomerMobile = customer.Mobile,
            CustomerAddress = customer.Address,
            DeviceTypeId = deviceType.Id,
            DeviceTypeName = deviceType.Name,
            DeviceBrandId = entity.DeviceBrandId,
            DeviceBrandName = brandName,
            ProblemDescription = entity.ProblemDescription,
            ReceiptImageUrl = entity.ReceiptImageUrl,
            RegisteredAt = entity.RegisteredAt,
            Status = entity.Status,
            TechnicianId = entity.TechnicianId,
            TechnicianName = entity.TechnicianId.HasValue
                ? await _db.WorkshopUsers.AsNoTracking().Where(x => x.Id == entity.TechnicianId.Value).Select(x => x.FullName).FirstOrDefaultAsync()
                : null,
            CreatedByUserId = entity.CreatedByUserId,
            CreatedByUserName = entity.CreatedByUserId.HasValue
                ? await _db.WorkshopUsers.AsNoTracking().Where(x => x.Id == entity.CreatedByUserId.Value).Select(x => x.FullName).FirstOrDefaultAsync()
                : null,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        });
    }

    [Authorize(Policy = "perm:admin.receipts.manage")]
    [HttpPost("upload")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<string>> Upload([FromForm] IFormFile file, [FromQuery] int? workshopId, CancellationToken cancellationToken)
    {
        if (file == null || file.Length <= 0)
            return BadRequest("فایل خالی است یا دریافت نشد");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".bin";

        var safeExt = ext.ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
        if (!allowed.Contains(safeExt))
            return BadRequest("فرمت فایل مجاز نیست. فقط jpg, jpeg, png, webp, svg");

        if (!TryResolveTargetWorkshopId(workshopId, out var targetWorkshopId, out var workshopError))
            return workshopError!;

        var relativeFolder = "uploads/customer-receipts/" + targetWorkshopId;

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

    [Authorize(Policy = "perm:admin.receipts.manage")]
    [HttpPost("{id:int}/send-registration-sms")]
    public async Task<ActionResult<SendSmsResultDto>> SendRegistrationSms(int id)
    {
        var receipt = await _db.CustomerReceipts
            .Include(r => r.Customer)
            .Include(r => r.DeviceType)
            .Include(r => r.DeviceBrand)
            .Include(r => r.Workshop)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (receipt == null) return NotFound();

        var isSuperAdmin = User.IsInRole("super_admin");
        if (!isSuperAdmin)
        {
            var userWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (userWorkshopId != receipt.WorkshopId)
                return Forbid();
        }

        var module = await _db.WorkshopSmsModules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.WorkshopId == receipt.WorkshopId && m.ModuleType == SmsModuleType.OrderRegistered);
        if (module?.IsActive == false)
            return BadRequest("ماژول پیامک پذیرش برای این کارگاه غیرفعال است");

        var wsSetting = await _db.WorkshopSmsSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.WorkshopId == receipt.WorkshopId);
        if (wsSetting != null && !wsSetting.IsActive)
            return BadRequest("پیامک برای این کارگاه غیرفعال است");

        var customerFullName = $"{receipt.Customer.FirstName} {receipt.Customer.LastName}";
        var deviceType = receipt.DeviceType?.Name ?? "";
        var deviceBrand = receipt.DeviceBrand?.Name;
        var workshopName = receipt.Workshop?.WorkshopName ?? "";
        var date = PersianDateHelper.ToPersianDateTimeString(receipt.RegisteredAt, false);

        var result = await _smsService.SendOrderRegisteredSmsAsync(
            receipt.WorkshopId,
            receipt.Customer.Mobile,
            customerFullName,
            deviceType,
            deviceBrand,
            date,
            workshopName,
            receipt.Id.ToString());

        if (!result.Success)
            return BadRequest(result.Message);
        return Ok(result);
    }

    [Authorize(Policy = "perm:admin.receipts.manage")]
    [HttpPost("{id:int}/send-ready-for-delivery-sms")]
    public async Task<ActionResult<SendSmsResultDto>> SendReadyForDeliverySms(int id)
    {
        var receipt = await _db.CustomerReceipts
            .Include(r => r.Customer)
            .Include(r => r.DeviceType)
            .Include(r => r.DeviceBrand)
            .Include(r => r.Workshop)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (receipt == null) return NotFound();

        var isSuperAdmin = User.IsInRole("super_admin");
        if (!isSuperAdmin)
        {
            var userWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (userWorkshopId != receipt.WorkshopId)
                return Forbid();
        }

        var moduleReady = await _db.WorkshopSmsModules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.WorkshopId == receipt.WorkshopId && m.ModuleType == SmsModuleType.OrderReadyForDelivery);
        if (moduleReady?.IsActive == false)
            return BadRequest("ماژول پیامک آماده تحویل برای این کارگاه غیرفعال است");

        var wsSetting = await _db.WorkshopSmsSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.WorkshopId == receipt.WorkshopId);
        if (wsSetting != null && !wsSetting.IsActive)
            return BadRequest("پیامک برای این کارگاه غیرفعال است");

        var customerFullName = $"{receipt.Customer.FirstName} {receipt.Customer.LastName}";
        var deviceType = receipt.DeviceType?.Name ?? "";
        var deviceBrand = receipt.DeviceBrand?.Name;
        var workshopName = receipt.Workshop?.WorkshopName ?? "";
        var date = PersianDateHelper.ToPersianDateTimeString(receipt.RegisteredAt, false);

        var result = await _smsService.SendOrderReadyForDeliverySmsAsync(
            receipt.WorkshopId,
            receipt.Customer.Mobile,
            customerFullName,
            deviceType,
            deviceBrand,
            date,
            workshopName,
            "آماده تحویل",
            receipt.Id);

        if (!result.Success)
            return BadRequest(result.Message);
        return Ok(result);
    }

    [Authorize(Policy = "perm:admin.receipts.manage")]
    [HttpPost("{id:int}/send-unrepairable-sms")]
    public async Task<ActionResult<SendSmsResultDto>> SendUnrepairableSms(int id)
    {
        var receipt = await _db.CustomerReceipts
            .Include(r => r.Customer)
            .Include(r => r.DeviceType)
            .Include(r => r.DeviceBrand)
            .Include(r => r.Workshop)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (receipt == null) return NotFound();

        var isSuperAdmin = User.IsInRole("super_admin");
        if (!isSuperAdmin)
        {
            var userWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (userWorkshopId != receipt.WorkshopId)
                return Forbid();
        }

        var moduleUnrepiarable = await _db.WorkshopSmsModules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.WorkshopId == receipt.WorkshopId && m.ModuleType == SmsModuleType.OrderUnrepairable);
        if (moduleUnrepiarable?.IsActive == false)
            return BadRequest("ماژول پیامک غیرقابل تعمیر برای این کارگاه غیرفعال است");

        var wsSetting = await _db.WorkshopSmsSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.WorkshopId == receipt.WorkshopId);
        if (wsSetting != null && !wsSetting.IsActive)
            return BadRequest("پیامک برای این کارگاه غیرفعال است");

        var customerFullName = $"{receipt.Customer.FirstName} {receipt.Customer.LastName}";
        var deviceType = receipt.DeviceType?.Name ?? "";
        var deviceBrand = receipt.DeviceBrand?.Name;
        var workshopName = receipt.Workshop?.WorkshopName ?? "";
        var date = PersianDateHelper.ToPersianDateTimeString(receipt.RegisteredAt, false);

        var result = await _smsService.SendOrderUnrepairableSmsAsync(
            receipt.WorkshopId,
            receipt.Customer.Mobile,
            customerFullName,
            deviceType,
            deviceBrand,
            date,
            workshopName,
            "غیر قابل تعمیر",
            receipt.Id);

        if (!result.Success)
            return BadRequest(result.Message);
        return Ok(result);
    }

    [Authorize(Policy = "perm:admin.receipts.manage")]
    [HttpGet("{id:int}/sms-logs")]
    public async Task<ActionResult<List<SmsLogDto>>> GetSmsLogs(int id)
    {
        var receipt = await _db.CustomerReceipts
            .FirstOrDefaultAsync(r => r.Id == id);
        if (receipt == null) return NotFound();

        var isSuperAdmin = User.IsInRole("super_admin");
        if (!isSuperAdmin)
        {
            var userWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (userWorkshopId != receipt.WorkshopId)
                return Forbid();
        }

        var logs = await _db.SmsLogs
            .Where(l => l.CustomerReceiptId == id)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new SmsLogDto
            {
                Id = l.Id,
                WorkshopId = l.WorkshopId,
                WorkshopName = l.Workshop != null ? l.Workshop.WorkshopName : "",
                ModuleTitle = l.ModuleType != null
                    ? (l.ModuleType == SmsModuleType.OrderRegistered ? "ثبت سفارش"
                        : l.ModuleType == SmsModuleType.OrderReadyForDelivery ? "آماده تحویل"
                        : l.ModuleType == SmsModuleType.OrderUnrepairable ? "غیر قابل تعمیر"
                        : l.ModuleType == SmsModuleType.NewJobForTechnician ? "کار جدید تکنسین"
                        : "")
                    : "",
                ModuleType = l.ModuleType != null ? l.ModuleType.ToString() : null,
                RecipientNumber = l.RecipientNumber,
                MessageText = l.MessageText,
                SendStatus = l.SendStatus == SmsSendStatus.Sent ? "ارسال شده"
                    : l.SendStatus == SmsSendStatus.Failed ? "ناموفق"
                    : l.SendStatus == SmsSendStatus.Pending ? "در انتظار" : "",
                ErrorMessage = l.ErrorMessage,
                Cost = l.Cost,
                DeliveryStatus = (int)l.DeliveryStatus,
                ProviderMessageId = l.ProviderMessageId,
                DeliveredAt = l.DeliveredAt,
                ProviderRawStatus = l.ProviderRawStatus,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();

        return Ok(logs);
    }

    [Authorize(Policy = "perm:admin.receipts.manage")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        return await DeleteInternal(id);
    }

    // POST fallback for hosts that block DELETE verb (e.g. WebDAV on IIS)
    [Authorize(Policy = "perm:admin.receipts.manage")]
    [HttpPost("delete/{id:int}")]
    public async Task<ActionResult> DeleteViaPost(int id)
    {
        return await DeleteInternal(id);
    }

    private async Task<ActionResult> DeleteInternal(int id)
    {
        var receipt = await _db.CustomerReceipts
            .Include(r => r.Billing)
                .ThenInclude(b => b.Items)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (receipt == null) return NotFound();

        // Delete all related records with Restrict FK behavior before deleting the receipt

        // 1. Monitoring archives (have Restrict FK to MonitoringReceiptConnection)
        var archives = await _db.Set<MonitoringDataArchive>()
            .Where(a => a.CustomerReceiptId == id)
            .ToListAsync();
        _db.RemoveRange(archives);

        // 2. Monitoring connections
        var connections = await _db.Set<MonitoringReceiptConnection>()
            .Where(c => c.CustomerReceiptId == id)
            .ToListAsync();
        _db.RemoveRange(connections);

        // 3. Online admission requests linked to this receipt
        var admissions = await _db.Set<OnlineAdmissionRequest>()
            .Where(a => a.CreatedReceiptId == id)
            .ToListAsync();
        _db.RemoveRange(admissions);

        // 4. Monitoring data records (set null FK — explicit null to be safe)
        var dataRecords = await _db.Set<MonitoringDataRecord>()
            .Where(r => r.CustomerReceiptId == id)
            .ToListAsync();
        foreach (var r in dataRecords) r.CustomerReceiptId = null;

        // 5. Monitoring alerts
        var alerts = await _db.Set<MonitoringAlert>()
            .Where(a => a.CustomerReceiptId == id)
            .ToListAsync();
        foreach (var a in alerts) a.CustomerReceiptId = null;

        // 6. Monitoring share links
        var shareLinks = await _db.Set<MonitoringShareLink>()
            .Where(s => s.CustomerReceiptId == id)
            .ToListAsync();
        foreach (var s in shareLinks) s.CustomerReceiptId = null;

        // 7. SmsLogs
        var smsLogs = await _db.Set<SmsLog>()
            .Where(s => s.CustomerReceiptId == id)
            .ToListAsync();
        foreach (var s in smsLogs) s.CustomerReceiptId = null;

        // Billing + invoice items cascade-delete automatically
        _db.CustomerReceipts.Remove(receipt);
        await _db.SaveChangesAsync();

        return Ok(new { message = "رسید با موفقیت حذف شد." });
    }

    private bool TryResolveTargetWorkshopId(int? workshopId, out int targetWorkshopId, out ActionResult? error)
    {
        error = null;
        var isSuperAdmin = User.IsInRole("super_admin");

        if (isSuperAdmin)
        {
            if (!workshopId.HasValue || workshopId.Value <= 0)
            {
                targetWorkshopId = 0;
                error = BadRequest("انتخاب کارگاه الزامی است");
                return false;
            }

            targetWorkshopId = workshopId.Value;
            return true;
        }

        targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
        return true;
    }
}
