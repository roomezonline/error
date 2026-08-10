using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class MonitoringDevice
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string DeviceNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MonitoringDeviceAssignment
{
    public int Id { get; set; }

    public int MonitoringDeviceId { get; set; }
    public MonitoringDevice MonitoringDevice { get; set; } = default!;

    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = default!;

    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MonitoringReceiptConnection
{
    public int Id { get; set; }

    public int CustomerReceiptId { get; set; }
    public CustomerReceipt CustomerReceipt { get; set; } = default!;

    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = default!;

    public int MonitoringDeviceId { get; set; }
    public MonitoringDevice MonitoringDevice { get; set; } = default!;

    public int? CreatedByUserId { get; set; }
    [MaxLength(200)]
    public string? CreatedByUserName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedAt { get; set; }

    [MaxLength(2000)]
    public string? EndReason { get; set; }

    public DateTimeOffset? GracePeriodEndAt { get; set; }
}

public sealed class MonitoringDeviceChangeLog
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string DeviceCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ChangeType { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? OldValue { get; set; }

    [MaxLength(50)]
    public string? NewValue { get; set; }

    [Required]
    [MaxLength(200)]
    public string ChangeDescription { get; set; } = string.Empty;

    [Required]
    public string DataSnapshot { get; set; } = string.Empty;

    public int? MonitoringId { get; set; }

    public int? CustomerReceiptId { get; set; }

    public int? WorkshopId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(30)]
    public string CreatedAtFa { get; set; } = string.Empty;
}

public sealed class MonitoringDataRecord
{
    public long Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string DeviceCode { get; set; } = string.Empty;

    public int? MonitoringId { get; set; }

    public int? CustomerReceiptId { get; set; }
    public CustomerReceipt? CustomerReceipt { get; set; }

    public float? TemperatureRef { get; set; }
    public float? TemperatureFreez { get; set; }
    public float? TemperatureEnv { get; set; }

    public bool MotorState { get; set; }
    public bool Bargh { get; set; }
    public bool Element1 { get; set; }
    public bool Element2 { get; set; }
    public bool Fdc1 { get; set; }
    public bool Fac1 { get; set; }

    public float? Jaryan { get; set; }
    public float? Power { get; set; }
    public float? Kw { get; set; }
    public float? SumKw { get; set; }

    [MaxLength(50)]
    public string? State { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(30)]
    public string TimestampFa { get; set; } = string.Empty;
}

public sealed class MonitoringDataArchive
{
    public int Id { get; set; }

    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = default!;

    public int MonitoringReceiptConnectionId { get; set; }
    public MonitoringReceiptConnection MonitoringReceiptConnection { get; set; } = default!;

    public int? CustomerReceiptId { get; set; }

    [Required]
    [MaxLength(50)]
    public string DeviceCode { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime EndedAt { get; set; }

    public int RecordCount { get; set; }

    public long EstimatedBytes { get; set; }

    public bool Downloaded { get; set; }

    public DateTime? DownloadedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class CachedCsvData
{
    [Key]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Data { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class MonitoringAlert
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string DeviceCode { get; set; } = string.Empty;

    public int? MonitoringId { get; set; }

    public int? CustomerReceiptId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    public int Num { get; set; }

    [Required]
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Command { get; set; } = string.Empty;

    public DateTime Time { get; set; }

    public bool State { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class MonitoringShareLink
{
    public int Id { get; set; }

    public int MonitoringId { get; set; }

    public int WorkshopId { get; set; }

    public int? CustomerReceiptId { get; set; }

    [Required]
    [MaxLength(50)]
    public string DeviceCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string Token { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAt { get; set; }
}

public sealed class ShareLinkViewerSession
{
    public int Id { get; set; }

    public int ShareLinkId { get; set; }

    [Required]
    [MaxLength(64)]
    public string SessionId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [MaxLength(200)]
    public string? DeviceInfo { get; set; }

    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;

    public DateTime LastPingAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExitedAt { get; set; }

    public int PingCount { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class MonitoringPlan
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public int Months { get; set; }

    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class MonitoringRenewalRequest
{
    public int Id { get; set; }

    public int AssignmentId { get; set; }
    public MonitoringDeviceAssignment Assignment { get; set; } = default!;

    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = default!;

    public int PlanMonths { get; set; }

    public int? MonitoringPlanId { get; set; }
    public MonitoringPlan? MonitoringPlan { get; set; }

    public decimal PriceAtRequest { get; set; }

    [MaxLength(500)]
    public string? PaymentReceiptUrl { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    public RenewalRequestStatus Status { get; set; } = RenewalRequestStatus.Pending;

    [MaxLength(1000)]
    public string? AdminNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? HandledAt { get; set; }
}

public enum RenewalRequestStatus
{
    Pending,
    Approved,
    Rejected
}
