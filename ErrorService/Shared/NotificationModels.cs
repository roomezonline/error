namespace ErrorService.Shared;

public sealed class NotificationDto
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int Severity { get; set; }
    public string SeverityTitle { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public int RecipientCount { get; set; }
    public bool IsBroadcast { get; set; }
    public string? CreatedByName { get; set; }
}

public sealed class NotificationSummaryDto
{
    public int UnreadCount { get; set; }
    public int PendingDeliveryCount { get; set; }
    public int FailedDeliveryCount { get; set; }
    public int TodayCount { get; set; }
}

public sealed class NotificationUpdateRequest
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int Severity { get; set; }
    public string? ActionUrl { get; set; }
}

public sealed class NotificationCreateRequest
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int Severity { get; set; }
    public string? ActionUrl { get; set; }
    public int? WorkshopId { get; set; }
    public bool IncludeWorkshopUsers { get; set; }
    public bool IncludeSuperAdmins { get; set; }
    public bool IncludeAllUsers { get; set; }
    public List<string> PhoneNumbers { get; set; } = new();
    public List<int> PurchasedProductIds { get; set; } = new();
    public bool BroadcastToAdmins { get; set; }
}

public sealed class NotificationRuleDto
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int? Severity { get; set; }
    public string TitleTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public string? ActionUrlTemplate { get; set; }
    public bool BroadcastToAdmins { get; set; }
    public List<string> Placeholders { get; set; } = new();
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class NotificationRuleUpdateRequest
{
    public bool IsEnabled { get; set; } = true;
    public int? Severity { get; set; }
    public string TitleTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public string? ActionUrlTemplate { get; set; }
    public bool BroadcastToAdmins { get; set; }
}
