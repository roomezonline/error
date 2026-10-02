using ErrorService.Shared;

namespace ErrorService.Server.Models;

public sealed record NotificationRuleDef(
    string EventType,
    string Group,
    string Label,
    string Title,
    string Body,
    string? ActionUrl,
    NotificationSeverity? Severity,
    bool IsEnabled,
    bool BroadcastToAdmins,
    string[] Placeholders);

public static class NotificationRuleDefaults
{
    public static readonly NotificationRuleDef[] All =
    {
        new("order.created", "سفارش و پرداخت", "ثبت سفارش",
            "سفارش شما ثبت شد",
            "سفارش {OrderNumber} به مبلغ {Amount} تومان ثبت شد و در انتظار پرداخت است.",
            "/my-orders/{OrderId}", NotificationSeverity.Info, true, false,
            new[] { "OrderNumber", "Amount", "OrderId" }),

        new("order.receipt_uploaded", "سفارش و پرداخت", "دریافت رسید پرداخت",
            "رسید پرداخت دریافت شد",
            "رسید پرداخت سفارش {OrderNumber} دریافت شد و در انتظار تأیید است.",
            "/my-orders/{OrderId}", NotificationSeverity.Info, true, false,
            new[] { "OrderNumber", "OrderId" }),

        new("order.status_changed", "سفارش و پرداخت", "تغییر وضعیت سفارش",
            "وضعیت سفارش به‌روزرسانی شد",
            "وضعیت سفارش {OrderNumber} به «{Status}» تغییر کرد.",
            "/my-orders/{OrderId}", null, true, false,
            new[] { "OrderNumber", "Status", "OrderId" }),

        new("order.auto_cancelled", "سفارش و پرداخت", "لغو خودکار سفارش",
            "سفارش لغو شد",
            "سفارش {OrderNumber} به دلیل عدم پرداخت در مهلت مقرر لغو شد.",
            "/my-orders/{OrderId}", NotificationSeverity.Warning, true, false,
            new[] { "OrderNumber", "OrderId" }),

        new("return.created", "مرجوعی", "ثبت درخواست مرجوعی",
            "درخواست مرجوعی ثبت شد",
            "درخواست مرجوعی سفارش {OrderNumber} به مبلغ {RefundAmount} تومان ثبت شد و در حال بررسی است.",
            "/my-orders/{OrderId}", NotificationSeverity.Info, true, false,
            new[] { "OrderNumber", "RefundAmount", "OrderId" }),

        new("return.refunded", "مرجوعی", "واریز مرجوعی",
            "واریز مرجوعی ثبت شد",
            "مبلغ {RefundAmount} تومان بابت مرجوعی سفارش {OrderNumber} واریز شد.",
            "/my-orders/{OrderId}", NotificationSeverity.Success, true, false,
            new[] { "OrderNumber", "RefundAmount", "OrderId" }),

        new("receipt.created", "رسید خدمات", "رسید جدید کارگاه",
            "رسید جدید ثبت شد",
            "رسید شماره {ReceiptId} {ReceiptNote}.",
            "/admin/receipts/{ReceiptId}", NotificationSeverity.Info, true, false,
            new[] { "ReceiptId", "ReceiptNote" }),

        new("receipt.status_changed", "رسید خدمات", "تغییر وضعیت رسید",
            "{StatusTitle}",
            "وضعیت رسید شماره {ReceiptId} تغییر کرد.",
            "/admin/receipts/{ReceiptId}", null, true, false,
            new[] { "ReceiptId", "Status", "StatusTitle" }),

        new("receipt.technician_assigned", "رسید خدمات", "اختصاص تکنسین",
            "کار جدید به شما اختصاص یافت",
            "رسید شماره {ReceiptId} برای بررسی به شما اختصاص داده شد.",
            "/admin/receipts/{ReceiptId}", NotificationSeverity.Info, true, false,
            new[] { "ReceiptId" }),

        new("order.stage.payment_confirmed", "مراحل سفارش (استپر)", "مرحله: پرداخت تایید شده",
            "مرحله سفارش تغییر کرد",
            "سفارش {OrderNumber} به مرحله «{Stage}» رسید.",
            "/my-orders/{OrderId}", NotificationSeverity.Info, false, false,
            new[] { "OrderNumber", "Stage", "Amount", "OrderId" }),

        new("order.stage.processing", "مراحل سفارش (استپر)", "مرحله: درحال پردازش",
            "مرحله سفارش تغییر کرد",
            "سفارش {OrderNumber} به مرحله «{Stage}» رسید.",
            "/my-orders/{OrderId}", NotificationSeverity.Info, true, false,
            new[] { "OrderNumber", "Stage", "Amount", "OrderId" }),

        new("order.stage.action_pending", "مراحل سفارش (استپر)", "مرحله: در دست اقدام",
            "مرحله سفارش تغییر کرد",
            "سفارش {OrderNumber} به مرحله «{Stage}» رسید.",
            "/my-orders/{OrderId}", NotificationSeverity.Info, true, false,
            new[] { "OrderNumber", "Stage", "Amount", "OrderId" }),

        new("order.stage.collecting", "مراحل سفارش (استپر)", "مرحله: درحال جمع‌آوری",
            "مرحله سفارش تغییر کرد",
            "سفارش {OrderNumber} به مرحله «{Stage}» رسید.",
            "/my-orders/{OrderId}", NotificationSeverity.Info, true, false,
            new[] { "OrderNumber", "Stage", "Amount", "OrderId" }),

        new("order.stage.collected", "مراحل سفارش (استپر)", "مرحله: جمع آوری شده",
            "مرحله سفارش تغییر کرد",
            "سفارش {OrderNumber} به مرحله «{Stage}» رسید.",
            "/my-orders/{OrderId}", NotificationSeverity.Info, true, false,
            new[] { "OrderNumber", "Stage", "Amount", "OrderId" }),

        new("order.stage.sent_to_unit", "مراحل سفارش (استپر)", "مرحله: ارسال به واحد حمل",
            "مرحله سفارش تغییر کرد",
            "سفارش {OrderNumber} به مرحله «{Stage}» رسید.",
            "/my-orders/{OrderId}", NotificationSeverity.Info, true, false,
            new[] { "OrderNumber", "Stage", "Amount", "OrderId" }),

        new("order.stage.resolved", "مراحل سفارش (استپر)", "مرحله: حل شده",
            "مرحله سفارش تغییر کرد",
            "سفارش {OrderNumber} به مرحله «{Stage}» رسید.",
            "/my-orders/{OrderId}", NotificationSeverity.Success, true, false,
            new[] { "OrderNumber", "Stage", "Amount", "OrderId" }),

        new("payment.succeeded", "سفارش و پرداخت", "پرداخت موفق",
            "پرداخت شما با موفقیت انجام شد",
            "مبلغ {Amount} تومان برای سفارش {OrderNumber} پرداخت و تأیید شد.",
            "/my-orders/{OrderId}", NotificationSeverity.Success, true, false,
            new[] { "OrderNumber", "Amount", "OrderId" }),

        new("payment.failed", "سفارش و پرداخت", "پرداخت ناموفق",
            "پرداخت ناموفق بود",
            "پرداخت سفارش {OrderNumber} به مبلغ {Amount} تومان ناموفق بود؛ لطفاً دوباره تلاش کنید.",
            "/my-orders/{OrderId}", NotificationSeverity.Warning, true, false,
            new[] { "OrderNumber", "Amount", "OrderId" }),

        new("shipment.created", "سفارش و پرداخت", "ثبت مرسوله",
            "مرسوله شما ثبت شد",
            "مرسوله سفارش {OrderNumber} با شرکت حمل {Carrier} ثبت شد. شماره بارنامه: {TrackingNumber}.",
            "/my-orders/{OrderId}", NotificationSeverity.Info, true, false,
            new[] { "OrderNumber", "Carrier", "TrackingNumber", "OrderId" }),

        new("shipment.status_changed", "سفارش و پرداخت", "تغییر وضعیت مرسوله",
            "وضعیت مرسوله تغییر کرد",
            "وضعیت مرسوله سفارش {OrderNumber} به «{Status}» تغییر کرد.",
            "/my-orders/{OrderId}", null, true, false,
            new[] { "OrderNumber", "Status", "TrackingNumber", "OrderId" }),

        new("return.rejected", "مرجوعی", "رد درخواست مرجوعی",
            "درخواست مرجوعی رد شد",
            "درخواست مرجوعی سفارش {OrderNumber} رد شد. دلیل: {Reason}",
            "/my-orders/{OrderId}", NotificationSeverity.Warning, true, false,
            new[] { "OrderNumber", "Reason", "OrderId" }),

        new("ticket.status_changed", "پشتیبانی", "تغییر وضعیت تیکت",
            "وضعیت تیکت شما تغییر کرد",
            "وضعیت تیکت «{TicketTitle}» به «{Status}» تغییر کرد.",
            "/technical/consultation", NotificationSeverity.Info, true, false,
            new[] { "TicketTitle", "Status", "TicketId" }),

        new("ticket.replied", "پشتیبانی", "پاسخ جدید تیکت",
            "پاسخ جدید دریافت شد",
            "به تیکت «{TicketTitle}» پاسخ جدید ثبت شد.",
            "/technical/consultation", NotificationSeverity.Info, true, false,
            new[] { "TicketTitle", "TicketId" }),

        new("contact.created", "پشتیبانی", "پیام جدید تماس با ما",
            "پیام جدید تماس با ما",
            "{FullName} با موضوع «{Subject}» پیامی ثبت کرده است. تلفن: {Phone}",
            "/admin/contact-messages", NotificationSeverity.Info, true, true,
            new[] { "FullName", "Subject", "Phone" }),

        new("user.registered", "حساب کاربری", "خوش‌آمدگویی ثبت‌نام",
            "به ارورسرویس خوش آمدید",
            "حساب کاربری شما با شماره {Phone} ساخته شد.",
            null, NotificationSeverity.Success, true, false,
            new[] { "Phone", "FullName" }),

        new("monitoring.expiring_soon", "پایش دستگاه‌ها", "نزدیک شدن به انقضای پایش",
            "انقضای سرویس پایش نزدیک است",
            "سرویس پایش دستگاه {DeviceName} در تاریخ {EndDate} به پایان می‌رسد.",
            "/admin/monitoring/renewal-requests", NotificationSeverity.Warning, true, true,
            new[] { "DeviceName", "EndDate" }),

        new("monitoring.expired", "پایش دستگاه‌ها", "انقضای سرویس پایش",
            "سرویس پایش دستگاه منقضی شد",
            "سرویس پایش دستگاه {DeviceName} منقضی شده است؛ برای ادامه پایش اقدام به تمدید کنید.",
            "/admin/monitoring/renewal-requests", NotificationSeverity.Critical, true, true,
            new[] { "DeviceName", "EndDate" }),

        new("renewal.requested", "پایش دستگاه‌ها", "درخواست تمدید پایش",
            "درخواست تمدید پایش ثبت شد",
            "درخواست تمدید سرویس پایش {DeviceName} ثبت شد و در انتظار بررسی است.",
            "/admin/monitoring/renewal-requests", NotificationSeverity.Info, true, true,
            new[] { "DeviceName", "RequestId" }),

        new("renewal.approved", "پایش دستگاه‌ها", "تأیید تمدید پایش",
            "تمدید سرویس پایش تأیید شد",
            "تمدید سرویس پایش {DeviceName} تأیید شد و تا تاریخ {EndDate} فعال است.",
            "/admin/monitoring/my-devices", NotificationSeverity.Success, true, false,
            new[] { "DeviceName", "EndDate" }),

        new("renewal.rejected", "پایش دستگاه‌ها", "رد تمدید پایش",
            "درخواست تمدید پایش رد شد",
            "درخواست تمدید سرویس پایش {DeviceName} رد شد. دلیل: {Reason}",
            "/admin/monitoring/renewal-requests", NotificationSeverity.Warning, true, false,
            new[] { "DeviceName", "Reason" }),

        new("monitoring.alert", "پایش دستگاه‌ها", "هشدار پایش دستگاه",
            "هشدار پایش دستگاه",
            "{Message}",
            "/admin/monitoring-devices", NotificationSeverity.Critical, true, true,
            new[] { "Message", "DeviceName" }),

        new("sms.low_balance", "پیامک", "کمبود اعتبار پیامک",
            "اعتبار پیامک رو به پایان است",
            "اعتبار پیامک شما {Balance} تومان است؛ برای ارسال پیامک جدید شارژ کنید.",
            "/admin/sms", NotificationSeverity.Warning, true, true,
            new[] { "Balance" }),

        new("backup.failed", "پشتیبان‌گیری", "خطای پشتیبان‌گیری",
            "پشتیبان‌گیری ناموفق بود",
            "پشتیبان‌گیری پایگاه داده با خطا مواجه شد: {Error}",
            "/admin/backup", NotificationSeverity.Critical, true, true,
            new[] { "Error" }),

        new("notification.delivery_failed", "اعلان‌ها", "عدم تحویل اعلان‌ها",
            "برخی اعلان‌ها تحویل نشدند",
            "{Count} اعلان در صف تحویل ناموفق ماند و برای ارسال مجدد برنامه‌ریزی شد.",
            "/admin/notifications", NotificationSeverity.Critical, true, true,
            new[] { "Count" }),

        new("review.created", "محتوا", "نظر جدید درباره محصول",
            "نظر جدید ثبت شد",
            "{CustomerName} برای محصول «{ProductTitle}» نظر ثبت کرده است و در انتظار تأیید است.",
            "/admin/product-reviews", NotificationSeverity.Info, true, true,
            new[] { "CustomerName", "ProductTitle" }),

        new("news.comment_created", "محتوا", "نظر جدید مقاله",
            "نظر جدید مقاله ثبت شد",
            "{UserName} برای «{NewsTitle}» نظر ثبت کرده است.",
            "/admin/comments", NotificationSeverity.Info, true, true,
            new[] { "UserName", "NewsTitle" }),

        new("admission.created", "پذیرش آنلاین", "درخواست پذیرش آنلاین",
            "درخواست پذیرش آنلاین جدید",
            "{CustomerName} درخواست «{WorkType}» با شماره {Phone} ثبت کرده است.",
            "/admin/online-admission", NotificationSeverity.Info, true, true,
            new[] { "CustomerName", "WorkType", "Phone" }),

        new("commitment.created", "تعهدنامه دیجیتال", "ثبت تعهدنامه",
            "تعهدنامه جدید ثبت شد",
            "تعهدنامه {CustomerName} ثبت شد و در انتظار امضا است.",
            "/commitment", NotificationSeverity.Info, true, false,
            new[] { "CustomerName", "CommitmentId" }),

        new("commitment.signed", "تعهدنامه دیجیتال", "امضای تعهدنامه",
            "تعهدنامه امضا شد",
            "تعهدنامه {CustomerName} امضا و نهایی شد.",
            "/commitment", NotificationSeverity.Success, true, false,
            new[] { "CustomerName", "CommitmentId" }),

        new("chat.session_rated", "پشتیبانی", "امتیاز گفتگوی پشتیبانی",
            "امتیاز جدید برای گفتگو",
            "مشتری {UserName} به گفتگوی #{SessionId} امتیاز {Rating} از ۵ داد.",
            "/admin/chat", NotificationSeverity.Info, true, false,
            new[] { "UserName", "SessionId", "Rating" }),

        new("chat.session_transferred", "پشتیبانی", "انتقال گفتگو",
            "گفتگو به شما منتقل شد",
            "گفتگوی #{SessionId} با {UserName} به شما منتقل شد.",
            "/admin/chat", NotificationSeverity.Info, true, false,
            new[] { "SessionId", "UserName" })
    };

    private static readonly Dictionary<string, string[]> PlaceholderMap =
        All.ToDictionary(x => x.EventType, x => x.Placeholders, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, NotificationRuleDef> DefMap =
        All.ToDictionary(x => x.EventType, x => x, StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string eventType, out NotificationRuleDef def)
        => DefMap.TryGetValue(eventType, out def!);

    public static string[] PlaceholdersFor(string eventType)
        => PlaceholderMap.TryGetValue(eventType, out var list) ? list : Array.Empty<string>();
}
