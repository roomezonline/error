using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services.Messenger;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

public sealed class NotificationEventService
{
    private readonly ErrorServiceDbContext _db;
    private readonly SystemEventService _systemEvents;
    private readonly ILogger<NotificationEventService> _logger;

    private Dictionary<string, NotificationRule>? _ruleCache;

    public NotificationEventService(ErrorServiceDbContext db, SystemEventService systemEvents, ILogger<NotificationEventService> logger)
    {
        _db = db;
        _systemEvents = systemEvents;
        _logger = logger;
    }

    public Task NotifyReceiptCreatedAsync(CustomerReceipt receipt) =>
        GuardAsync("receipt.created", () => EmitAsync(
            eventType: "receipt.created",
            values: new Dictionary<string, string?>
            {
                ["ReceiptId"] = receipt.Id.ToString(),
                ["ReceiptNote"] = receipt.TechnicianId.HasValue
                    ? "ثبت و به تکنسین اختصاص داده شد"
                    : "در کارگاه ثبت شد و در انتظار بررسی است"
            },
            idempotencyKey: $"receipt.created:{receipt.Id}",
            legacySeverity: NotificationSeverity.Info,
            legacyTitle: "رسید جدید ثبت شد",
            legacyBody: receipt.TechnicianId.HasValue
                ? $"رسید شماره {receipt.Id} ثبت و به تکنسین اختصاص داده شد."
                : $"رسید شماره {receipt.Id} در کارگاه ثبت شد و در انتظار بررسی است.",
            legacyUrl: $"/admin/receipts/{receipt.Id}",
            workshopId: receipt.WorkshopId,
            workshopUserId: receipt.TechnicianId));

    public async Task NotifyReceiptChangedAsync(CustomerReceipt receipt, string? previousStatus, int? previousTechnicianId)
    {
        if (!string.Equals(previousStatus, receipt.Status, StringComparison.OrdinalIgnoreCase))
        {
            var statusTitle = receipt.Status switch
            {
                CustomerReceiptStatuses.Repaired => "دستگاه تعمیر شد",
                CustomerReceiptStatuses.Delivered => "دستگاه تحویل شد",
                CustomerReceiptStatuses.NotRepairable => "وضعیت تعمیر به‌روزرسانی شد",
                _ => "وضعیت رسید به‌روزرسانی شد"
            };
            var statusSeverity = receipt.Status switch
            {
                CustomerReceiptStatuses.Repaired or CustomerReceiptStatuses.Delivered => NotificationSeverity.Success,
                CustomerReceiptStatuses.NotRepairable => NotificationSeverity.Warning,
                _ => NotificationSeverity.Info
            };
            await GuardAsync("receipt.status_changed", () => EmitAsync(
                eventType: "receipt.status_changed",
                values: new Dictionary<string, string?>
                {
                    ["ReceiptId"] = receipt.Id.ToString(),
                    ["Status"] = receipt.Status,
                    ["StatusTitle"] = statusTitle
                },
                idempotencyKey: $"receipt.status:{receipt.Id}:{receipt.Status}:{receipt.UpdatedAt.UtcTicks}",
                legacySeverity: statusSeverity,
                legacyTitle: statusTitle,
                legacyBody: $"وضعیت رسید شماره {receipt.Id} تغییر کرد.",
                legacyUrl: $"/admin/receipts/{receipt.Id}",
                workshopId: receipt.WorkshopId,
                workshopUserId: receipt.TechnicianId));
        }

        if (previousTechnicianId != receipt.TechnicianId && receipt.TechnicianId.HasValue)
        {
            await GuardAsync("receipt.technician_assigned", () => EmitAsync(
                eventType: "receipt.technician_assigned",
                values: new Dictionary<string, string?> { ["ReceiptId"] = receipt.Id.ToString() },
                idempotencyKey: $"receipt-technician:{receipt.Id}:{receipt.TechnicianId}:{receipt.UpdatedAt.UtcTicks}",
                legacySeverity: NotificationSeverity.Info,
                legacyTitle: "کار جدید به شما اختصاص یافت",
                legacyBody: $"رسید شماره {receipt.Id} برای بررسی به شما اختصاص داده شد.",
                legacyUrl: $"/admin/receipts/{receipt.Id}",
                workshopId: receipt.WorkshopId,
                workshopUserId: receipt.TechnicianId));
        }
    }

