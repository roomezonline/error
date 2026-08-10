using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/sms-admin")]
public sealed class SmsAdminController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly ISmsService _smsService;

    public SmsAdminController(ErrorServiceDbContext db, ISmsService smsService)
    {
        _db = db;
        _smsService = smsService;
    }

    private static SmsDeliveryStatus MapDeliveryState(byte? state) => state switch
    {
        1 => SmsDeliveryStatus.Delivered,        // رسیده
        3 => SmsDeliveryStatus.SentToTelecom,     // رسیده به مخابرات
        5 => SmsDeliveryStatus.SentToTelecom,     // رسیده به اپراتور
        _ => SmsDeliveryStatus.NotDelivered       // 2 نرسیده به گوشی, 4 نرسیده به مخابرات, 6 ناموفق, 7 لیست سیاه, 8 نامشخص, null
    };

    private static string GetChargeStatusTitle(SmsChargeStatus status) => status switch
    {
        SmsChargeStatus.Pending => "در انتظار تایید",
        SmsChargeStatus.Approved => "تایید شده",
        SmsChargeStatus.Rejected => "رد شده",
        _ => "نامشخص"
    };

    [Authorize]
    [HttpGet("dashboard")]
    public async Task<ActionResult<SmsDashboardDto>> GetDashboard()
    {
        var todayStart = DateTime.UtcNow.Date;
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalBalance = (await _db.WorkshopSmsCredits.SumAsync(c => (decimal?)c.Balance)) ?? 0;
        var activeWorkshops = await _db.Workshops.CountAsync(w => w.IsActive);
        var todaySent = await _db.SmsLogs.CountAsync(l => l.CreatedAt >= todayStart);
        var pendingCharges = await _db.SmsChargeTransactions.CountAsync(t => t.Status == SmsChargeStatus.Pending);
        var monthlyCost = (await _db.SmsLogs.Where(l => l.CreatedAt >= monthStart).SumAsync(l => (decimal?)l.Cost)) ?? 0;

        return Ok(new SmsDashboardDto
        {
            TotalActiveWorkshops = activeWorkshops,
            TotalBalance = totalBalance,
            TodaySentCount = todaySent,
            PendingChargeCount = pendingCharges,
            MonthlyCost = monthlyCost
        });
    }

    // ─── اعتبار همه کارگاه‌ها ───
    [Authorize(Policy = "perm:admin.sms.credits.view")]
    [HttpGet("credits")]
    public async Task<ActionResult<List<WorkshopSmsCreditDto>>> GetAllCredits()
    {
        var tariff = (await _db.SmsSettings.FirstOrDefaultAsync())?.TariffPerSms ?? 1;
        var workshops = await _db.Workshops.Where(w => w.IsActive).ToListAsync();
        var credits = await _db.WorkshopSmsCredits.Include(x => x.Workshop).ToListAsync();

        return Ok(workshops.Select(w =>
        {
            var c = credits.FirstOrDefault(x => x.WorkshopId == w.Id);
            return new WorkshopSmsCreditDto
            {
                WorkshopId = w.Id,
                WorkshopName = w.WorkshopName,
                Balance = c?.Balance ?? 0,
                SmsCount = tariff > 0 ? (int)((c?.Balance ?? 0) / tariff) : 0
            };
        }).ToList());
    }

    // ─── اعتبار یک کارگاه خاص ───
    [Authorize(Policy = "perm:admin.sms.credits.view")]
    [HttpGet("credits/{workshopId:int}")]
    public async Task<ActionResult<WorkshopSmsCreditDto>> GetWorkshopCredit(int workshopId)
    {
        var tariff = (await _db.SmsSettings.FirstOrDefaultAsync())?.TariffPerSms ?? 1;
        var ws = await _db.Workshops.FindAsync(workshopId);
        if (ws == null) return NotFound("کارگاه یافت نشد");

        var credit = await _db.WorkshopSmsCredits
            .FirstOrDefaultAsync(x => x.WorkshopId == workshopId);

        return Ok(new WorkshopSmsCreditDto
        {
            WorkshopId = ws.Id,
            WorkshopName = ws.WorkshopName,
            Balance = credit?.Balance ?? 0,
            SmsCount = tariff > 0 ? (int)((credit?.Balance ?? 0) / tariff) : 0
        });
    }

    // ─── تعدیل دستی اعتبار ───
    [Authorize(Policy = "perm:admin.sms.credits.manage")]
    [HttpPost("credits/adjust")]
    public async Task<IActionResult> AdjustCredit([FromQuery] int workshopId, [FromQuery] decimal amount, [FromQuery] string? reason)
    {
        var credit = await _db.WorkshopSmsCredits
            .FirstOrDefaultAsync(c => c.WorkshopId == workshopId);
        if (credit == null)
        {
            credit = new WorkshopSmsCredit
            {
                WorkshopId = workshopId,
                Balance = 0
            };
            _db.WorkshopSmsCredits.Add(credit);
        }

        credit.Balance += amount;
        credit.UpdatedAt = DateTime.UtcNow;

        var tx = new SmsChargeTransaction
        {
            WorkshopId = workshopId,
            Amount = amount,
            SmsCount = 0,
            Description = $"تعدیل دستی: {reason}",
            Status = SmsChargeStatus.Approved,
            ReviewedByUserName = User.Identity?.Name,
            CreatedAt = DateTime.UtcNow,
            ReviewedAt = DateTime.UtcNow
        };
        _db.SmsChargeTransactions.Add(tx);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── همه درخواست‌های شارژ ───
    [Authorize(Policy = "perm:admin.sms.charges.view")]
    [HttpGet("charges")]
    public async Task<ActionResult<PagedResult<SmsChargeTransactionDto>>> GetAllCharges(
        [FromQuery] int? workshopId, [FromQuery] int? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.SmsChargeTransactions.Include(x => x.Workshop).AsQueryable();

        if (workshopId.HasValue)
            query = query.Where(x => x.WorkshopId == workshopId.Value);
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

    // ─── تایید/رد درخواست شارژ ───
    [Authorize(Policy = "perm:admin.sms.charges.approve")]
    [HttpPut("charges/{id:int}/review")]
    public async Task<IActionResult> ReviewCharge(int id, SmsChargeReviewRequest request)
    {
        var tx = await _db.SmsChargeTransactions.FindAsync(id);
        if (tx == null) return NotFound();
        if (tx.Status != SmsChargeStatus.Pending)
            return BadRequest("این درخواست قبلاً بررسی شده است");

        tx.ReviewNote = request.ReviewNote;
        tx.ReviewedAt = DateTime.UtcNow;
        tx.ReviewedByUserName = User.Identity?.Name;

        if (request.Approved)
        {
            tx.Status = SmsChargeStatus.Approved;

            var credit = await _db.WorkshopSmsCredits
                .FirstOrDefaultAsync(c => c.WorkshopId == tx.WorkshopId);
            if (credit == null)
            {
                credit = new WorkshopSmsCredit
                {
                    WorkshopId = tx.WorkshopId,
                    Balance = 0
                };
                _db.WorkshopSmsCredits.Add(credit);
            }
            credit.Balance += tx.Amount;
            credit.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            tx.Status = SmsChargeStatus.Rejected;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── برگرداندن تراکنش به حالت انتظار ───
    [Authorize(Policy = "perm:admin.sms.charges.approve")]
    [HttpPost("charges/{id:int}/revert")]
    public async Task<IActionResult> RevertCharge(int id)
    {
        var tx = await _db.SmsChargeTransactions.FindAsync(id);
        if (tx == null) return NotFound();
        if (tx.Status != SmsChargeStatus.Approved && tx.Status != SmsChargeStatus.Rejected)
            return BadRequest("فقط تراکنش‌های تایید یا رد شده قابل برگشت هستند");

        if (tx.Status == SmsChargeStatus.Approved)
        {
            var credit = await _db.WorkshopSmsCredits
                .FirstOrDefaultAsync(c => c.WorkshopId == tx.WorkshopId);
            if (credit != null)
            {
                credit.Balance -= tx.Amount;
                if (credit.Balance < 0) credit.Balance = 0;
                credit.UpdatedAt = DateTime.UtcNow;
            }
        }

        tx.Status = SmsChargeStatus.Pending;
        tx.ReviewNote = null;
        tx.ReviewedAt = null;
        tx.ReviewedByUserName = null;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ─── گزارشات همه کارگاه‌ها ───
    [Authorize(Policy = "perm:admin.sms.logs.view")]
    [HttpGet("logs")]
    public async Task<ActionResult<PagedResult<SmsLogDto>>> GetAllLogs(
        [FromQuery] int? workshopId, [FromQuery] int? moduleType, [FromQuery] int? sendStatus,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _db.SmsLogs.Include(x => x.Workshop).AsQueryable();

        if (workshopId.HasValue)
            query = query.Where(x => x.WorkshopId == workshopId.Value);
        if (moduleType.HasValue)
            query = query.Where(x => x.ModuleType == (SmsModuleType)moduleType.Value);
        if (sendStatus.HasValue)
            query = query.Where(x => x.SendStatus == (SmsSendStatus)sendStatus.Value);
        if (dateFrom.HasValue)
            query = query.Where(x => x.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(x => x.CreatedAt <= dateTo.Value);

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
                ModuleTitle = x.ModuleType != null ?
                    (x.ModuleType == SmsModuleType.OrderRegistered ? "ثبت سفارش" :
                     x.ModuleType == SmsModuleType.OrderReadyForDelivery ? "آماده تحویل" :
                     x.ModuleType == SmsModuleType.OrderUnrepairable ? "غیر قابل تعمیر" :
                     x.ModuleType == SmsModuleType.NewJobForTechnician ? "کار جدید تکنسین" :
                     x.ModuleType == SmsModuleType.ActivationCode ? "کد فعالسازی" : "نامشخص") : null,
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

    [Authorize(Policy = "perm:admin.sms.logs.view")]
    [HttpPost("logs/check-delivery")]
    public async Task<ActionResult<DeliveryCheckBatchResult>> CheckDelivery()
    {
        var settings = await _db.SmsSettings.FirstOrDefaultAsync();
        if (settings == null || string.IsNullOrWhiteSpace(settings.ApiKey))
            return BadRequest("تنظیمات پیامک پیکربندی نشده است");

        // Get all pending logs; skip those without a messageId inside the loop
        var pendingLogs = await _db.SmsLogs
            .Where(x => x.DeliveryStatus == SmsDeliveryStatus.Pending)
            .Take(50)
            .ToListAsync();

        var checkedCount = 0;
        var updated = 0;
        var errors = 0;

        foreach (var log in pendingLogs)
        {
            if (string.IsNullOrWhiteSpace(log.ProviderMessageId))
            {
                errors++;
                continue;
            }

            checkedCount++;

            var result = await _smsService.CheckDeliveryStatusAsync(log.ProviderMessageId, settings.ApiKey);
            if (!result.Success)
            {
                errors++;
                continue;
            }

            log.DeliveryStatus = MapDeliveryState(result.DeliveryState);
            if (result.DeliveryDateTime.HasValue)
                log.DeliveredAt = result.DeliveryDateTime;
            if (result.ProviderRawStatus != null)
                log.ProviderRawStatus = result.ProviderRawStatus;

            updated++;
        }

        await _db.SaveChangesAsync();

        return Ok(new DeliveryCheckBatchResult
        {
            Checked = checkedCount,
            Updated = updated,
            Errors = errors
        });
    }
}
