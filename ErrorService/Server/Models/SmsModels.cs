using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class WorkshopSmsSetting
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class SmsSettings
{
    public int Id { get; set; }
    public bool IsActive { get; set; } = true;
    public bool OtpEnabled { get; set; } = true;
    public decimal TariffPerSms { get; set; } = 0;
    [MaxLength(500)] public string? ApiKey { get; set; }
    [MaxLength(50)] public string? SenderNumber { get; set; }
    public int? MonthlyQuota { get; set; }
    public int? WarningThreshold { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class WorkshopSmsCredit
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = null!;
    public decimal Balance { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum SmsModuleType
{
    OrderRegistered = 1,
    OrderReadyForDelivery = 2,
    OrderUnrepairable = 3,
    NewJobForTechnician = 4,
    ActivationCode = 5
}

public enum SmsDeliveryStatus
{
    Pending = 0,
    SentToTelecom = 1,
    Delivered = 2,
    NotDelivered = 3
}

public sealed class WorkshopSmsModule
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = null!;
    public SmsModuleType ModuleType { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CustomTemplate { get; set; }
}

public enum SmsChargeStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public sealed class SmsChargeTransaction
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = null!;
    public decimal Amount { get; set; }
    public int SmsCount { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFileName { get; set; }
    public string? ReceiptFilePath { get; set; }
    public SmsChargeStatus Status { get; set; } = SmsChargeStatus.Pending;
    public int? ReviewedByUserId { get; set; }
    public string? ReviewedByUserName { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}

public enum SmsSendStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

public sealed class SmsLog
{
    public long Id { get; set; }
    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = null!;
    public int? CustomerReceiptId { get; set; }
    public SmsModuleType? ModuleType { get; set; }
    [Required][MaxLength(20)] public string RecipientNumber { get; set; } = string.Empty;
    [Required] public string MessageText { get; set; } = string.Empty;
    public SmsSendStatus SendStatus { get; set; }
    [MaxLength(100)] public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal Cost { get; set; }
    public SmsDeliveryStatus DeliveryStatus { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ProviderRawStatus { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
