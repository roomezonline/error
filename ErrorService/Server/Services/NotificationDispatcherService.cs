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
        var safir = scope.ServiceProvider.GetRequiredService<BaleSafirService>();

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

            var appUserId = delivery.Recipient?.AppUserId;
            if (appUserId == null || !MessengerChannels.IsEnabled(delivery.Channel, settings))
            {
                delivery.AttemptCount++;
                delivery.Status = NotificationDeliveryStatus.Failed;
                delivery.ErrorMessage = "کانال اعلان پیکربندی یا فعال نشده است";
                continue;
            }

            MessengerSendResult result;
            if (delivery.Channel == NotificationDeliveryChannel.Bale)
            {
                var userPhone = await db.Users
                    .Where(x => x.Id == appUserId.Value)
                    .Select(x => x.PhoneNumber)
                    .FirstOrDefaultAsync(ct);

                if (string.IsNullOrWhiteSpace(userPhone))
                {
                    delivery.Status = NotificationDeliveryStatus.Skipped;
                    delivery.ErrorMessage = "شماره موبایل برای این کاربر ثبت نشده است";
                    continue;
                }

                var text = BuildMessage(delivery.Notification, _configuration);
                var actionUrl = BuildActionUrl(delivery.Notification, _configuration);
                result = await safir.SendToPhoneAsync(settings, userPhone, text, actionUrl);
            }
            else
            {
                var endpoint = await db.MessengerEndpoints
                    .FirstOrDefaultAsync(x => x.Channel == delivery.Channel
                        && x.AppUserId == appUserId.Value
                        && x.Status == MessengerEndpointStatus.Verified, ct);

                if (endpoint == null)
                {
                    delivery.Status = NotificationDeliveryStatus.Skipped;
                    delivery.ErrorMessage = "نشانی این پیام‌رسان برای کاربر توسط مدیر ثبت نشده است";
                    continue;
                }

                if (!long.TryParse(endpoint.ExternalId, out var chatId))
                {
                    delivery.AttemptCount++;
                    delivery.Status = NotificationDeliveryStatus.Failed;
                    delivery.ErrorMessage = "نشانی ثبت‌شده برای گیرنده نامعتبر است";
                    continue;
                }

                var channel = router.GetByKey(ChannelKey(delivery.Channel));
                if (channel == null)
                {
                    delivery.AttemptCount++;
                    delivery.Status = NotificationDeliveryStatus.Failed;
                    delivery.ErrorMessage = "کانال اعلان پیکربندی نشده است";
                    continue;
                }

                result = await channel.TrySendTextToChatAsync(chatId, BuildMessage(delivery.Notification, _configuration));
                if (result.Success)
                    endpoint.LastSentAt = now;
            }

            delivery.AttemptCount++;

            if (result.Success)
            {
                delivery.Status = NotificationDeliveryStatus.Sent;
                delivery.SentAt = now;
                delivery.ExternalMessageId = result.ExternalMessageId;
                delivery.ErrorMessage = null;
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

    internal static string? BuildActionUrl(Notification notification, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(notification.ActionUrl))
            return null;
        var baseUrl = (configuration.GetValue<string>("Site:BaseUrl") ?? "https://errorservice.ir").TrimEnd('/');
        var url = notification.ActionUrl.StartsWith('/') ? notification.ActionUrl : "/" + notification.ActionUrl;
        return baseUrl + url;
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
