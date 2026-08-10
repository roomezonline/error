using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/workshop-sms")]
public sealed class WorkshopSmsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public WorkshopSmsController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [Authorize]
    [HttpGet("dashboard")]
    public async Task<ActionResult<WorkshopSmsDashboardDto>> GetDashboard()
    {
        int wsId;
        try { wsId = ClaimsHelper.GetWorkshopId(User); }
        catch { return Forbid(); }

        var todayStart = DateTime.UtcNow.Date;
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var credit = await _db.WorkshopSmsCredits
            .FirstOrDefaultAsync(c => c.WorkshopId == wsId);

        var tariff = (await _db.SmsSettings.FirstOrDefaultAsync())?.TariffPerSms ?? 1;
        var balance = credit?.Balance ?? 0;
        var smsCount = tariff > 0 ? (int)(balance / tariff) : 0;

        var todaySent = await _db.SmsLogs.CountAsync(l => l.WorkshopId == wsId && l.CreatedAt >= todayStart);
        var pendingCharges = await _db.SmsChargeTransactions
            .CountAsync(t => t.WorkshopId == wsId && t.Status == SmsChargeStatus.Pending);
        var monthlyCost = (await _db.SmsLogs
            .Where(l => l.WorkshopId == wsId && l.CreatedAt >= monthStart)
            .SumAsync(l => (decimal?)l.Cost)) ?? 0;

        var wsSetting = await _db.WorkshopSmsSettings
            .FirstOrDefaultAsync(s => s.WorkshopId == wsId);

        var globalSettings = await _db.SmsSettings.FirstOrDefaultAsync();

        return Ok(new WorkshopSmsDashboardDto
        {
            Balance = balance,
            SmsCount = smsCount,
            TodaySentCount = todaySent,
            PendingChargeCount = pendingCharges,
            MonthlyCost = monthlyCost,
            SmsActive = wsSetting?.IsActive ?? true,
            WarningThreshold = globalSettings?.WarningThreshold
        });
    }

    private int GetWorkshopId()
    {
        try { return ClaimsHelper.GetWorkshopId(User); }
        catch { throw new UnauthorizedAccessException("کارگاه در توکن شناسایی نشد"); }
    }

    private int ResolveWorkshopId(int? queryWorkshopId = null)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        if (isSuperAdmin)
        {
            if (queryWorkshopId.HasValue && queryWorkshopId.Value > 0)
                return queryWorkshopId.Value;
            throw new UnauthorizedAccessException("کارگاه مشخص نشده است");
        }
        return GetWorkshopId();
    }

    private static string GetModuleTitle(SmsModuleType type) => type switch
    {
        SmsModuleType.OrderRegistered => "ثبت سفارش",
        SmsModuleType.OrderReadyForDelivery => "آماده تحویل",
        SmsModuleType.OrderUnrepairable => "غیر قابل تعمیر",
        SmsModuleType.NewJobForTechnician => "کار جدید تکنسین",
        SmsModuleType.ActivationCode => "کد فعالسازی",
        _ => "نامشخص"
    };

    private static string GetChargeStatusTitle(SmsChargeStatus status) => status switch
    {
        SmsChargeStatus.Pending => "در انتظار تایید",
        SmsChargeStatus.Approved => "تایید شده",
        SmsChargeStatus.Rejected => "رد شده",
        _ => "نامشخص"
    };

    // ─── تنظیمات پیامک کارگاه ───
    [Authorize(Policy = "perm:workshop.sms.settings.manage")]
    [HttpGet("settings")]
    public async Task<ActionResult<WorkshopSmsSettingDto>> GetSettings([FromQuery] int? workshopId = null)
    {
        var wsId = ResolveWorkshopId(workshopId);
        var setting = await _db.WorkshopSmsSettings
            .Include(x => x.Workshop)
            .FirstOrDefaultAsync(x => x.WorkshopId == wsId);

        if (setting == null)
        {
            var ws = await _db.Workshops.FindAsync(wsId);
            if (ws == null) return NotFound("کارگاه یافت نشد");
            return Ok(new WorkshopSmsSettingDto
            {
                WorkshopId = wsId,
                WorkshopName = ws.WorkshopName,
                IsActive = true
            });
        }

        return Ok(new WorkshopSmsSettingDto
        {
            Id = setting.Id,
            WorkshopId = setting.WorkshopId,
            WorkshopName = setting.Workshop.WorkshopName,
            IsActive = setting.IsActive,
            UpdatedAt = setting.UpdatedAt
        });
    }

    [Authorize(Policy = "perm:workshop.sms.settings.manage")]
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(WorkshopSmsSettingUpdateRequest request, [FromQuery] int? workshopId = null)
    {
        var wsId = ResolveWorkshopId(workshopId);
        var setting = await _db.WorkshopSmsSettings
            .FirstOrDefaultAsync(x => x.WorkshopId == wsId);

        if (setting == null)
        {
            setting = new WorkshopSmsSetting
            {
                WorkshopId = wsId,
                IsActive = request.IsActive
            };
            _db.WorkshopSmsSettings.Add(setting);
        }
        else
        {
            setting.IsActive = request.IsActive;
        }

        setting.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── ماژول‌های پیامک کارگاه ───
    [Authorize(Policy = "perm:workshop.sms.settings.manage")]
    [HttpGet("modules")]
    public async Task<ActionResult<List<WorkshopSmsModuleDto>>> GetModules([FromQuery] int? workshopId = null)
    {
        var wsId = ResolveWorkshopId(workshopId);
        var modules = await _db.WorkshopSmsModules
            .Where(x => x.WorkshopId == wsId)
            .ToListAsync();

        var allTypes = Enum.GetValues<SmsModuleType>().Where(t => t != SmsModuleType.ActivationCode);
        var result = allTypes.Select(t =>
        {
            var existing = modules.FirstOrDefault(m => m.ModuleType == t);
            return new WorkshopSmsModuleDto
            {
                Id = existing?.Id ?? 0,
                WorkshopId = wsId,
                ModuleType = (int)t,
                ModuleTitle = GetModuleTitle(t),
                IsActive = existing?.IsActive ?? true,
                CustomTemplate = existing?.CustomTemplate
            };
        }).ToList();

        return Ok(result);
    }

    [Authorize(Policy = "perm:workshop.sms.settings.manage")]
    [HttpPut("modules/{id:int}")]
    public async Task<IActionResult> UpdateModule(int id, WorkshopSmsModuleUpdateRequest request, [FromQuery] int? workshopId = null)
    {
        var wsId = ResolveWorkshopId(workshopId);

        WorkshopSmsModule? module;
        if (id == 0 && request.ModuleType.HasValue)
        {
            module = await _db.WorkshopSmsModules
                .FirstOrDefaultAsync(m => m.WorkshopId == wsId && m.ModuleType == (SmsModuleType)request.ModuleType.Value);
            if (module == null)
            {
                module = new WorkshopSmsModule
                {
                    WorkshopId = wsId,
                    ModuleType = (SmsModuleType)request.ModuleType.Value,
                    IsActive = request.IsActive,
                    CustomTemplate = request.CustomTemplate
                };
                _db.WorkshopSmsModules.Add(module);
                await _db.SaveChangesAsync();
                return Ok(new { id = module.Id });
            }
        }
        else
        {
            module = await _db.WorkshopSmsModules
                .FirstOrDefaultAsync(m => m.Id == id && m.WorkshopId == wsId);
            if (module == null) return NotFound();
        }

        module.IsActive = request.IsActive;
        module.CustomTemplate = request.CustomTemplate;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── اعتبار کارگاه ───
    [Authorize(Policy = "perm:workshop.sms.credit.view")]
    [HttpGet("credit")]
    public async Task<ActionResult<WorkshopSmsCreditDto>> GetCredit()
    {
        var wsId = GetWorkshopId();
        var tariff = (await _db.SmsSettings.FirstOrDefaultAsync())?.TariffPerSms ?? 1;
        var credit = await _db.WorkshopSmsCredits
            .Include(x => x.Workshop)
            .FirstOrDefaultAsync(x => x.WorkshopId == wsId);

        if (credit == null)
        {
            var ws = await _db.Workshops.FindAsync(wsId);
            if (ws == null) return NotFound("کارگاه یافت نشد");
            return Ok(new WorkshopSmsCreditDto
            {
                WorkshopId = wsId,
                WorkshopName = ws.WorkshopName,
                Balance = 0,
                SmsCount = 0
            });
        }

        return Ok(new WorkshopSmsCreditDto
        {
            WorkshopId = credit.WorkshopId,
            WorkshopName = credit.Workshop.WorkshopName,
            Balance = credit.Balance,
            SmsCount = tariff > 0 ? (int)(credit.Balance / tariff) : 0
        });
    }

    // ─── تاریخچه شارژ کارگاه ───
    [Authorize(Policy = "perm:workshop.sms.charge.history")]
    [HttpGet("charges")]
    public async Task<ActionResult<PagedResult<SmsChargeTransactionDto>>> GetMyCharges(
        [FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var wsId = GetWorkshopId();
        var query = _db.SmsChargeTransactions
            .Include(x => x.Workshop)
            .Where(x => x.WorkshopId == wsId);

        if (status.HasValue)
            query = query.Where(x => x.Status == (SmsChargeStatus)status.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SmsChargeTransactionDto
            {
                Id = x.Id,
                WorkshopId = x.WorkshopId,
                WorkshopName = x.Workshop.WorkshopName,
                Amount = x.Amount,
                SmsCount = x.SmsCount,
                Description = x.Description,
                ReceiptFileName = x.ReceiptFileName,
                ReceiptFilePath = x.ReceiptFilePath,
                Status = (int)x.Status,
                StatusTitle = GetChargeStatusTitle(x.Status),
                ReviewedBy = x.ReviewedByUserName,
                ReviewNote = x.ReviewNote,
                CreatedAt = x.CreatedAt,
                ReviewedAt = x.ReviewedAt
            })
            .ToListAsync();

        return Ok(new PagedResult<SmsChargeTransactionDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    // ─── ثبت درخواست شارژ توسط کارگاه ───
    [Authorize(Policy = "perm:workshop.sms.charge.request")]
    [HttpPost("charges")]
    public async Task<IActionResult> RequestCharge(SmsChargeSubmitRequest request)
    {
        int wsId;
        try { wsId = ClaimsHelper.GetWorkshopId(User); }
        catch { return Forbid(); }

        var tariff = (await _db.SmsSettings.FirstOrDefaultAsync())?.TariffPerSms ?? 1;
        if (tariff <= 0) return BadRequest("تعرفه پیامک تنظیم نشده است");

        var tx = new SmsChargeTransaction
        {
            WorkshopId = wsId,
            Amount = request.Amount,
            SmsCount = (int)(request.Amount / tariff),
            Description = request.Description,
            Status = SmsChargeStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.SmsChargeTransactions.Add(tx);
        await _db.SaveChangesAsync();
        return Ok(new { id = tx.Id });
    }

    // ─── آپلود رسید پرداخت ───
    [Authorize(Policy = "perm:workshop.sms.charge.request")]
    [HttpPost("charges/{id:int}/receipt")]
    public async Task<IActionResult> UploadReceipt(int id, IFormFile file)
    {
        int wsId;
        try { wsId = ClaimsHelper.GetWorkshopId(User); }
        catch { return Forbid(); }

        var tx = await _db.SmsChargeTransactions
            .FirstOrDefaultAsync(x => x.Id == id && x.WorkshopId == wsId);
        if (tx == null) return NotFound("درخواست شارژ یافت نشد");
        if (tx.Status != SmsChargeStatus.Pending)
            return BadRequest("فقط درخواست‌های در انتظار تایید قابل ویرایش هستند");

        if (file == null || file.Length == 0)
            return BadRequest("فایل ارسال نشده است");

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "sms-receipts");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"receipt_{tx.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        tx.ReceiptFileName = file.FileName;
        tx.ReceiptFilePath = $"/uploads/sms-receipts/{fileName}";
        await _db.SaveChangesAsync();

        return Ok(new { receiptPath = tx.ReceiptFilePath });
    }

    // ─── گزارش ارسال کارگاه ───
    [Authorize(Policy = "perm:workshop.sms.logs.view")]
    [HttpGet("logs")]
    public async Task<ActionResult<PagedResult<SmsLogDto>>> GetMyLogs(
        [FromQuery] int? moduleType, [FromQuery] int? sendStatus,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var wsId = GetWorkshopId();
        var query = _db.SmsLogs
            .Include(x => x.Workshop)
            .Where(x => x.WorkshopId == wsId);

        if (moduleType.HasValue)
            query = query.Where(x => x.ModuleType == (SmsModuleType)moduleType.Value);
        if (sendStatus.HasValue)
            query = query.Where(x => x.SendStatus == (SmsSendStatus)sendStatus.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SmsLogDto
            {
                Id = x.Id,
                WorkshopId = x.WorkshopId,
                WorkshopName = x.Workshop.WorkshopName,
                ModuleTitle = x.ModuleType != null ? GetModuleTitle(x.ModuleType.Value) : null,
                RecipientNumber = x.RecipientNumber,
                MessageText = x.MessageText,
                SendStatus = x.SendStatus == SmsSendStatus.Sent ? "ارسال شده" : x.SendStatus == SmsSendStatus.Failed ? "ناموفق" : "در انتظار",
                ErrorMessage = x.ErrorMessage,
                Cost = x.Cost,
                DeliveryStatus = (int)x.DeliveryStatus,
                ProviderMessageId = x.ProviderMessageId,
                DeliveredAt = x.DeliveredAt,
                ProviderRawStatus = x.ProviderRawStatus,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        return Ok(new PagedResult<SmsLogDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    [Authorize]
    [HttpGet("tariff")]
    public async Task<ActionResult<int>> GetTariff()
    {
        var settings = await _db.SmsSettings.FirstOrDefaultAsync();
        return Ok(settings?.TariffPerSms ?? 0);
    }

    [Authorize]
    [HttpGet("module-status")]
    public async Task<ActionResult<bool>> GetModuleStatus([FromQuery] int workshopId, [FromQuery] int moduleType)
    {
        var wsSetting = await _db.WorkshopSmsSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.WorkshopId == workshopId);
        if (wsSetting != null && !wsSetting.IsActive)
            return Ok(false);

        var module = await _db.WorkshopSmsModules
            .FirstOrDefaultAsync(m => m.WorkshopId == workshopId && m.ModuleType == (SmsModuleType)moduleType);
        return Ok(module?.IsActive ?? true);
    }
}
