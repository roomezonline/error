using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ErrorService.Server.Services;

/// <summary>
/// کد تایید پیامکی چندمنظوره با تنظیمات قابل کنترل توسط ادمین.
/// کلید کش به مصرف (purpose) و شماره موبایل وابسته است تا کد ارسال‌شده برای
/// یک فرم در فرم دیگر قابل استفاده نباشد.
/// </summary>
public sealed class OtpService
{
    private const int WorkshopIdForOtp = 1;
    private const int OtpTemplateId = 969320;

    private readonly ErrorServiceDbContext _db;
    private readonly ISmsService _sms;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OtpService> _logger;

    public OtpService(ErrorServiceDbContext db, ISmsService sms, IMemoryCache cache, ILogger<OtpService> logger)
    {
        _db = db;
        _sms = sms;
        _cache = cache;
        _logger = logger;
    }

    public async Task<OtpRequirementsDto> GetRequirementsAsync()
    {
        var settings = await _db.SmsSettings.AsNoTracking().FirstOrDefaultAsync();
        var master = settings?.OtpEnabled ?? true;

        return new OtpRequirementsDto
        {
            MasterEnabled = master,
            Registration = master && (settings?.OtpRegistrationEnabled ?? false),
            PasswordReset = master && (settings?.OtpPasswordResetEnabled ?? true),
            AdmissionRepair = master && (settings?.OtpAdmissionRepairEnabled ?? true),
            AdmissionExpertise = master && (settings?.OtpAdmissionExpertiseEnabled ?? true),
            AdmissionTracking = master && (settings?.OtpAdmissionTrackingEnabled ?? true),
            CodeTtlSeconds = Math.Clamp(settings?.OtpCodeTtlSeconds ?? 120, 60, 600),
            ResendCooldownSeconds = Math.Clamp(settings?.OtpResendCooldownSeconds ?? 60, 0, 600)
        };
    }

    public async Task<bool> IsRequiredAsync(string purpose)
        => (await GetRequirementsAsync()).IsRequired(purpose);

    public async Task<OtpResultDto> SendAsync(string mobile, string purpose)
    {
        mobile = (mobile ?? string.Empty).Trim();
        purpose = (purpose ?? string.Empty).Trim().ToLowerInvariant();

        if (!OtpPurposes.IsValid(purpose))
            return Fail("نوع درخواست کد تایید نامعتبر است");

        var settings = await _db.SmsSettings.AsNoTracking().FirstOrDefaultAsync();
        if (settings != null && !settings.IsActive)
            return Fail("سامانه پیامک غیرفعال است. لطفاً بعداً تلاش کنید.");

        var requirements = await GetRequirementsAsync();
        if (!requirements.IsRequired(purpose))
            return Fail("ارسال کد تایید برای این بخش توسط مدیر سیستم غیرفعال شده است.");

        var cooldown = TimeSpan.FromSeconds(requirements.ResendCooldownSeconds);
        if (cooldown > TimeSpan.Zero && _cache.TryGetValue(CooldownKey(purpose, mobile), out _))
            return Fail("کد تایید قبلی ارسال شده است؛ لطفاً کمی صبر کنید.");

        var code = Random.Shared.Next(100000, 999999).ToString();
        var ttl = TimeSpan.FromSeconds(requirements.CodeTtlSeconds);

        _cache.Set(CodeKey(purpose, mobile), code, ttl);
        _cache.Set(AttemptsKey(purpose, mobile), 0, ttl);

        var smsResult = await _sms.SendVerificationSmsAsync(WorkshopIdForOtp, mobile, code, OtpTemplateId);
        if (!smsResult.Success)
        {
            _cache.Remove(CodeKey(purpose, mobile));
            _cache.Remove(AttemptsKey(purpose, mobile));
            _logger.LogWarning("OTP SMS failed for purpose {Purpose}: {Message}", purpose, smsResult.Message);
            return smsResult;
        }

        if (cooldown > TimeSpan.Zero)
            _cache.Set(CooldownKey(purpose, mobile), true, cooldown);

        return new OtpResultDto { Success = true, Message = "کد تایید ارسال شد", LineNumber = smsResult.LineNumber };
    }

    public async Task<OtpVerifyResultDto> VerifyAsync(string mobile, string purpose, string? code, bool consume)
    {
        mobile = (mobile ?? string.Empty).Trim();
        purpose = (purpose ?? string.Empty).Trim().ToLowerInvariant();

        if (!OtpPurposes.IsValid(purpose))
            return new OtpVerifyResultDto { Success = false, Message = "نوع درخواست کد تایید نامعتبر است" };

        var requirements = await GetRequirementsAsync();
        if (!requirements.IsRequired(purpose))
            return new OtpVerifyResultDto { Success = false, Message = "کد تایید برای این بخش غیرفعال است" };

        var cached = _cache.Get<string>(CodeKey(purpose, mobile));
        if (cached == null)
            return new OtpVerifyResultDto
            {
                Success = false,
                Expired = true,
                Message = "کد تایید منقضی شده است. لطفاً مجدداً درخواست دهید."
            };

        if (!string.Equals(cached, (code ?? string.Empty).Trim(), StringComparison.Ordinal))
        {
            var settings = await _db.SmsSettings.AsNoTracking().FirstOrDefaultAsync();
            var maxAttempts = Math.Clamp(settings?.OtpMaxAttempts ?? 5, 1, 20);
            var attempts = _cache.Get<int?>(AttemptsKey(purpose, mobile)) ?? 0;
            attempts++;
            _cache.Set(AttemptsKey(purpose, mobile), attempts, TimeSpan.FromSeconds(requirements.CodeTtlSeconds));

            if (attempts >= maxAttempts)
            {
                _cache.Remove(CodeKey(purpose, mobile));
                _cache.Remove(AttemptsKey(purpose, mobile));
                return new OtpVerifyResultDto
                {
                    Success = false,
                    Expired = true,
                    Message = "تعداد تلاش‌های ناموفق بیش از حد مجاز شد. لطفاً کد جدید دریافت کنید."
                };
            }

            return new OtpVerifyResultDto { Success = false, Message = "کد تایید اشتباه است" };
        }

        if (consume)
        {
            _cache.Remove(CodeKey(purpose, mobile));
            _cache.Remove(AttemptsKey(purpose, mobile));
            _cache.Remove(CooldownKey(purpose, mobile));
        }

        return new OtpVerifyResultDto { Success = true, Message = "کد تایید معتبر است" };
    }

    private static OtpResultDto Fail(string message) => new() { Success = false, Message = message };

    private static string CodeKey(string purpose, string mobile) => $"otp:{purpose}:{mobile}";
    private static string AttemptsKey(string purpose, string mobile) => $"otp:attempts:{purpose}:{mobile}";
    private static string CooldownKey(string purpose, string mobile) => $"otp:cooldown:{purpose}:{mobile}";
}
