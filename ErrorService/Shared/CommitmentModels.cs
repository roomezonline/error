using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public static class CommitmentStatuses
{
    public const string Pending = "pending";
    public const string Signed = "signed";

    public static readonly string[] All = { Pending, Signed };
}

public sealed class DigitalCommitmentDto
{
    public int Id { get; set; }
    public int CustomerReceiptId { get; set; }
    public int WorkshopId { get; set; }
    public string WorkshopName { get; set; } = string.Empty;
    public string? WorkshopLogoUrl { get; set; }
    public int CustomerId { get; set; }
    public string CustomerFullName { get; set; } = string.Empty;
    public string CustomerMobile { get; set; } = string.Empty;
    public string DeviceTypeName { get; set; } = string.Empty;
    public string? DeviceBrandName { get; set; }
    public int ReceiptNumber { get; set; }
    public string RegisteredAtFa { get; set; } = string.Empty;
    public string? ExtraBodyText { get; set; }
    public string FullBodyText { get; set; } = string.Empty;
    public string Status { get; set; } = CommitmentStatuses.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedAtFa { get; set; } = string.Empty;
    public DateTimeOffset? SignedAt { get; set; }
    public string? SignedAtFa { get; set; }
    public string? SenderLineNumber { get; set; }
    public string? VerificationCode { get; set; }
}

public sealed class DigitalCommitmentCreateRequest
{
    [Required]
    public int CustomerReceiptId { get; set; }
    public string? ExtraBodyText { get; set; }
}

public sealed class CommitmentSearchRequest
{
    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود")]
    public string Mobile { get; set; } = string.Empty;
}

public sealed class CommitmentSignRequest
{
    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل باید ۱۱ رقم و با ۰۹ شروع شود")]
    public string Mobile { get; set; } = string.Empty;

    [Required(ErrorMessage = "شناسه تعهدنامه الزامی است")]
    public int CommitmentId { get; set; }

    [Required(ErrorMessage = "کد تایید الزامی است")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "کد تایید باید ۶ رقم باشد")]
    public string Code { get; set; } = string.Empty;
}
