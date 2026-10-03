using ErrorService.Server.Models;

namespace ErrorService.Server.Services.Messenger;

public static class MessengerChannels
{
    public static NotificationDeliveryChannel? ParseChannel(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "bale" => NotificationDeliveryChannel.Bale,
        "telegram" => NotificationDeliveryChannel.Telegram,
        "eitaa" => NotificationDeliveryChannel.Eitaa,
        _ => null
    };

    public static string ChannelKey(NotificationDeliveryChannel channel) => channel switch
    {
        NotificationDeliveryChannel.Bale => "bale",
        NotificationDeliveryChannel.Telegram => "telegram",
        NotificationDeliveryChannel.Eitaa => "eitaa",
        _ => string.Empty
    };

    public static string ChannelTitle(NotificationDeliveryChannel channel) => channel switch
    {
        NotificationDeliveryChannel.Bale => "بله",
        NotificationDeliveryChannel.Telegram => "تلگرام",
        NotificationDeliveryChannel.Eitaa => "ایتا",
        _ => "نامشخص"
    };

    public static bool IsEnabled(NotificationDeliveryChannel channel, SiteSettings? settings)
    {
        if (settings == null)
            return false;
        return channel switch
        {
            NotificationDeliveryChannel.Bale => settings.EnableBaleNotifications
                && !string.IsNullOrWhiteSpace(settings.BaleBotToken)
                && !string.IsNullOrWhiteSpace(settings.BaleSafirApiKey),
            NotificationDeliveryChannel.Telegram => settings.EnableTelegramNotifications
                && !string.IsNullOrWhiteSpace(settings.TelegramBotToken),
            NotificationDeliveryChannel.Eitaa => settings.EnableEitaaNotifications
                && !string.IsNullOrWhiteSpace(settings.EitaaBotToken),
            _ => false
        };
    }
}
