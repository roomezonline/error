using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services.Messenger;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ErrorService.Server.Services;

public sealed class NotificationDispatcherService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private const int MaxAttempts = 5;
    private const int CandidateBatch = 200;
    private const int DispatchBatch = 50;

    private static readonly TimeSpan[] RetryBackoff =
    {
        TimeSpan.Zero,
        TimeSpan.FromSeconds(15),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15)
    };

    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationDispatcherService> _logger;

    public NotificationDispatcherService(IServiceProvider services, IConfiguration configuration, ILogger<NotificationDispatcherService> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationDispatcherService is running");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while dispatching notification deliveries");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DispatchAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
        var router = scope.ServiceProvider.GetRequiredService<MessengerRouter>();

        var now = DateTimeOffset.UtcNow;
        var candidates = await db.NotificationDeliveries
            .Include(x => x.Recipient)
            .Include(x => x.Notification)
            .Where(x => x.Status == NotificationDeliveryStatus.Pending
                && x.Channel != NotificationDeliveryChannel.Internal
                && x.AttemptCount < MaxAttempts)
            .OrderBy(x => x.Id)
            .Take(CandidateBatch)
            .ToListAsync(ct);

        var due = candidates
            .Where(x => x.LastAttemptAt == null || now - x.LastAttemptAt >= RetryBackoff[Math.Min(x.AttemptCount, RetryBackoff.Length - 1)])
            .Take(DispatchBatch)
            .ToList();

        if (due.Count == 0)
            return;

        var settings = await db.SiteSettings.FirstOrDefaultAsync(ct);

        foreach (var delivery in due)
        {
            delivery.LastAttemptAt = now;

            var channelKey = ChannelKey(delivery.Channel);
            var channel = channelKey == null ? null : router.GetByKey(channelKey);
            var appUserId = delivery.Recipient?.AppUserId;

            if (channel == null || appUserId == null || !NotificationsEnabled(delivery.Channel, settings))
            {
                delivery.AttemptCount++;
                delivery.Status = NotificationDeliveryStatus.Failed;
                delivery.ErrorMessage = "کانال اعلان پیکربندی نشده یا برای این گیرنده در دسترس نیست";
                continue;
            }

            var endpoint = await db.MessengerEndpoints
                .FirstOrDefaultAsync(x => x.Channel == delivery.Channel
                    && x.AppUserId == appUserId.Value
                    && x.Status == MessengerEndpointStatus.Verified, ct);

            if (endpoint == null)
            {
                delivery.Status = NotificationDeliveryStatus.Skipped;
                delivery.ErrorMessage = "کاربر هنوز این پیام‌رسان را در تنظیمات اعلان متصل نکرده است";
                continue;
            }

            if (!long.TryParse(endpoint.ExternalId, out var chatId))
            {
                delivery.AttemptCount++;
                delivery.Status = NotificationDeliveryStatus.Failed;
                delivery.ErrorMessage = "شناسه گفتگوی ذخیره‌شده نامعتبر است";
                continue;
            }

            var result = await channel.TrySendTextToChatAsync(chatId, BuildMessage(delivery.Notification, _configuration));
            delivery.AttemptCount++;

            if (result.Success)
            {
                delivery.Status = NotificationDeliveryStatus.Sent;
                delivery.SentAt = now;
                delivery.ExternalMessageId = result.ExternalMessageId;
                delivery.ErrorMessage = null;
                endpoint.LastSentAt = now;
                _logger.LogInformation("Notification {NotificationId} delivered via {Channel} to user {UserId}",
                    delivery.NotificationId, delivery.Channel, appUserId);
            }
            else
            {
                var error = result.Error ?? "خطای ناشناخته";
                delivery.ErrorMessage = error.Length > 2000 ? error[..2000] : error;
                if (delivery.AttemptCount >= MaxAttempts)
                {
                    delivery.Status = NotificationDeliveryStatus.Failed;
                    _logger.LogWarning("Notification {NotificationId} permanently failed via {Channel} to user {UserId}: {Error}",
                        delivery.NotificationId, delivery.Channel, appUserId, error);
                }
                else
                {
                    _logger.LogWarning("Notification {NotificationId} delivery attempt {Attempt}/{Max} via {Channel} failed: {Error}",
                        delivery.NotificationId, delivery.AttemptCount, MaxAttempts, delivery.Channel, error);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static string? ChannelKey(NotificationDeliveryChannel channel) => channel switch
    {
        NotificationDeliveryChannel.Bale => "bale",
        NotificationDeliveryChannel.Telegram => "telegram",
        NotificationDeliveryChannel.Eitaa => "eitaa",
        _ => null
    };

    private static bool NotificationsEnabled(NotificationDeliveryChannel channel, SiteSettings? settings)
    {
        if (settings == null)
            return false;
        return channel switch
        {
            NotificationDeliveryChannel.Bale => settings.EnableBaleNotifications && !string.IsNullOrWhiteSpace(settings.BaleBotToken),
            NotificationDeliveryChannel.Telegram => settings.EnableTelegramNotifications && !string.IsNullOrWhiteSpace(settings.TelegramBotToken),
            NotificationDeliveryChannel.Eitaa => settings.EnableEitaaNotifications && !string.IsNullOrWhiteSpace(settings.EitaaBotToken),
            _ => false
        };
    }

    internal static string BuildMessage(Notification notification, IConfiguration configuration)
    {
        var sb = new StringBuilder();
        sb.Append(notification.Title);
        sb.Append('\n');
        sb.Append(notification.Body);
        if (!string.IsNullOrWhiteSpace(notification.ActionUrl))
        {
            var baseUrl = (configuration.GetValue<string>("Site:BaseUrl") ?? "https://errorservice.ir").TrimEnd('/');
            var url = notification.ActionUrl.StartsWith('/') ? notification.ActionUrl : "/" + notification.ActionUrl;
            sb.Append('\n');
            sb.Append(baseUrl);
            sb.Append(url);
        }
        return sb.ToString();
    }
}