    public Task NotifyOrderStatusChangedAsync(Order order, OrderStatus previousStatus)
    {
        if (!order.UserId.HasValue || previousStatus == order.Status)
            return Task.CompletedTask;

        var label = OrderStatusTitle(order.Status);
        var severity = order.Status switch
        {
            OrderStatus.Rejected or OrderStatus.Cancelled => NotificationSeverity.Warning,
            OrderStatus.Completed => NotificationSeverity.Success,
            _ => NotificationSeverity.Info
        };
        return GuardAsync("order.status_changed", () => EmitAsync(
            eventType: "order.status_changed",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["Amount"] = order.TotalAmount.ToString("N0"),
                ["Status"] = label,
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"order-status:{order.Id}:{previousStatus}:{order.Status}:{order.UpdatedAt.UtcTicks}",
            legacySeverity: severity,
            legacyTitle: "وضعیت سفارش به‌روزرسانی شد",
            legacyBody: $"وضعیت سفارش {order.OrderNumber} به «{label}» تغییر کرد.",
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId));
    }

    public Task NotifyOrderStageChangedAsync(Order order, OrderStage? previousStage)
    {
        if (!order.UserId.HasValue || !order.Stage.HasValue)
            return Task.CompletedTask;

        var stage = order.Stage.Value;
        if (stage == previousStage)
            return Task.CompletedTask;

        var label = OrderStageInfo.Label(stage);
        return GuardAsync($"order.stage.{StageKey(stage)}", () => EmitAsync(
            eventType: $"order.stage.{StageKey(stage)}",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["Amount"] = order.TotalAmount.ToString("N0"),
                ["Stage"] = label,
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"order-stage:{order.Id}:{stage}:{order.UpdatedAt.UtcTicks}",
            legacySeverity: NotificationSeverity.Info,
            legacyTitle: "مرحله سفارش تغییر کرد",
            legacyBody: $"سفارش {order.OrderNumber} به مرحله «{label}» رسید.",
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId));
    }

    public Task NotifyOrderCreatedAsync(Order order)
    {
        if (!order.UserId.HasValue)
            return Task.CompletedTask;
        return GuardAsync("order.created", () => EmitAsync(
            eventType: "order.created",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["Amount"] = order.TotalAmount.ToString("N0"),
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"order.created:{order.Id}",
            legacySeverity: NotificationSeverity.Info,
            legacyTitle: "سفارش شما ثبت شد",
            legacyBody: $"سفارش {order.OrderNumber} به مبلغ {order.TotalAmount:N0} تومان ثبت شد و در انتظار پرداخت است.",
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId));
    }

    public Task NotifyOrderReceiptUploadedAsync(Order order)
    {
        if (!order.UserId.HasValue)
            return Task.CompletedTask;
        var when = order.PaymentDate ?? order.UpdatedAt;
        return GuardAsync("order.receipt_uploaded", () => EmitAsync(
            eventType: "order.receipt_uploaded",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"order.receipt:{order.Id}:{when.UtcTicks}",
            legacySeverity: NotificationSeverity.Info,
            legacyTitle: "رسید پرداخت دریافت شد",
            legacyBody: $"رسید پرداخت سفارش {order.OrderNumber} دریافت شد و در انتظار تأیید است.",
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId));
    }

    public Task NotifyOrderAutoCancelledAsync(Order order)
    {
        if (!order.UserId.HasValue)
            return Task.CompletedTask;
        return GuardAsync("order.auto_cancelled", () => EmitAsync(
            eventType: "order.auto_cancelled",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"order.cancelled:{order.Id}",
            legacySeverity: NotificationSeverity.Warning,
            legacyTitle: "سفارش لغو شد",
            legacyBody: $"سفارش {order.OrderNumber} به دلیل عدم پرداخت در مهلت مقرر لغو شد.",
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId));
    }

    public Task NotifyReturnCreatedAsync(Order order, OrderReturn orderReturn)
    {
        if (!order.UserId.HasValue)
            return Task.CompletedTask;
        return GuardAsync("return.created", () => EmitAsync(
            eventType: "return.created",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["RefundAmount"] = orderReturn.RefundAmount.ToString("N0"),
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"return.created:{orderReturn.Id}",
            legacySeverity: NotificationSeverity.Info,
            legacyTitle: "درخواست مرجوعی ثبت شد",
            legacyBody: $"درخواست مرجوعی سفارش {order.OrderNumber} به مبلغ {orderReturn.RefundAmount:N0} تومان ثبت شد و در حال بررسی است.",
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId));
    }

    public Task NotifyReturnRefundedAsync(Order order, OrderReturn orderReturn)
    {
        if (!order.UserId.HasValue)
            return Task.CompletedTask;
        return GuardAsync("return.refunded", () => EmitAsync(
            eventType: "return.refunded",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["RefundAmount"] = orderReturn.RefundAmount.ToString("N0"),
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"return.refunded:{orderReturn.Id}",
            legacySeverity: NotificationSeverity.Success,
            legacyTitle: "واریز مرجوعی ثبت شد",
            legacyBody: $"مبلغ {orderReturn.RefundAmount:N0} تومان بابت مرجوعی سفارش {order.OrderNumber} واریز شد.",
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId));
    }

