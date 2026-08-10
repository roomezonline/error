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
[Route("api/online-admission")]
public sealed class OnlineAdmissionController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ISmsService _sms;
    private readonly IMemoryCache _cache;
    private const int WorkshopIdForOtp = 1;
    private const int OtpTemplateId = 969320;
    private const int TechnicianSmsTemplateId = 532056;
    private static readonly TimeSpan OtpExpiry = TimeSpan.FromMinutes(2);

    public OnlineAdmissionController(ErrorServiceDbContext db, IWebHostEnvironment env, ISmsService sms, IMemoryCache cache)
    {
        _db = db;
        _env = env;
        _sms = sms;
        _cache = cache;
    }

    [HttpGet("settings")]
    public async Task<ActionResult<AdmissionExpertiseSettingsDto>> GetSettings()
    {
        var settings = await _db.AdmissionExpertiseSettings.FirstOrDefaultAsync();
        var smsSettings = await _db.SmsSettings.FirstOrDefaultAsync();
        var bankAccounts = await _db.BankAccounts
            .Where(x => x.WorkshopId == 1 && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new AdmissionBankAccountDto
            {
                Id = x.Id,
                BankName = x.BankName ?? "",
                OwnerName = x.OwnerName ?? "",
                CardNumber = x.CardNumber,
                AccountNumber = x.AccountNumber,
                Iban = x.Iban,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return Ok(new AdmissionExpertiseSettingsDto
        {
            Amount = settings?.Amount ?? 0,
            HtmlDescription = settings?.HtmlDescription ?? "لطفاً مبلغ کارشناسی را به یکی از حساب‌های زیر واریز نموده و رسید را آپلود کنید.",
            IsEnabled = settings?.IsEnabled ?? true,
            OtpEnabled = smsSettings?.OtpEnabled ?? true,
            BankAccounts = bankAccounts
        });
    }

    [HttpPost("send-otp")]
    public async Task<ActionResult<OtpResultDto>> SendOtp([FromBody] SendOtpRequest request)
    {
        var smsSettings = await _db.SmsSettings.FirstOrDefaultAsync();
        if (smsSettings != null && !smsSettings.OtpEnabled)
            return Ok(new OtpResultDto { Success = false, Message = "ارسال کد تایید توسط مدیر سیستم غیرفعال شده است." });

        var code = Random.Shared.Next(100000, 999999).ToString();
        _cache.Set($"otp:{request.Mobile}", code, OtpExpiry);

        var result = await _sms.SendVerificationSmsAsync(WorkshopIdForOtp, request.Mobile, code, OtpTemplateId);

        if (result.Success == false)
        {
            _cache.Remove($"otp:{request.Mobile}");
            Console.Error.WriteLine($"[SmsService ERROR] {result.Message} — mobile: {request.Mobile}");
        }

        return Ok(result);
    }

    [HttpPost("verify-otp")]
    public ActionResult<OtpResultDto> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var cached = _cache.Get<string>($"otp:{request.Mobile}");
        if (cached == null)
            return Ok(new OtpResultDto { Success = false, Message = "کد تایید منقضی شده است. لطفاً مجدداً درخواست دهید." });

        if (cached != request.Code)
            return Ok(new OtpResultDto { Success = false, Message = "کد تایید اشتباه است" });

        return Ok(new OtpResultDto { Success = true });
    }

    [HttpPost("submit")]
    public async Task<ActionResult> SubmitRequest([FromBody] OnlineAdmissionCreateRequest request)
    {
        var smsSettings = await _db.SmsSettings.FirstOrDefaultAsync();
        if (smsSettings?.OtpEnabled != false)
        {
            var cached = _cache.Get<string>($"otp:{request.CustomerMobile}");
            if (cached == null || cached != request.VerificationCode)
                return BadRequest("کد تایید معتبر نیست");
        }

        var settings = await _db.AdmissionExpertiseSettings.FirstOrDefaultAsync();
        
        var entity = new OnlineAdmissionRequest
        {
            Type = request.Type,
            Status = OnlineAdmissionStatuses.Pending,
            CustomerFullName = request.CustomerFullName,
            CustomerMobile = request.CustomerMobile,
            CustomerAddress = request.CustomerAddress,
            ProvinceId = request.ProvinceId,
            CityId = request.CityId,
            DeviceTypeId = request.DeviceTypeId,
            DeviceBrandId = request.DeviceBrandId,
            ProblemDescription = request.ProblemDescription,
            DeviceImageUrl = request.DeviceImageUrl,
            ReceiptImageUrl = request.ReceiptImageUrl,
            ExpertiseAmount = request.Type == OnlineAdmissionTypes.Expertise ? (settings?.Amount ?? 0) : null,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.OnlineAdmissionRequests.Add(entity);
        await _db.SaveChangesAsync();

        if (smsSettings?.OtpEnabled != false)
            _cache.Remove($"otp:{request.CustomerMobile}");
        return Ok(new { id = entity.Id });
    }

    [Authorize(Policy = "perm:admin.onlineadmission.manage")] // فقط سوپر ادمین
    [HttpGet("admin/list")]
    public async Task<ActionResult<List<OnlineAdmissionDto>>> GetAdminList([FromQuery] string? type, [FromQuery] string? status)
    {
        var query = _db.OnlineAdmissionRequests
            .AsNoTracking()
            .Include(x => x.DeviceType)
            .Include(x => x.DeviceBrand)
            .Include(x => x.AssignedWorkshop)
            .Include(x => x.Province)
            .Include(x => x.City)
            .AsQueryable();

        if (!string.IsNullOrEmpty(type))
            query = query.Where(x => x.Type == type);
        
        if (!string.IsNullOrEmpty(status))
            query = query.Where(x => x.Status == status);

        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new OnlineAdmissionDto
            {
                Id = x.Id,
                Type = x.Type,
                Status = x.Status,
                CustomerFullName = x.CustomerFullName,
                CustomerMobile = x.CustomerMobile,
                CustomerAddress = x.CustomerAddress,
                ProvinceId = x.ProvinceId,
                ProvinceName = x.Province != null ? x.Province.Name : null,
                CityId = x.CityId,
                CityName = x.City != null ? x.City.Name : null,
                DeviceTypeId = x.DeviceTypeId,
                DeviceTypeName = x.DeviceType.Name,
                DeviceBrandId = x.DeviceBrandId,
                DeviceBrandName = x.DeviceBrand != null ? x.DeviceBrand.Name : null,
                ProblemDescription = x.ProblemDescription,
                DeviceImageUrl = x.DeviceImageUrl,
                ReceiptImageUrl = x.ReceiptImageUrl,
                ExpertiseAmount = x.ExpertiseAmount,
                AdminNote = x.AdminNote,
                AssignedWorkshopId = x.AssignedWorkshopId,
                AssignedWorkshopName = x.AssignedWorkshop != null ? x.AssignedWorkshop.WorkshopName : null,
                CreatedReceiptId = x.CreatedReceiptId,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    [Authorize(Policy = "perm:admin.onlineadmission.manage")]
    [HttpPost("admin/update-status")]
    public async Task<ActionResult> UpdateStatus(int id, string status, string? adminNote)
    {
        status = (status ?? string.Empty).Trim();
        if (!OnlineAdmissionStatuses.All.Contains(status))
            return BadRequest("وضعیت معتبر نیست");

        var entity = await _db.OnlineAdmissionRequests.FindAsync(id);
        if (entity == null) return NotFound();

        entity.Status = status;
        entity.AdminNote = adminNote;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        
        await _db.SaveChangesAsync();
        return Ok();
    }

    [Authorize(Policy = "perm:admin.onlineadmission.manage")]
    [HttpPost("admin/reject")]
    public async Task<ActionResult> Reject(int id, string adminNote)
    {
        if (string.IsNullOrWhiteSpace(adminNote))
            return BadRequest("یادداشت مدیر الزامی است");

        var entity = await _db.OnlineAdmissionRequests.FindAsync(id);
        if (entity == null) return NotFound();

        entity.Status = OnlineAdmissionStatuses.Rejected;
        entity.AdminNote = adminNote.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }

    [Authorize(Policy = "perm:admin.onlineadmission.manage")]
    [HttpPost("admin/delete")]
    public async Task<ActionResult> Delete(int id)
    {
        var entity = await _db.OnlineAdmissionRequests.FindAsync(id);
        if (entity == null) return NotFound();

        entity.Status = OnlineAdmissionStatuses.Deleted;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }

    [Authorize(Policy = "perm:admin.onlineadmission.manage")]
    [HttpPost("admin/restore")]
    public async Task<ActionResult> Restore(int id)
    {
        var entity = await _db.OnlineAdmissionRequests.FindAsync(id);
        if (entity == null) return NotFound();
        if (!string.Equals(entity.Status, OnlineAdmissionStatuses.Deleted, StringComparison.OrdinalIgnoreCase))
            return BadRequest("فقط درخواست‌های حذف‌شده قابل بازگردانی هستند");

        entity.Status = OnlineAdmissionStatuses.Pending;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }

    [Authorize(Policy = "perm:admin.onlineadmission.manage")]
    [HttpPost("admin/assign-repair")]
    public async Task<ActionResult> AssignRepair(int id, int workshopId, string? adminNote)
    {
        if (workshopId <= 0) return BadRequest("کارگاه معتبر نیست");

        var req = await _db.OnlineAdmissionRequests
            .Include(x => x.AssignedWorkshop)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (req == null) return NotFound();

        // Both expertise and repair can be assigned
        var isExpertise = string.Equals(req.Type, OnlineAdmissionTypes.Expertise, StringComparison.OrdinalIgnoreCase);
        var isRepair = string.Equals(req.Type, OnlineAdmissionTypes.Repair, StringComparison.OrdinalIgnoreCase);

        if (!isExpertise && !isRepair)
            return BadRequest("این درخواست قابل ارجاع نیست");

        var ws = await _db.Workshops.AsNoTracking().FirstOrDefaultAsync(x => x.Id == workshopId && x.IsActive);
        if (ws == null) return BadRequest("کارگاه فعال یافت نشد");

        // Upsert customer in target workshop by mobile
        var mobile = (req.CustomerMobile ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(mobile)) return BadRequest("شماره موبایل مشتری نامعتبر است");

        var customer = await _db.WorkshopCustomers
            .FirstOrDefaultAsync(x => x.WorkshopId == workshopId && x.Mobile == mobile);

        var fullName = (req.CustomerFullName ?? string.Empty).Trim();
        var firstName = fullName;
        var lastName = string.Empty;
        var lastSpace = fullName.LastIndexOf(' ');
        if (lastSpace > 0)
        {
            firstName = fullName.Substring(0, lastSpace).Trim();
            lastName = fullName.Substring(lastSpace + 1).Trim();
        }
        if (string.IsNullOrWhiteSpace(firstName)) firstName = fullName;
        if (string.IsNullOrWhiteSpace(lastName)) lastName = "-";

        if (customer == null)
        {
            customer = new Models.WorkshopCustomer
            {
                WorkshopId = workshopId,
                FirstName = firstName,
                LastName = lastName,
                Mobile = mobile,
                Address = req.CustomerAddress,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.WorkshopCustomers.Add(customer);
            await _db.SaveChangesAsync(); // Ensure customer has an ID
        }
        else
        {
            customer.FirstName = firstName;
            customer.LastName = lastName;
            customer.Address = req.CustomerAddress;
            customer.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }

        var note = (adminNote ?? req.AdminNote ?? string.Empty).Trim();

        var problem = (req.ProblemDescription ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(note))
        {
            problem += $"\n\n---\nیادداشت ارجاع 2:\n{note}";
        }

        // Copy device image to target workshop folder
        string? receiptImageUrl = null;
        if (!string.IsNullOrWhiteSpace(req.DeviceImageUrl))
        {
            receiptImageUrl = await CopyImageToWorkshopFolderAsync(req.DeviceImageUrl, workshopId);
        }

        var receipt = new CustomerReceipt
        {
            WorkshopId = workshopId,
            CustomerId = customer.Id, // explicitly set CustomerId
            DeviceTypeId = req.DeviceTypeId,
            DeviceBrandId = req.DeviceBrandId,
            ProblemDescription = problem,
            ReceiptImageUrl = receiptImageUrl,
            Status = CustomerReceiptStatuses.Pending,
            RegisteredAt = DateTimeOffset.UtcNow,
            CreatedByUserId = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.CustomerReceipts.Add(receipt);
        await _db.SaveChangesAsync(); // save receipt first to get ID

        req.Status = OnlineAdmissionStatuses.Verified;
        req.AdminNote = note;
        req.AssignedWorkshopId = workshopId;
        req.CreatedReceiptId = receipt.Id; // set ID explicitly
        req.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { receiptId = receipt.Id });
    }

    [Authorize(Policy = "perm:admin.onlineadmission.manage")]
    [HttpPost("admin/send-technician-sms")]
    public async Task<ActionResult> SendTechnicianSms(SendTechnicianSmsRequest request)
    {
        if (request.Recipients == null || request.Recipients.Count == 0)
            return BadRequest("گیرنده‌ای انتخاب نشده است");
        if (request.WorkshopId <= 0)
            return BadRequest("کارگاه معتبر نیست");

        var module = await _db.WorkshopSmsModules
            .FirstOrDefaultAsync(m => m.WorkshopId == request.WorkshopId && m.ModuleType == SmsModuleType.NewJobForTechnician);
        if (module != null && !module.IsActive)
            return BadRequest("ماژول پیامک برای این کارگاه فعال نیست");

            var ws = await _db.Workshops.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.WorkshopId);
            var workshopName = ws?.WorkshopName ?? "کارگاه";

        var results = new List<SendSmsSingleResult>();
        foreach (var r in request.Recipients)
        {
            var result = await _sms.SendTechnicianSmsAsync(
                request.WorkshopId,
                r.Mobile,
                r.TechnicianName,
                workshopName,
                TechnicianSmsTemplateId);
            results.Add(new SendSmsSingleResult
            {
                Mobile = r.Mobile,
                Success = result.Success,
                Message = result.Message
            });
        }

        return Ok(new SendTechnicianSmsResponse { Results = results });
    }

    private async Task<string?> CopyImageToWorkshopFolderAsync(string? sourceUrl, int targetWorkshopId)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            return null;

        try
        {
            // Parse source URL to get file path
            // Expected format: /uploads/customer-receipts/{sourceWorkshopId}/{filename}
            var parts = sourceUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 || !parts[0].Equals("uploads") || !parts[1].Equals("customer-receipts"))
                return sourceUrl; // Return original if format is unexpected

            var fileName = parts[^1]; // Get last part (filename)
            var sourcePath = Path.Combine(_env.WebRootPath, "uploads", "customer-receipts", parts[2], fileName);

            if (!System.IO.File.Exists(sourcePath))
                return sourceUrl; // Return original if source doesn't exist

            // Create target folder
            var targetFolder = Path.Combine(_env.WebRootPath, "uploads", "customer-receipts", targetWorkshopId.ToString());
            if (!Directory.Exists(targetFolder))
                Directory.CreateDirectory(targetFolder);

            // Copy file to target folder
            var targetPath = Path.Combine(targetFolder, fileName);
            await Task.Run(() => System.IO.File.Copy(sourcePath, targetPath, true));

            // Return new URL
            return $"/uploads/customer-receipts/{targetWorkshopId}/{fileName}";
        }
        catch
        {
            // If copy fails, return original URL
            return sourceUrl;
        }
    }

    [HttpPost("upload")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<string>> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
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

        // Upload to workshop 1 folder for online admission
        var targetWorkshopId = 1;
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

    [Authorize(Policy = "perm:admin.onlineadmission.manage")]
    [HttpPost("admin/settings")]
    public async Task<ActionResult> UpdateSettings(AdmissionExpertiseSettingsDto request)
    {
        var settings = await _db.AdmissionExpertiseSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new AdmissionExpertiseSetting();
            _db.AdmissionExpertiseSettings.Add(settings);
        }

        settings.Amount = request.Amount;
        settings.HtmlDescription = request.HtmlDescription;
        settings.IsEnabled = request.IsEnabled;

        await _db.SaveChangesAsync();
        return Ok();
    }
}
