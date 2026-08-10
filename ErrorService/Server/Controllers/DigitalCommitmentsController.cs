using System.Security.Claims;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/digital-commitments")]
public sealed class DigitalCommitmentsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly ISmsService _sms;
    private readonly IMemoryCache _cache;
    private const int WorkshopIdForOtp = 1;
    private const int OtpTemplateId = 969320;
    private static readonly TimeSpan OtpExpiry = TimeSpan.FromMinutes(2);

    public DigitalCommitmentsController(ErrorServiceDbContext db, ISmsService sms, IMemoryCache cache)
    {
        _db = db;
        _sms = sms;
        _cache = cache;
    }

    [Authorize(Policy = "perm:admin.receipts.manage")]
    [HttpPost]
    public async Task<ActionResult<DigitalCommitmentDto>> Create([FromBody] DigitalCommitmentCreateRequest request)
    {
        if (!IsWorkshop1())
            return BadRequest("امکان ایجاد تعهدنامه فقط برای کارگاه شماره ۱ فعال است");

        var receipt = await _db.CustomerReceipts
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.DeviceType)
            .Include(x => x.DeviceBrand)
            .Include(x => x.Workshop)
            .FirstOrDefaultAsync(x => x.Id == request.CustomerReceiptId);

        if (receipt == null)
            return NotFound("رسید یافت نشد");

        if (receipt.WorkshopId != 1)
            return BadRequest("امکان ایجاد تعهدنامه فقط برای رسیدهای کارگاه شماره ۱ فعال است");

        var existing = await _db.DigitalCommitments.AnyAsync(x => x.CustomerReceiptId == request.CustomerReceiptId);
        if (existing)
            return BadRequest("برای این رسید قبلاً تعهدنامه ثبت شده است");

        var isSuperAdmin = User.IsInRole("super_admin");
        if (!isSuperAdmin)
        {
            var workshopId = ClaimsHelper.GetWorkshopId(User);
            if (receipt.WorkshopId != workshopId)
                return Forbid();
        }

        var registeredAtFa = PersianDateHelper.ToPersianDateTimeString(receipt.RegisteredAt, false);
        var deviceBrand = receipt.DeviceBrand?.Name;
        var customerFullName = $"{receipt.Customer.FirstName} {receipt.Customer.LastName}";

        var bodyBeforeExtra = $"اینجانب {customerFullName} بابت یک دستگاه {receipt.DeviceType.Name}{(deviceBrand != null ? $" برند {deviceBrand}" : "")} به شماره پذیرش {receipt.Id} در تاریخ {registeredAtFa} در {receipt.Workshop.WorkshopName}";
        var typedText = string.IsNullOrWhiteSpace(request.ExtraBodyText) ? "...." : request.ExtraBodyText;
        var fullBodyText = $"{bodyBeforeExtra} متعهد می‌شوم {typedText} موافقت خود را اعلام میکنم";

        int? userId = null;
        var workshopUserIdStr = User.FindFirst("workshop_user_id")?.Value;
        if (!string.IsNullOrWhiteSpace(workshopUserIdStr) && int.TryParse(workshopUserIdStr, out var wuId))
            userId = wuId;
        else
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userIdStr) && int.TryParse(userIdStr, out var uid))
                userId = uid;
        }

        var entity = new DigitalCommitment
        {
            CustomerReceiptId = receipt.Id,
            WorkshopId = receipt.WorkshopId,
            CustomerId = receipt.CustomerId,
            CustomerFullName = customerFullName,
            CustomerMobile = receipt.Customer.Mobile,
            DeviceTypeName = receipt.DeviceType.Name,
            DeviceBrandName = deviceBrand,
            ReceiptNumber = receipt.Id,
            RegisteredAtFa = registeredAtFa,
            WorkshopName = receipt.Workshop.WorkshopName,
            WorkshopLogoUrl = receipt.Workshop.ImageUrl,
            ExtraBodyText = request.ExtraBodyText,
            FullBodyText = fullBodyText,
            Status = CommitmentStatuses.Pending,
            CreatedByUserId = userId
        };

        _db.DigitalCommitments.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(MapDto(entity));
    }

    [HttpPost("search")]
    public async Task<ActionResult<List<DigitalCommitmentDto>>> SearchByMobile([FromBody] CommitmentSearchRequest request)
    {
        var entities = await _db.DigitalCommitments
            .AsNoTracking()
            .Where(x => x.WorkshopId == 1 && x.CustomerMobile == request.Mobile && x.Status == CommitmentStatuses.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var list = entities.Select(MapDto).ToList();

        return Ok(list);
    }

    [HttpPost("send-otp")]
    public async Task<ActionResult<OtpResultDto>> SendOtp([FromBody] SendOtpRequest request)
    {
        var smsSettings = await _db.SmsSettings.FirstOrDefaultAsync();
        if (smsSettings != null && !smsSettings.OtpEnabled)
            return Ok(new OtpResultDto { Success = false, Message = "ارسال کد تایید توسط مدیر سیستم غیرفعال شده است." });

        var hasPending = await _db.DigitalCommitments.AnyAsync(x => x.WorkshopId == 1 && x.CustomerMobile == request.Mobile && x.Status == CommitmentStatuses.Pending);
        if (!hasPending)
            return Ok(new OtpResultDto { Success = false, Message = "تعهدنامه‌ای برای این شماره یافت نشد" });

        var code = Random.Shared.Next(100000, 999999).ToString();
        _cache.Set($"commitment_otp:{request.Mobile}", code, OtpExpiry);

        var result = await _sms.SendVerificationSmsAsync(WorkshopIdForOtp, request.Mobile, code, OtpTemplateId);

        if (result.Success == false)
        {
            _cache.Remove($"commitment_otp:{request.Mobile}");
            Console.Error.WriteLine($"[SmsService ERROR] {result.Message} — mobile: {request.Mobile}");
        }
        else
        {
            var senderLine = result.LineNumber ?? smsSettings?.SenderNumber;
            if (!string.IsNullOrEmpty(senderLine))
                _cache.Set($"commitment_otp_line:{request.Mobile}", senderLine, OtpExpiry);
        }

        return Ok(result);
    }

    [HttpPost("verify-and-sign")]
    public async Task<ActionResult<OtpResultDto>> VerifyAndSign([FromBody] CommitmentSignRequest request)
    {
        var cached = _cache.Get<string>($"commitment_otp:{request.Mobile}");
        if (cached == null)
            return Ok(new OtpResultDto { Success = false, Message = "کد تایید منقضی شده است. لطفاً مجدداً درخواست دهید." });

        if (cached != request.Code)
            return Ok(new OtpResultDto { Success = false, Message = "کد تایید اشتباه است" });

        var commitment = await _db.DigitalCommitments
            .FirstOrDefaultAsync(x => x.Id == request.CommitmentId && x.CustomerMobile == request.Mobile && x.Status == CommitmentStatuses.Pending);

        if (commitment == null)
            return Ok(new OtpResultDto { Success = false, Message = "تعهدنامه یافت نشد یا قبلاً امضا شده است" });

        commitment.Status = CommitmentStatuses.Signed;
        commitment.SignedAt = DateTimeOffset.UtcNow;
        commitment.VerificationCode = request.Code;

        var cachedLine = _cache.Get<string>($"commitment_otp_line:{request.Mobile}");
        if (!string.IsNullOrEmpty(cachedLine))
            commitment.SenderLineNumber = cachedLine;

        await _db.SaveChangesAsync();

        _cache.Remove($"commitment_otp:{request.Mobile}");
        _cache.Remove($"commitment_otp_line:{request.Mobile}");

        return Ok(new OtpResultDto { Success = true, Message = "تعهدنامه با موفقیت امضا شد" });
    }

    [Authorize(Policy = "perm:admin.receipts.view")]
    [HttpGet("by-receipt/{receiptId:int}")]
    public async Task<ActionResult<DigitalCommitmentDto>> GetByReceipt(int receiptId)
    {
        var commitment = await _db.DigitalCommitments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CustomerReceiptId == receiptId);

        if (commitment == null)
            return NotFound();

        return Ok(MapDto(commitment));
    }

    [Authorize(Policy = "perm:admin.receipts.manage")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var commitment = await _db.DigitalCommitments.FindAsync(id);
        if (commitment == null)
            return NotFound();

        _db.DigitalCommitments.Remove(commitment);
        await _db.SaveChangesAsync();

        return Ok();
    }

    private bool IsWorkshop1()
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        if (isSuperAdmin) return true;
        try { return ClaimsHelper.GetWorkshopId(User) == 1; }
        catch { return false; }
    }

    private static DigitalCommitmentDto MapDto(DigitalCommitment x)
    {
        return new DigitalCommitmentDto
        {
            Id = x.Id,
            CustomerReceiptId = x.CustomerReceiptId,
            WorkshopId = x.WorkshopId,
            WorkshopName = x.WorkshopName,
            WorkshopLogoUrl = x.WorkshopLogoUrl,
            CustomerId = x.CustomerId,
            CustomerFullName = x.CustomerFullName,
            CustomerMobile = x.CustomerMobile,
            DeviceTypeName = x.DeviceTypeName,
            DeviceBrandName = x.DeviceBrandName,
            ReceiptNumber = x.ReceiptNumber,
            RegisteredAtFa = x.RegisteredAtFa,
            ExtraBodyText = x.ExtraBodyText,
            FullBodyText = x.FullBodyText,
            Status = x.Status,
            CreatedAt = x.CreatedAt,
            CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(x.CreatedAt, false),
            SignedAt = x.SignedAt,
            SignedAtFa = x.SignedAt != null ? PersianDateHelper.ToPersianDateTimeString(x.SignedAt.Value, true) : null,
            SenderLineNumber = x.SenderLineNumber,
            VerificationCode = x.VerificationCode
        };
    }
}
