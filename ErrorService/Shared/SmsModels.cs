using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class WorkshopSmsSettingDto
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public string WorkshopName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class WorkshopSmsSettingUpdateRequest
{
    public bool IsActive { get; set; }
}

public sealed class SmsSettingsDto
{
    public bool IsActive { get; set; }
    public bool OtpEnabled { get; set; }
    public decimal TariffPerSms { get; set; }
    public string? ApiKey { get; set; }
    public string? SenderNumber { get; set; }
    public int? MonthlyQuota { get; set; }
    public int? WarningThreshold { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class SmsSettingsUpdateRequest
{
    public bool IsActive { get; set; }
    public bool OtpEnabled { get; set; }
    [Range(0, 100000)] public decimal TariffPerSms { get; set; }
    public string? ApiKey { get; set; }
    public string? SenderNumber { get; set; }
    [Range(0, int.MaxValue)] public int? MonthlyQuota { get; set; }
    [Range(0, int.MaxValue)] public int? WarningThreshold { get; set; }
}

public sealed class WorkshopSmsCreditDto
{
    public int WorkshopId { get; set; }
    public string WorkshopName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public int SmsCount { get; set; }
}

public sealed class WorkshopSmsModuleDto
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public int ModuleType { get; set; }
    public string ModuleTitle { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? CustomTemplate { get; set; }
}

public sealed class WorkshopSmsModuleUpdateRequest
{
    public bool IsActive { get; set; }
    public int? ModuleType { get; set; }
    public string? CustomTemplate { get; set; }
}

public sealed class SmsChargeTransactionDto
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public string WorkshopName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int SmsCount { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFileName { get; set; }
    public string? ReceiptFilePath { get; set; }
    public int Status { get; set; }
    public string StatusTitle { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public sealed class SmsChargeRequest
{
    [Range(1000, 100_000_000)] public decimal Amount { get; set; }
    public string? Description { get; set; }
}

public sealed class SmsChargeSubmitRequest
{
    [Range(1000, 100_000_000)] public decimal Amount { get; set; }
    public string? Description { get; set; }
}

public sealed class SmsChargeReviewRequest
{
    public bool Approved { get; set; }
    public string? Description { get; set; }
    public string? ReviewNote { get; set; }
}

public sealed class SmsLogDto
{
    public long Id { get; set; }
    public int WorkshopId { get; set; }
    public string WorkshopName { get; set; } = string.Empty;
    public string? ModuleTitle { get; set; }
    public string? ModuleType { get; set; }
    public string? RecipientNumber { get; set; }
    public string? MessageText { get; set; }
    public string SendStatus { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public decimal Cost { get; set; }
    public int DeliveryStatus { get; set; }
    public string? ProviderMessageId { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ProviderRawStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SmsLogFilterRequest
{
    public int? WorkshopId { get; set; }
    public int? ModuleType { get; set; }
    public int? SendStatus { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class SmsDashboardDto
{
    public int TotalActiveWorkshops { get; set; }
    public decimal TotalBalance { get; set; }
    public int TodaySentCount { get; set; }
    public int PendingChargeCount { get; set; }
    public decimal MonthlyCost { get; set; }
}

public sealed class DeliveryCheckBatchResult
{
    public int Checked { get; set; }
    public int Updated { get; set; }
    public int Errors { get; set; }
}

public sealed class WorkshopSmsDashboardDto
{
    public decimal Balance { get; set; }
    public int SmsCount { get; set; }
    public int TodaySentCount { get; set; }
    public int PendingChargeCount { get; set; }
    public decimal MonthlyCost { get; set; }
    public bool SmsActive { get; set; }
    public int? WarningThreshold { get; set; }
}
