using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

/// <summary>مصرف‌های کد تایید؛ هر مصرف می‌تواند مستقلاً در تنظیمات ادمین فعال/غیرفعال شود.</summary>
public static class OtpPurposes
{
    public const string Registration = "registration";
    public const string PasswordReset = "password_reset";
    public const string AdmissionRepair = "admission_repair";
    public const string AdmissionExpertise = "admission_expertise";
    public const string AdmissionTracking = "admission_tracking";

    public static readonly string[] All =
    {
        Registration, PasswordReset, AdmissionRepair, AdmissionExpertise, AdmissionTracking
    };

    public static bool IsValid(string? purpose) =>
        !string.IsNullOrWhiteSpace(purpose) &&
        All.Contains(purpose.Trim().ToLowerInvariant(), StringComparer.Ordinal);
}

public sealed class OtpRequirementsDto
{
    public bool MasterEnabled { get; set; }
    public bool Registration { get; set; }
    public bool PasswordReset { get; set; }
    public bool AdmissionRepair { get; set; }
    public bool AdmissionExpertise { get; set; }
    public bool AdmissionTracking { get; set; }
    public int CodeTtlSeconds { get; set; } = 120;
    public int ResendCooldownSeconds { get; set; } = 60;

    public bool IsRequired(string purpose) => purpose switch
    {
        OtpPurposes.Registration => Registration,
        OtpPurposes.PasswordReset => PasswordReset,
        OtpPurposes.AdmissionRepair => AdmissionRepair,
        OtpPurposes.AdmissionExpertise => AdmissionExpertise,
        OtpPurposes.AdmissionTracking => AdmissionTracking,
        _ => false
    };
}

public sealed class OtpVerifyResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public bool Expired { get; set; }
}

public sealed class SendOtpByPurposeRequest
{
    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود")]
    public string Mobile { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع کد تایید الزامی است")]
    public string Purpose { get; set; } = string.Empty;
}

public sealed class VerifyOtpByPurposeRequest
{
    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود")]
    public string Mobile { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع کد تایید الزامی است")]
    public string Purpose { get; set; } = string.Empty;

    [Required(ErrorMessage = "کد تایید الزامی است")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "کد تایید باید ۶ رقم باشد")]
    public string Code { get; set; } = string.Empty;
}

/// <summary>تنظیمات عمومی احراز هویت که صفحه‌های ثبت‌نام/بازیابی رمز بر اساس آن فرم را نشان می‌دهند.</summary>
public sealed class AuthPublicConfigDto
{
    public bool RegistrationOtpEnabled { get; set; }
    public bool PasswordResetOtpEnabled { get; set; }
    public int CodeTtlSeconds { get; set; } = 120;
    public int ResendCooldownSeconds { get; set; } = 60;
}

public sealed class PasswordResetSendRequest
{
    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود")]
    public string PhoneNumber { get; set; } = string.Empty;
}

public sealed class PasswordResetRequest
{
    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "کد تایید الزامی است")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "کد تایید باید ۶ رقم باشد")]
    public string VerificationCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور جدید الزامی است")]
    [MinLength(4, ErrorMessage = "رمز عبور کوتاه است")]
    public string NewPassword { get; set; } = string.Empty;
}
