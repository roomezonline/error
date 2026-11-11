using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services.Messenger;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

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

    /// <summary>
    /// مبلغ واقعاً قابل پرداخت سفارش (مبلغ کل منهای تخفیف).
    /// TotalAmount شامل حمل‌ونقل ولی بدون تخفیف است؛ نمایش خام آن در پیام‌ها
    /// با مبلغی که مشتری می‌پردازد نمی‌خواند.
    /// </summary>
    private static decimal PayableAmount(Order order)
        => order.TotalAmount - (order.DiscountAmount ?? 0);

    /// <summary>
    /// سوپر ادمین‌ها در دو جدول جدا نگهداری می‌شوند: کاربر سایت (AppUserRoles)
    /// و کاربر کارگاه (WorkshopUserRoles). برای گیرندگان اعلان، هر دو یکسان‌اند.
    /// </summary>
    public async Task<(List<int> AppUserIds, List<int> WorkshopUserIds)> GetSuperAdminRecipientsAsync()
    {
        var appUserIds = await (
            from ur in _db.AppUserRoles
            join u in _db.Users on ur.UserId equals u.Id
            where u.IsActive && !u.IsDeleted && ur.Role.Key == "super_admin"
            select u.Id)
            .Distinct()
            .ToListAsync();

        var workshopUserIds = await (
            from wur in _db.WorkshopUserRoles
            join w in _db.WorkshopUsers on wur.WorkshopUserId equals w.Id
            join ws in _db.Workshops on w.WorkshopId equals ws.Id
            where w.IsActive && !w.IsDeleted && !ws.IsDeleted && wur.Role.Key == "super_admin"
            select w.Id)
            .Distinct()
            .ToListAsync();

        return (appUserIds, workshopUserIds);
    }

    /// <summary>
    /// اگر همان شخص در سایت هم گیرنده باشد، نسخهٔ کارگاهی حذف می‌شود
    /// تا اعلان داخلی و پیام بله دو بار برایش ارسال نشود.
    /// </summary>
    public async Task<List<int>> FilterWorkshopRecipientsByPhoneAsync(
        IReadOnlyCollection<int> workshopUserIds,
        IReadOnlyCollection<int> appUserIds)
    {
        if (workshopUserIds.Count == 0)
            return new List<int>();
        if (appUserIds.Count == 0)
            return workshopUserIds.ToList();

        var appPhones = await _db.Users
            .Where(x => appUserIds.Contains(x.Id))
            .Select(x => x.PhoneNumber)
            .ToListAsync();
        var taken = appPhones
            .Select(PhoneDigits)
            .Where(x => x != null)
            .Select(x => x!)
            .ToHashSet(StringComparer.Ordinal);

        var workshopPhones = await _db.WorkshopUsers
            .Where(x => workshopUserIds.Contains(x.Id))
            .Select(x => new { x.Id, x.PhoneNumber })
            .ToListAsync();

        return workshopPhones
            .Where(x =>
            {
                var key = PhoneDigits(x.PhoneNumber);
                return key == null || !taken.Contains(key);
            })
            .Select(x => x.Id)
            .ToList();
    }

    internal static string? PhoneDigits(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var chars = raw.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c >= '۰' && c <= '۹') chars[i] = (char)('0' + (c - '۰'));
            else if (c >= '٠' && c <= '٩') chars[i] = (char)('0' + (c - '٠'));
        }

        var digits = new string(chars.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0098")) digits = "0" + digits[4..];
        else if (digits.StartsWith("98") && digits.Length >= 12) digits = "0" + digits[2..];
        else if (digits.Length == 10 && digits[0] != '0') digits = "0" + digits;
        return digits.Length is >= 10 and <= 15 ? digits : null;
    }

    /// <summary>
    /// متن آزاد کاربر (نظر/پرسش) را برای پیام اعلان تمیز می‌کند:
    /// یکدست‌سازی خطوط، حذف خطوط خالی تکراری و محدودکردن طول پیام.
    /// </summary>
    public static string CleanContent(string? text, int maxLength = 2500)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "—";

        var value = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        while (value.Contains("\n\n\n"))
            value = value.Replace("\n\n\n", "\n\n");

        if (value.Length > maxLength)
            value = value[..maxLength].TrimEnd() + "…";
        return value;
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

    public async Task<string?> NotifyOrderStatusChangedAsync(Order order, OrderStatus previousStatus)
    {
        if (!order.UserId.HasValue || previousStatus == order.Status)
            return null;

        var label = OrderStatusTitle(order.Status);
        var severity = order.Status switch
        {
            OrderStatus.Rejected or OrderStatus.Cancelled => NotificationSeverity.Warning,
            OrderStatus.Completed => NotificationSeverity.Success,
            _ => NotificationSeverity.Info
        };
        var idempotencyKey = $"order-status:{order.Id}:{previousStatus}:{order.Status}:{order.UpdatedAt.UtcTicks}";
        await GuardAsync("order.status_changed", () => EmitAsync(
            eventType: "order.status_changed",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["Amount"] = PayableAmount(order).ToString("N0"),
                ["Status"] = label,
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: idempotencyKey,
            legacySeverity: severity,
            legacyTitle: "وضعیت سفارش به‌روزرسانی شد",
            legacyBody: $"وضعیت سفارش {order.OrderNumber} به مبلغ {PayableAmount(order):N0} تومان به «{label}» تغییر کرد.",
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId));
        return idempotencyKey;
    }

    /// <summary>
    /// یادآوری روزانهٔ سفارش تکمیل‌نشده برای سوپر ادمین‌ها. پیام شامل نام مشتری،
    /// اقلام سفارش، تاریخ ثبت، وضعیت/مرحلهٔ فعلی و زمان آخرین تغییر وضعیت است و
    /// هم در مرکز اعلان‌ها و هم در بله ارسال می‌شود.
    /// </summary>
    public Task NotifyOrderStaleAsync(Order order, DateTimeOffset lastChangeAt, int staleDays)
    {
        var customerName = string.IsNullOrWhiteSpace(order.FullName) ? "—" : order.FullName.Trim();
        var phone = string.IsNullOrWhiteSpace(order.PhoneNumber) ? "—" : order.PhoneNumber.Trim();
        var status = OrderStatusTitle(order.Status);
        var stage = order.Stage.HasValue ? OrderStageInfo.Label(order.Stage.Value) : "—";
        var tracking = string.IsNullOrWhiteSpace(order.TrackingNumber) ? "—" : order.TrackingNumber!;
        var days = Math.Max(1, staleDays);

        return GuardAsync("order.stale", () => EmitAsync(
            eventType: "order.stale",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["CustomerName"] = customerName,
                ["Phone"] = phone,
                ["OrderDate"] = JalaliDateTime(order.CreatedAt),
                ["Products"] = OrderProductsSummary(order),
                ["Amount"] = PayableAmount(order).ToString("N0"),
                ["Status"] = status,
                ["Stage"] = stage,
                ["LastChange"] = JalaliDateTime(lastChangeAt),
                ["StaleDays"] = days.ToString(),
                ["TrackingNumber"] = tracking,
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"order.stale:{order.Id}:{lastChangeAt.UtcTicks}:{TehranDateKey(DateTimeOffset.UtcNow)}",
            legacySeverity: NotificationSeverity.Warning,
            legacyTitle: $"سفارش {order.OrderNumber} در انتظار اقدام است",
            legacyBody: $"سفارش {order.OrderNumber} از {customerName} بیش از {days} روز در وضعیت «{status}» بدون تغییر مانده است.",
            legacyUrl: "/admin/orders",
            superAdminsOnly: true));
    }

    public async Task<string?> NotifyOrderStageChangedAsync(Order order, OrderStage? previousStage)
    {
        if (!order.UserId.HasValue || !order.Stage.HasValue)
            return null;

        var stage = order.Stage.Value;
        if (stage == previousStage)
            return null;

        var label = OrderStageInfo.Label(stage);
        var idempotencyKey = $"order-stage:{order.Id}:{stage}:{order.UpdatedAt.UtcTicks}";
        await GuardAsync($"order.stage.{StageKey(stage)}", () => EmitAsync(
            eventType: $"order.stage.{StageKey(stage)}",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["Amount"] = PayableAmount(order).ToString("N0"),
                ["Stage"] = label,
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: idempotencyKey,
            legacySeverity: NotificationSeverity.Info,
            legacyTitle: "مرحله سفارش تغییر کرد",
            legacyBody: $"سفارش {order.OrderNumber} به مرحله «{label}» رسید.",
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId));
        return idempotencyKey;
    }

    /// <summary>
    /// یادداشت مستقل مدیر (متن و تصاویر) را به‌عنوان اعلان مشتری ثبت می‌کند.
    /// کلید ثابت مبتنی بر شناسهٔ یادداشت است تا با حذف یادداشت، اعلان آن هم پاک شود.
    /// </summary>
    public async Task<string?> NotifyOrderNoteAddedAsync(Order order, OrderNote note)
    {
        if (!order.UserId.HasValue && string.IsNullOrWhiteSpace(order.PhoneNumber))
            return null;

        var images = ParseImageUrls(note.ImageUrlsJson);
        var message = string.IsNullOrWhiteSpace(note.Message)
            ? (images.Count > 0 ? "تصویر جدیدی برای سفارش شما ثبت شد." : "یادداشت جدیدی برای سفارش شما ثبت شد.")
            : note.Message.Trim();
        var idempotencyKey = $"order-note:{note.Id}";

        await GuardAsync("order.note_added", () => EmitAsync(
            eventType: "order.note_added",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["OrderId"] = order.Id.ToString(),
                ["Message"] = message,
                ["ImageCount"] = images.Count.ToString()
            },
            idempotencyKey: idempotencyKey,
            legacySeverity: NotificationSeverity.Info,
            legacyTitle: "یادداشت جدید مدیر",
            legacyBody: images.Count > 0
                ? $"{message}\n{images.Count} تصویر پیوست شده است."
                : message,
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId,
            phone: order.PhoneNumber,
            imageUrls: images));
        return idempotencyKey;
    }

    private static List<string> ParseImageUrls(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    /// <summary>متن و تصاویر اعلان یادداشت را پس از ویرایش یادداشت هم‌خوان می‌کند.</summary>
    public async Task SyncOrderNoteNotificationAsync(Order order, OrderNote note)
    {
        var key = $"order-note:{note.Id}";
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.IdempotencyKey == key);
        if (notification == null) return;

        var images = ParseImageUrls(note.ImageUrlsJson);
        var message = string.IsNullOrWhiteSpace(note.Message)
            ? (images.Count > 0 ? "تصویر جدیدی برای سفارش شما ثبت شد." : "یادداشت جدیدی برای سفارش شما ثبت شد.")
            : note.Message.Trim();
        var values = new Dictionary<string, string?>
        {
            ["OrderNumber"] = order.OrderNumber,
            ["OrderId"] = order.Id.ToString(),
            ["Message"] = message,
            ["ImageCount"] = images.Count.ToString()
        };

        var rule = await GetRuleAsync("order.note_added");
        if (rule != null)
        {
            notification.Title = Render(rule.TitleTemplate, values);
            notification.Body = Render(rule.BodyTemplate, values);
        }
        else
        {
            notification.Title = "یادداشت جدید مدیر";
            notification.Body = images.Count > 0 ? $"{message}\n{images.Count} تصویر پیوست شده است." : message;
        }

        notification.ImageUrlsJson = images.Count > 0 ? JsonSerializer.Serialize(images) : null;
    }

    /// <summary>اعلان یادداشت حذف‌شده را از سیستم اعلان پاک می‌کند.</summary>
    public async Task DeleteOrderNoteNotificationAsync(int noteId)
    {
        var key = $"order-note:{noteId}";
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.IdempotencyKey == key);
        if (notification != null)
            _db.Notifications.Remove(notification);
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
                ["Amount"] = PayableAmount(order).ToString("N0"),
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"order.created:{order.Id}",
            legacySeverity: NotificationSeverity.Info,
            legacyTitle: "سفارش شما ثبت شد",
            legacyBody: $"سفارش {order.OrderNumber} به مبلغ {PayableAmount(order):N0} تومان ثبت شد و در انتظار پرداخت است.",
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId));
    }

    /// <summary>
    /// سفارش جدید سایت را به همهٔ سوپر ادمین‌ها (حساب سایت و کاربر کارگاه)
    /// در اعلان داخلی و بله اطلاع می‌دهد.
    /// </summary>
    public Task NotifyOrderPlacedForSuperAdminsAsync(Order order)
    {
        var customerName = string.IsNullOrWhiteSpace(order.FullName) ? "—" : order.FullName.Trim();
        var phone = string.IsNullOrWhiteSpace(order.PhoneNumber) ? "—" : order.PhoneNumber.Trim();

        return GuardAsync("order.created.admin", () => EmitAsync(
            eventType: "order.created.admin",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["Amount"] = PayableAmount(order).ToString("N0"),
                ["CustomerName"] = customerName,
                ["Phone"] = phone,
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"order.created.admin:{order.Id}",
            legacySeverity: NotificationSeverity.Info,
            legacyTitle: "سفارش جدید ثبت شد",
            legacyBody: $"سفارش {order.OrderNumber} به مبلغ {PayableAmount(order):N0} تومان توسط {customerName} ثبت شد.",
            legacyUrl: "/admin/orders",
            superAdminsOnly: true));
    }

    /// <summary>
    /// سفارشی که مدیر برای مشتری می‌سازد یا پیش‌فاکتور او را منتشر می‌کند.
    /// کانال پیش‌فرض این رویداد علاوه بر اعلان داخلی، بله است.
    /// </summary>
    public Task NotifyOrderCreatedByAdminAsync(Order order)
    {
        if (!order.UserId.HasValue && string.IsNullOrWhiteSpace(order.PhoneNumber))
            return Task.CompletedTask;
        return GuardAsync("order.created_by_admin", () => EmitAsync(
            eventType: "order.created_by_admin",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["Amount"] = PayableAmount(order).ToString("N0"),
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"order.created_by_admin:{order.Id}",
            legacySeverity: NotificationSeverity.Info,
            legacyTitle: "سفارش شما آماده پرداخت است",
            legacyBody: $"سفارش {order.OrderNumber} به مبلغ {PayableAmount(order):N0} تومان توسط فروشگاه برای شما ثبت شد و آماده پرداخت است.",
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId,
            phone: order.PhoneNumber));
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
                ["Amount"] = PayableAmount(order).ToString("N0"),
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"order.receipt:{order.Id}:{when.UtcTicks}",
            legacySeverity: NotificationSeverity.Info,
            legacyTitle: "رسید پرداخت دریافت شد",
            legacyBody: $"رسید پرداخت سفارش {order.OrderNumber} به مبلغ {PayableAmount(order):N0} تومان دریافت شد و در انتظار تأیید است.",
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
                ["Amount"] = PayableAmount(order).ToString("N0"),
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"order.cancelled:{order.Id}",
            legacySeverity: NotificationSeverity.Warning,
            legacyTitle: "سفارش لغو شد",
            legacyBody: $"سفارش {order.OrderNumber} به مبلغ {PayableAmount(order):N0} تومان به دلیل عدم پرداخت در مهلت مقرر لغو شد.",
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId));
    }

    /// <summary>لغو سفارشِ پرداخت‌نشده توسط مشتری؛ اعلان برای مشتری و (طبق قاعده) مدیران.</summary>
    public Task NotifyOrderCustomerCancelledAsync(Order order, string? reason)
    {
        var reasonText = string.IsNullOrWhiteSpace(reason) ? "—" : reason.Trim();
        return GuardAsync("order.customer_cancelled", () => EmitAsync(
            eventType: "order.customer_cancelled",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["Amount"] = PayableAmount(order).ToString("N0"),
                ["Reason"] = reasonText,
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"order.customer_cancelled:{order.Id}",
            legacySeverity: NotificationSeverity.Warning,
            legacyTitle: "سفارش لغو شد",
            legacyBody: $"سفارش {order.OrderNumber} به مبلغ {PayableAmount(order):N0} تومان توسط مشتری لغو شد. دلیل: {reasonText}",
            legacyUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId,
            phone: order.PhoneNumber));
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
        string? phone = null,
        bool superAdminsOnly = false,
        bool workshopOnly = false,
        IReadOnlyList<string>? imageUrls = null) =>
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
            phone,
            superAdminsOnly,
            workshopOnly,
            imageUrls));

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
        string? phone = null,
        bool superAdminsOnly = false,
        bool workshopOnly = false,
        IReadOnlyList<string>? imageUrls = null)
    {
        var rule = await GetRuleAsync(eventType);
        if (rule is { IsEnabled: false })
            return;

        string title;
        string body;
        string? actionUrl;
        NotificationSeverity severity;
        bool broadcast;
        var channels = NotificationChannelFlags.Internal;

        if (rule != null)
        {
            title = Render(rule.TitleTemplate, values);
            body = Render(rule.BodyTemplate, values);
            actionUrl = string.IsNullOrWhiteSpace(rule.ActionUrlTemplate) ? null : Render(rule.ActionUrlTemplate, values);
            severity = rule.Severity ?? legacySeverity;
            broadcast = rule.BroadcastToAdmins;
            channels = rule.Channels;
        }
        else if (NotificationRuleDefaults.TryGet(eventType, out var def))
        {
            title = Render(def.Title, values);
            body = Render(def.Body, values);
            actionUrl = def.ActionUrl == null ? null : Render(def.ActionUrl, values);
            severity = def.Severity ?? legacySeverity;
            broadcast = def.BroadcastToAdmins;
            channels = def.Channels;
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
            IdempotencyKey = idempotencyKey,
            ImageUrlsJson = imageUrls is { Count: > 0 }
                ? JsonSerializer.Serialize(imageUrls.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
                : null
        };

        var appUserIds = new HashSet<int>();
        if (!appUserId.HasValue && !string.IsNullOrWhiteSpace(phone))
            appUserId = await _db.Users
                .Where(x => x.PhoneNumber == phone && x.IsActive && !x.IsDeleted)
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
                    && !x.IsDeleted
                    && (!workshopUserId.HasValue || x.Id == workshopUserId.Value))
                .Select(x => x.Id)
                .ToListAsync();
            workshopUserIds.UnionWith(users);
        }

        // هشدارهای دستگاه (هم‌زمانی، حدها و ...) فقط به کاربران همان کارگاه می‌رود؛
        // اگر دستگاه به کارگاه متصل نباشد یا کارگاه کاربر فعالی نداشته باشد،
        // برای گم‌نشدن هشدار به مدیران ارسال می‌شود.
        var workshopScoped = workshopOnly && workshopId.HasValue && workshopUserIds.Count > 0;
        if (broadcast && !workshopScoped)
        {
            if (!superAdminsOnly)
            {
                var adminIds = await (
                    from ur in _db.AppUserRoles
                    join u in _db.Users on ur.UserId equals u.Id
                    join rp in _db.RolePermissions on ur.RoleId equals rp.RoleId
                    join p in _db.Permissions on rp.PermissionId equals p.Id
                    where u.IsActive && !u.IsDeleted && p.Key == "admin.notifications.view"
                    select ur.UserId)
                    .Distinct()
                    .ToListAsync();
                appUserIds.UnionWith(adminIds);
            }

            // سوپر ادمین‌های کارگاه هم مثل کاربر سایت باید همین اعلان‌ها را بگیرند.
            var (superAppIds, superWorkshopIds) = await GetSuperAdminRecipientsAsync();
            appUserIds.UnionWith(superAppIds);
            workshopUserIds.UnionWith(
                await FilterWorkshopRecipientsByPhoneAsync(superWorkshopIds, appUserIds.ToList()));

            notification.IsBroadcast = appUserIds.Count > 0 || workshopUserIds.Count > 0;
        }

        foreach (var id in appUserIds)
            notification.Recipients.Add(new NotificationRecipient { AppUserId = id });
        foreach (var id in workshopUserIds)
            notification.Recipients.Add(new NotificationRecipient { WorkshopUserId = id });

        if (notification.Recipients.Count == 0)
            return;

        await CreateDeliveriesAsync(notification, channels);

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

        await QueueExternalDeliveriesAsync(notification, effectiveChannels);
    }

    private static readonly (NotificationDeliveryChannel Channel, int Flag)[] ExternalChannelFlags =
    {
        (NotificationDeliveryChannel.Bale, NotificationChannelFlags.Bale),
        (NotificationDeliveryChannel.Telegram, NotificationChannelFlags.Telegram),
        (NotificationDeliveryChannel.Eitaa, NotificationChannelFlags.Eitaa)
    };

    private async Task QueueExternalDeliveriesAsync(Notification notification, int ruleChannels)
    {
        var flags = ruleChannels & ~NotificationChannelFlags.Internal;
        if (flags == 0 || notification.Recipients.Count == 0)
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
                // گیرنده کارگاهی هم کانال بیرونی می‌گیرد؛ شمارهٔ موبایلش
                // در سرویس تحویل (سفیر یا اتصال بله کاربر سایتِ هم‌شماره) حل می‌شود.
                if (!recipient.AppUserId.HasValue && !recipient.WorkshopUserId.HasValue)
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

    private static readonly System.Text.RegularExpressions.Regex PlaceholderPattern =
        new(@"\{([A-Za-z0-9_]+)\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// جایگزینی تک‌مرحله‌ای جای‌نگهدارها؛ متن کاربر (مثل نظر) ممکن است خودش
    /// شامل آکولاد باشد و نباید دوباره به‌عنوان جای‌نگهدار تفسیر شود.
    /// </summary>
    private static string Render(string? template, IReadOnlyDictionary<string, string?> values)
    {
        if (string.IsNullOrEmpty(template))
            return template ?? string.Empty;

        var map = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
        return PlaceholderPattern.Replace(template, match =>
            map.TryGetValue(match.Groups[1].Value, out var value) ? value ?? string.Empty : match.Value);
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

    // ساعت رسمی ایران بدون تغییر ساعت تابستانی است؛ برای کلید روزانه و
    // نمایش تاریخ شمسی از همین آفست ثابت استفاده می‌شود.
    private static readonly TimeSpan TehranOffset = TimeSpan.FromHours(3.5);

    /// <summary>کلید روزِ تهران (مثلاً ۱۴۰۵۰۸۲۰) برای یک‌بار در روز بودن یادآوری.</summary>
    private static string TehranDateKey(DateTimeOffset value)
    {
        var local = value.ToOffset(TehranOffset).DateTime;
        var pc = new PersianCalendar();
        return $"{pc.GetYear(local):0000}{pc.GetMonth(local):00}{pc.GetDayOfMonth(local):00}";
    }

    private static string JalaliDateTime(DateTimeOffset value)
    {
        var local = value.ToOffset(TehranOffset).DateTime;
        var pc = new PersianCalendar();
        return $"{pc.GetYear(local):0000}/{pc.GetMonth(local):00}/{pc.GetDayOfMonth(local):00} {local:HH:mm}";
    }

    private static string OrderProductsSummary(Order order)
    {
        if (order.Items == null || order.Items.Count == 0)
            return "—";

        return string.Join("، ", order.Items.Select(i => $"{i.ProductName} × {i.Quantity}"));
    }
}
