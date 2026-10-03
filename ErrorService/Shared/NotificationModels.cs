namespace ErrorService.Shared;

public static class NotificationChannelFlags
{
    public const int Internal = 1;
    public const int Bale = 2;
    public const int Telegram = 4;
    public const int Eitaa = 8;
    public const int All = Internal | Bale | Telegram | Eitaa;

    public static int FlagFor(int deliveryChannel) => deliveryChannel switch
    {
        1 => Bale,
        2 => Telegram,
        3 => Eitaa,
        _ => Internal
    };
}

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

public sealed class NotificationRecipientInfoDto
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Kind { get; set; } = "user";
    public string? RoleTitle { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
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
    public int Channels { get; set; } = NotificationChannelFlags.Internal;

    public bool BaleEnabled
    {
        get => (Channels & NotificationChannelFlags.Bale) != 0;
        set => Channels = value ? Channels | NotificationChannelFlags.Bale : Channels & ~NotificationChannelFlags.Bale;
    }

    public bool TelegramEnabled
    {
        get => (Channels & NotificationChannelFlags.Telegram) != 0;
        set => Channels = value ? Channels | NotificationChannelFlags.Telegram : Channels & ~NotificationChannelFlags.Telegram;
    }

    public bool EitaaEnabled
    {
        get => (Channels & NotificationChannelFlags.Eitaa) != 0;
        set => Channels = value ? Channels | NotificationChannelFlags.Eitaa : Channels & ~NotificationChannelFlags.Eitaa;
    }
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
    public int Channels { get; set; } = NotificationChannelFlags.Internal;
    public List<string> Placeholders { get; set; } = new();
    public DateTimeOffset UpdatedAt { get; set; }

    public bool BaleEnabled
    {
        get => (Channels & NotificationChannelFlags.Bale) != 0;
        set => Channels = value ? Channels | NotificationChannelFlags.Bale : Channels & ~NotificationChannelFlags.Bale;
    }

    public bool TelegramEnabled
    {
        get => (Channels & NotificationChannelFlags.Telegram) != 0;
        set => Channels = value ? Channels | NotificationChannelFlags.Telegram : Channels & ~NotificationChannelFlags.Telegram;
    }

    public bool EitaaEnabled
    {
        get => (Channels & NotificationChannelFlags.Eitaa) != 0;
        set => Channels = value ? Channels | NotificationChannelFlags.Eitaa : Channels & ~NotificationChannelFlags.Eitaa;
    }
}

public sealed class NotificationRuleUpdateRequest
{
    public bool IsEnabled { get; set; } = true;
    public int? Severity { get; set; }
    public string TitleTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public string? ActionUrlTemplate { get; set; }
    public bool BroadcastToAdmins { get; set; }
    public int Channels { get; set; } = NotificationChannelFlags.Internal;
}

public sealed class MessengerEndpointDto
{
    public int Channel { get; set; }
    public string ChannelKey { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public bool Available { get; set; }
    public bool Connected { get; set; }
    public string? ExternalIdMasked { get; set; }
    public string? ExternalUserName { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
}

public sealed class MessengerChannelStatusDto
{
    public int Channel { get; set; }
    public string ChannelKey { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public bool BotConfigured { get; set; }
    public bool NotificationsEnabled { get; set; }
    public int ConnectedUserCount { get; set; }
    public string CountLabel { get; set; } = string.Empty;
}

public sealed class BaleConnectStatusDto
{
    public bool Connected { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
    public string? Phone { get; set; }
    public bool BotConfigured { get; set; }
    public string? BotUsername { get; set; }
    public string? BotUrl { get; set; }
    public bool NotificationsEnabled { get; set; }
    public bool SafirEnabled { get; set; }
}

public sealed class MessengerUserDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? MessengerName { get; set; }
    public string? ChatIdMasked { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ChannelPreviewDto
{
    public int Channel { get; set; }
    public string ChannelKey { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public bool Available { get; set; }
    public int Count { get; set; }
}
