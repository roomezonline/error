using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public enum NotificationSeverity
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Critical = 3
}

public enum NotificationDeliveryChannel
{
    Internal = 0,
    Bale = 1,
    Telegram = 2,
    Eitaa = 3
}

public enum NotificationDeliveryStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Skipped = 3
}

public sealed class Notification
{
    public long Id { get; set; }

    [Required, MaxLength(120)]
    public string EventType { get; set; } = "manual";

    [Required, MaxLength(250)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public int? WorkshopId { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsBroadcast { get; set; }
    public string? ActionUrl { get; set; }
    public string? IdempotencyKey { get; set; }

    public ICollection<NotificationRecipient> Recipients { get; set; } = new List<NotificationRecipient>();
    public ICollection<NotificationDelivery> Deliveries { get; set; } = new List<NotificationDelivery>();
}

public sealed class NotificationRecipient
{
    public long Id { get; set; }
    public long NotificationId { get; set; }
    public Notification Notification { get; set; } = default!;

    public int? AppUserId { get; set; }
    public int? WorkshopUserId { get; set; }
    public int? WorkshopCustomerId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? HiddenAt { get; set; }
}

public sealed class NotificationRule
{
    public long Id { get; set; }

    [Required, MaxLength(120)]
    public string EventType { get; set; } = string.Empty;

    [Required, MaxLength(250)]
    public string Group { get; set; } = string.Empty;

    [Required, MaxLength(250)]
    public string Label { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public NotificationSeverity? Severity { get; set; }

    [Required, MaxLength(250)]
    public string TitleTemplate { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string BodyTemplate { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ActionUrlTemplate { get; set; }

    public bool BroadcastToAdmins { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int? UpdatedByUserId { get; set; }
}

public sealed class NotificationDelivery
{
    public long Id { get; set; }
    public long NotificationId { get; set; }
    public Notification Notification { get; set; } = default!;
    public long? RecipientId { get; set; }
    public NotificationDeliveryChannel Channel { get; set; }
    public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? ExternalMessageId { get; set; }
    public string? ErrorMessage { get; set; }
}