    public Task NotifyAsync(
        string eventType,
        IReadOnlyDictionary<string, string?> values,
        string idempotencyKey,
        NotificationSeverity fallbackSeverity,
        string fallbackTitle,
        string fallbackBody,
        string? fallbackUrl = null,
        int? appUserId = null,
        int? workshopId = null,
        int? workshopUserId = null,
        string? phone = null) =>
        GuardAsync(eventType, () => EmitAsync(
            eventType,
            values,
            idempotencyKey,
            fallbackSeverity,
            fallbackTitle,
            fallbackBody,
            fallbackUrl,
            appUserId,
            workshopId,
            workshopUserId,
            phone));

    private async Task EmitAsync(
        string eventType,
        IReadOnlyDictionary<string, string?> values,
        string idempotencyKey,
        NotificationSeverity legacySeverity,
        string legacyTitle,
        string legacyBody,
        string? legacyUrl,
        int? appUserId = null,
        int? workshopId = null,
        int? workshopUserId = null,
        string? phone = null)
    {
        var rule = await GetRuleAsync(eventType);
        if (rule is { IsEnabled: false })
            return;

        string title;
        string body;
        string? actionUrl;
        NotificationSeverity severity;
        bool broadcast;

        if (rule != null)
        {
            title = Render(rule.TitleTemplate, values);
            body = Render(rule.BodyTemplate, values);
            actionUrl = string.IsNullOrWhiteSpace(rule.ActionUrlTemplate) ? null : Render(rule.ActionUrlTemplate, values);
            severity = rule.Severity ?? legacySeverity;
            broadcast = rule.BroadcastToAdmins;
        }
        else if (NotificationRuleDefaults.TryGet(eventType, out var def))
        {
            title = Render(def.Title, values);
            body = Render(def.Body, values);
            actionUrl = def.ActionUrl == null ? null : Render(def.ActionUrl, values);
            severity = def.Severity ?? legacySeverity;
            broadcast = def.BroadcastToAdmins;
        }
        else
        {
            title = legacyTitle;
            body = legacyBody;
            actionUrl = legacyUrl;
            severity = legacySeverity;
            broadcast = false;
        }

        if (await IsDuplicateAsync(idempotencyKey))
            return;

        var notification = new Notification
        {
            EventType = eventType,
            Title = title,
            Body = body,
            Severity = severity,
            WorkshopId = workshopId,
            ActionUrl = actionUrl,
            IdempotencyKey = idempotencyKey
        };

        var appUserIds = new HashSet<int>();
        if (!appUserId.HasValue && !string.IsNullOrWhiteSpace(phone))
            appUserId = await _db.Users
                .Where(x => x.PhoneNumber == phone && x.IsActive)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync();
        if (appUserId.HasValue)
            appUserIds.Add(appUserId.Value);

        var workshopUserIds = new HashSet<int>();
        if (workshopId.HasValue)
        {
            var users = await _db.WorkshopUsers
                .Where(x => x.WorkshopId == workshopId.Value
                    && x.IsActive
                    && (!workshopUserId.HasValue || x.Id == workshopUserId.Value))
                .Select(x => x.Id)
                .ToListAsync();
            workshopUserIds.UnionWith(users);
        }

        if (broadcast)
        {
            var adminIds = await (
                from ur in _db.AppUserRoles
                join u in _db.Users on ur.UserId equals u.Id
                join rp in _db.RolePermissions on ur.RoleId equals rp.RoleId
                join p in _db.Permissions on rp.PermissionId equals p.Id
                where u.IsActive && p.Key == "admin.notifications.view"
                select ur.UserId)
                .Distinct()
                .ToListAsync();
            if (adminIds.Count > 0)
            {
                appUserIds.UnionWith(adminIds);
                notification.IsBroadcast = true;
            }
        }

        foreach (var id in appUserIds)
            notification.Recipients.Add(new NotificationRecipient { AppUserId = id });
        foreach (var id in workshopUserIds)
            notification.Recipients.Add(new NotificationRecipient { WorkshopUserId = id });

        if (notification.Recipients.Count == 0)
            return;

        await CreateDeliveriesAsync(notification, rule?.Channels ?? NotificationChannelFlags.Internal);

        _db.Notifications.Add(notification);
    }

