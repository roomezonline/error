using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public enum MessengerEndpointStatus
{
    Unverified = 0,
    Verified = 1
}

public sealed class MessengerEndpoint
{
    public int Id { get; set; }

    public NotificationDeliveryChannel Channel { get; set; }

    public int AppUserId { get; set; }

    [Required, MaxLength(100)]
    public string ExternalId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? ExternalUserName { get; set; }

    public MessengerEndpointStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? VerifiedAt { get; set; }

    public DateTimeOffset? LastSentAt { get; set; }
}
