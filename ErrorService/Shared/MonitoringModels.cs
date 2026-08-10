using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class MonitoringDeviceDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DeviceNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class MonitoringDeviceUpsertRequest
{
    [Required(ErrorMessage = "عنوان الزامی است")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره دستگاه الزامی است")]
    [MaxLength(50)]
    public string DeviceNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class MonitoringDeviceAssignmentDto
{
    public int Id { get; set; }
    public int MonitoringDeviceId { get; set; }
    public string MonitoringDeviceTitle { get; set; } = string.Empty;
    public string MonitoringDeviceNumber { get; set; } = string.Empty;

    public int WorkshopId { get; set; }
    public string WorkshopName { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }

    public string StartDateFa { get; set; } = string.Empty;
    public string EndDateFa { get; set; } = string.Empty;

    public bool IsActiveNow { get; set; }
}

public sealed class MonitoringDeviceAssignRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب دستگاه الزامی است")]
    public int MonitoringDeviceId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب کارگاه الزامی است")]
    public int WorkshopId { get; set; }

    [Required(ErrorMessage = "تاریخ شروع الزامی است")]
    public string StartDateFa { get; set; } = string.Empty;

    public string? EndDateFa { get; set; }
}

public sealed class MonitoringReceiptConnectRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب رسید الزامی است")]
    public int CustomerReceiptId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب دستگاه مانیتورینگ الزامی است")]
    public int MonitoringDeviceId { get; set; }
}

public sealed class MonitoringReceiptEndRequest
{
    [MaxLength(2000)]
    public string? EndReason { get; set; }
}

public sealed class SaveSettingRequest
{
    public string Code { get; set; } = "";
    public int MotorMax { get; set; }
    public int MotorMin { get; set; }
    public int ElementMax { get; set; }
    public int ElementMin { get; set; }
    public bool Simultaneity { get; set; } = true;
    public int TimeoutMinutes { get; set; } = 2;
    public bool AmbientTempAlarmEnabled { get; set; } = true;
    public int AmbientTempThreshold { get; set; } = 35;
}

public sealed class MonitoringReceiptConnectionDto
{
    public int Id { get; set; }
    public int CustomerReceiptId { get; set; }
    public int WorkshopId { get; set; }
    public string WorkshopName { get; set; } = string.Empty;

    public int MonitoringDeviceId { get; set; }
    public string MonitoringDeviceTitle { get; set; } = string.Empty;
    public string MonitoringDeviceNumber { get; set; } = string.Empty;

    public string CustomerFullName { get; set; } = string.Empty;
    public string CustomerMobile { get; set; } = string.Empty;
    public string CustomerDeviceTitle { get; set; } = string.Empty;

    public string ProblemDescription { get; set; } = string.Empty;

    public string? ReceiptImageUrl { get; set; }

    public int? CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedAtFa { get; set; } = string.Empty;

    public DateTimeOffset? EndedAt { get; set; }
    public string? EndedAtFa { get; set; }

    public bool IsActive { get; set; }

    public string? EndReason { get; set; }
    public bool IsPoweredOn { get; set; }
    public int AlertCount { get; set; }
}

public sealed class MonitoringArchiveDto
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public string WorkshopName { get; set; } = string.Empty;
    public int MonitoringReceiptConnectionId { get; set; }
    public string DeviceCode { get; set; } = string.Empty;
    public string DeviceTitle { get; set; } = string.Empty;

    public string CustomerFullName { get; set; } = string.Empty;
    public string CustomerMobile { get; set; } = string.Empty;
    public string CustomerDeviceTitle { get; set; } = string.Empty;

    public string StartedAtFa { get; set; } = string.Empty;
    public string EndedAtFa { get; set; } = string.Empty;

    public int RecordCount { get; set; }
    public long EstimatedBytes { get; set; }
    public string EstimatedSizeText { get; set; } = string.Empty;

    public bool Downloaded { get; set; }
    public string? DownloadedAtFa { get; set; }

    public string CreatedAtFa { get; set; } = string.Empty;
    public int AgeInDays { get; set; }
}

public sealed class PendingArchiveCountDto
{
    public int Count { get; set; }
}

public class AlertDto
{
    public int Id { get; set; }
    public string DeviceCode { get; set; } = "";
    public int? MonitoringId { get; set; }
    public string Type { get; set; } = "";
    public int Num { get; set; }
    public string Message { get; set; } = "";
    public string Command { get; set; } = "";
    public DateTime Time { get; set; }
    public bool State { get; set; }
}

// ── Monitoring Plan Models ──

public sealed class MonitoringPlanDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Months { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public string CreatedAtFa { get; set; } = string.Empty;
}

public sealed class MonitoringPlanUpsertRequest
{
    [Required(ErrorMessage = "عنوان پلن الزامی است")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "تعداد ماه الزامی است")]
    [Range(1, 120, ErrorMessage = "تعداد ماه باید بین ۱ تا ۱۲۰ باشد")]
    public int Months { get; set; }

    [Required(ErrorMessage = "قیمت الزامی است")]
    [Range(0, 1_000_000_000, ErrorMessage = "قیمت معتبر نیست")]
    public decimal Price { get; set; }
}

// ── Monitoring Renewal Models ──

public sealed class MyDeviceDto
{
    public int AssignmentId { get; set; }
    public int DeviceId { get; set; }
    public string DeviceTitle { get; set; } = string.Empty;
    public string DeviceNumber { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public string StartDateFa { get; set; } = string.Empty;
    public string EndDateFa { get; set; } = string.Empty;
    public bool IsActiveNow { get; set; }
    public int? RemainingDays { get; set; }
    public bool IsExpiringSoon { get; set; } // within 30 days
    public bool HasPendingRenewal { get; set; }
}

public sealed class RenewalRequestDto
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public string? DeviceTitle { get; set; }
    public string? DeviceNumber { get; set; }
    public int WorkshopId { get; set; }
    public string? WorkshopName { get; set; }
    public int PlanMonths { get; set; }
    public string? PlanTitle { get; set; }
    public decimal PriceAtRequest { get; set; }
    public string? PaymentReceiptUrl { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "Pending";
    public string? AdminNote { get; set; }
    public string CreatedAtFa { get; set; } = string.Empty;
    public string? HandledAtFa { get; set; }
}

public sealed class RenewalRequestActionDto
{
    [Required(ErrorMessage = "نتیجه الزامی است")]
    public string Status { get; set; } = "Approved";
    [MaxLength(1000)]
    public string? AdminNote { get; set; }
}

public sealed class ExpiringDeviceItem
{
    public int AssignmentId { get; set; }
    public string DeviceTitle { get; set; } = string.Empty;
    public string DeviceNumber { get; set; } = string.Empty;
    public string EndDateFa { get; set; } = string.Empty;
    public int? RemainingDays { get; set; }
}

public sealed class ExpiringSummaryDto
{
    public int ExpiredCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public List<ExpiringDeviceItem> ExpiredDevices { get; set; } = new();
    public List<ExpiringDeviceItem> ExpiringSoonDevices { get; set; } = new();
}