    public async Task CreateDeliveriesAsync(Notification notification, int channels)
    {
        var effectiveChannels = channels | NotificationChannelFlags.Internal;

        foreach (var recipient in notification.Recipients)
        {
            notification.Deliveries.Add(new NotificationDelivery
            {
                Recipient = recipient,
                Channel = NotificationDeliveryChannel.Internal,
                Status = NotificationDeliveryStatus.Sent,
                AttemptCount = 1,
                LastAttemptAt = DateTimeOffset.UtcNow,
                SentAt = DateTimeOffset.UtcNow
            });
        }

        var appUserIds = notification.Recipients
            .Where(x => x.AppUserId.HasValue)
            .Select(x => x.AppUserId!.Value)
            .ToHashSet();

        await QueueExternalDeliveriesAsync(notification, effectiveChannels, appUserIds);
    }

    private static readonly (NotificationDeliveryChannel Channel, int Flag)[] ExternalChannelFlags =
    {
        (NotificationDeliveryChannel.Bale, NotificationChannelFlags.Bale),
        (NotificationDeliveryChannel.Telegram, NotificationChannelFlags.Telegram),
        (NotificationDeliveryChannel.Eitaa, NotificationChannelFlags.Eitaa)
    };

    private async Task QueueExternalDeliveriesAsync(Notification notification, int ruleChannels, HashSet<int> appUserIds)
    {
        var flags = ruleChannels & ~NotificationChannelFlags.Internal;
        if (flags == 0 || appUserIds.Count == 0)
            return;

        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings == null)
            return;

        foreach (var (channel, flag) in ExternalChannelFlags)
        {
            if ((flags & flag) == 0)
                continue;
            if (!MessengerChannels.IsEnabled(channel, settings))
                continue;

            foreach (var recipient in notification.Recipients)
            {
                if (!recipient.AppUserId.HasValue || !appUserIds.Contains(recipient.AppUserId.Value))
                    continue;

                notification.Deliveries.Add(new NotificationDelivery
                {
                    Recipient = recipient,
                    Channel = channel,
                    Status = NotificationDeliveryStatus.Pending
                });
            }
        }
    }

    private async Task GuardAsync(string eventType, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification event {EventType} failed. Main operation continues without notification.", eventType);

            var stale = _db.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added
                    && e.Entity is Notification or NotificationRecipient or NotificationDelivery)
                .ToList();
            foreach (var entry in stale)
                entry.State = EntityState.Detached;

            try
            {
                await _systemEvents.LogEventAsync(new SystemEventLog
                {
                    OccurredAt = DateTimeOffset.UtcNow,
                    Severity = EventSeverity.Error,
                    Category = EventCategory.System,
                    PersianTitle = "خطا در ساخت اعلان خودکار",
                    PersianDescription = $"ساخت اعلان رویداد «{eventType}» ناموفق بود؛ عملیات اصلی بدون اعلان ادامه یافت.",
                    TechnicalTitle = "NotificationEventFailed",
                    TechnicalDetails = ex.Message,
                    StackTrace = ex.StackTrace,
                    ErrorCode = "notification_event_failed",
                    Component = "NotificationEventService",
                    Tags = eventType,
                    Status = InvestigationStatus.New
                });
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to record notification failure for {EventType}.", eventType);
            }
        }
    }

    private async Task<NotificationRule?> GetRuleAsync(string eventType)
    {
        if (_ruleCache == null)
        {
            _ruleCache = await _db.NotificationRules
                .AsNoTracking()
                .ToDictionaryAsync(x => x.EventType, StringComparer.OrdinalIgnoreCase);
        }
        return _ruleCache.TryGetValue(eventType, out var rule) ? rule : null;
    }

    private static string Render(string? template, IReadOnlyDictionary<string, string?> values)
    {
        if (string.IsNullOrEmpty(template))
            return template ?? string.Empty;
        foreach (var kv in values)
            template = template.Replace("{" + kv.Key + "}", kv.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        return template;
    }

    private async Task<bool> IsDuplicateAsync(string? idempotencyKey)
        => idempotencyKey != null && await _db.Notifications.AnyAsync(n => n.IdempotencyKey == idempotencyKey);

    private static string StageKey(OrderStage stage) => stage switch
    {
        OrderStage.PaymentConfirmed => "payment_confirmed",
        OrderStage.Processing => "processing",
        OrderStage.ActionPending => "action_pending",
        OrderStage.Collecting => "collecting",
        OrderStage.Collected => "collected",
        OrderStage.SentToUnit => "sent_to_unit",
        OrderStage.Resolved => "resolved",
        _ => stage.ToString().ToLowerInvariant()
    };

    private static string OrderStatusTitle(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => "در انتظار پرداخت",
        OrderStatus.ReceiptUploaded => "رسید آپلود شد",
        OrderStatus.Approved => "تأیید شده",
        OrderStatus.Rejected => "رد شده",
        OrderStatus.Cancelled => "لغو شده",
        OrderStatus.Completed => "تکمیل شده",
        _ => status.ToString()
    };
}
