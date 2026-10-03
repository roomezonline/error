using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace ErrorService.Server.Services.Messenger;

public sealed class MessengerLinkService
{
    public const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int CodeLength = 8;
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);

    private readonly ErrorServiceDbContext _db;
    private readonly ILogger<MessengerLinkService> _logger;

    public MessengerLinkService(ErrorServiceDbContext db, ILogger<MessengerLinkService> logger)
    {
        _db = db;
        _logger = logger;
    }

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

    public async Task<string> CreateCodeAsync(int userId, NotificationDeliveryChannel channel)
    {
        var old = await _db.MessengerLinkCodes
            .Where(x => x.AppUserId == userId && x.Channel == channel && x.UsedAt == null)
            .ToListAsync();
        if (old.Count > 0)
            _db.MessengerLinkCodes.RemoveRange(old);

        var link = new MessengerLinkCode
        {
            Code = await GenerateUniqueCodeAsync(),
            Channel = channel,
            AppUserId = userId,
            ExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime)
        };
        _db.MessengerLinkCodes.Add(link);
        await _db.SaveChangesAsync();
        return link.Code;
    }

    public async Task<(bool Handled, string Reply)> TryHandleMessageAsync(
        NotificationDeliveryChannel channel, string? text, long chatId, string? userName)
    {
        var candidate = ExtractCode(text);
        if (candidate == null)
            return (false, string.Empty);

        var now = DateTimeOffset.UtcNow;
        var link = await _db.MessengerLinkCodes
            .FirstOrDefaultAsync(x => x.Code == candidate && x.Channel == channel);

        if (link == null || link.UsedAt != null || link.ExpiresAt < now)
            return (true, "کد اتصال نامعتبر یا منقضی شده است.\nدر پروفایل خود در سایت، کد جدیدی بگیرید و دوباره بفرستید.");

        var externalId = chatId.ToString();
        var conflicts = await _db.MessengerEndpoints
            .Where(x => x.Channel == channel && (x.ExternalId == externalId || x.AppUserId == link.AppUserId))
            .ToListAsync();
        if (conflicts.Count > 0)
            _db.MessengerEndpoints.RemoveRange(conflicts);

        _db.MessengerEndpoints.Add(new MessengerEndpoint
        {
            Channel = channel,
            AppUserId = link.AppUserId,
            ExternalId = externalId,
            ExternalUserName = userName,
            Status = MessengerEndpointStatus.Verified,
            VerifiedAt = now
        });
        link.UsedAt = now;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Messenger endpoint linked: user {UserId} channel {Channel} chat {ChatId}",
            link.AppUserId, channel, chatId);

        return (true,
            $"✅ اتصال انجام شد.\nحالا می‌توانید در «تنظیمات اعلان» پروفایل خود، دریافت اعلان از {ChannelTitle(channel)} را روشن کنید.");
    }

    public static string? ExtractCode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var value = text.Trim();
        if (value.StartsWith("/start ", StringComparison.Ordinal))
            value = value[7..].Trim();
        else if (value.StartsWith("/start@", StringComparison.Ordinal))
            return null;

        if (value.Length != CodeLength)
            return null;

        value = value.ToUpperInvariant();
        foreach (var ch in value)
        {
            if (CodeAlphabet.IndexOf(ch) < 0)
                return null;
        }
        return value;
    }

    private async Task<string> GenerateUniqueCodeAsync()
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            Span<char> chars = stackalloc char[CodeLength];
            for (var i = 0; i < CodeLength; i++)
            {
                var index = RandomNumberGenerator.GetInt32(CodeAlphabet.Length);
                chars[i] = CodeAlphabet[index];
            }
            var code = new string(chars);
            if (!await _db.MessengerLinkCodes.AnyAsync(x => x.Code == code))
                return code;
        }
        return Guid.NewGuid().ToString("N")[..CodeLength].ToUpperInvariant();
    }
}
