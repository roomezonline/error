using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public static class OnlineAdmissionStatuses
{
    public const string Pending = "pending";
    public const string Verified = "verified";
    public const string Processing = "processing";
    public const string Done = "done";
    public const string Canceled = "canceled";
    public const string Rejected = "rejected";
    public const string Deleted = "deleted";

    public static readonly string[] All = { Pending, Verified, Processing, Done, Canceled, Rejected, Deleted };
}

public static class OnlineAdmissionTypes
{
    public const string Expertise = "expertise";
    public const string Repair = "repair";

    public static readonly string[] All = { Expertise, Repair };
}

public sealed class OnlineAdmissionDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public string CustomerFullName { get; set; } = string.Empty;
    public string CustomerMobile { get; set; } = string.Empty;
    public string? CustomerAddress { get; set; }

    public int? ProvinceId { get; set; }
    public string? ProvinceName { get; set; }
    public int? CityId { get; set; }
    public string? CityName { get; set; }

    public int DeviceTypeId { get; set; }
    public string DeviceTypeName { get; set; } = string.Empty;
    public int? DeviceBrandId { get; set; }
    public string? DeviceBrandName { get; set; }

    public string ProblemDescription { get; set; } = string.Empty;
    public string? DeviceImageUrl { get; set; }
    public string? ReceiptImageUrl { get; set; }
    
    public long? ExpertiseAmount { get; set; }
    public string? AdminNote { get; set; }

    public int? AssignedWorkshopId { get; set; }
    public string? AssignedWorkshopName { get; set; }
    public int? CreatedReceiptId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class OnlineAdmissionCreateRequest
{
    [Required]
    public string Type { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
    [MinLength(3, ErrorMessage = "نام وارد شده خیلی کوتاه است")]
    public string CustomerFullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود")]
    public string CustomerMobile { get; set; } = string.Empty;

    [Required(ErrorMessage = "آدرس جهت مراجعه الزامی است")]
    [MinLength(10, ErrorMessage = "آدرس باید دقیق‌تر وارد شود")]
    public string? CustomerAddress { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب استان الزامی است")]
    public int ProvinceId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب شهر الزامی است")]
    public int CityId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب نوع دستگاه الزامی است")]
    public int DeviceTypeId { get; set; }

    public int? DeviceBrandId { get; set; }

    [Required(ErrorMessage = "توضیحات مشکل الزامی است")]
    public string ProblemDescription { get; set; } = string.Empty;

    public string? DeviceImageUrl { get; set; }
    public string? ReceiptImageUrl { get; set; }
    
    public string? VerificationCode { get; set; }
}

public sealed class SendOtpRequest
{
    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود")]
    public string Mobile { get; set; } = string.Empty;
}

public sealed class VerifyOtpRequest
{
    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود")]
    public string Mobile { get; set; } = string.Empty;

    [Required(ErrorMessage = "کد تایید الزامی است")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "کد تایید باید ۶ رقم باشد")]
    public string Code { get; set; } = string.Empty;
}

public sealed class OtpResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? LineNumber { get; set; }
}

public sealed class AdmissionExpertiseSettingsDto
{
    public long Amount { get; set; }
    public string? HtmlDescription { get; set; }
    public bool IsEnabled { get; set; }
    public bool OtpEnabled { get; set; }
    public List<AdmissionBankAccountDto> BankAccounts { get; set; } = new();
}

public sealed class AdmissionBankAccountDto
{
    public int Id { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? CardNumber { get; set; }
    public string? AccountNumber { get; set; }
    public string? Iban { get; set; }
    public bool IsActive { get; set; }
}

public sealed class SendTechnicianSmsRequest
{
    public int WorkshopId { get; set; }
    public List<SmsRecipientDto> Recipients { get; set; } = new();
}

public sealed class SmsRecipientDto
{
    public string Mobile { get; set; } = string.Empty;
    public string TechnicianName { get; set; } = string.Empty;
}

public sealed class SendTechnicianSmsResponse
{
    public List<SendSmsSingleResult> Results { get; set; } = new();
}

public sealed class SendSmsSingleResult
{
    public string Mobile { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
}
