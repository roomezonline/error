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
    string[] Placeholders,
    int Channels = NotificationChannelFlags.Internal);

public static class NotificationRuleDefaults
{
    public static readonly NotificationRuleDef[] All =
    {
        new("order.created", "سفارش و پرداخت", "ثبت سفارش",
            "سفارش شما ثبت شد",
            "سفارش {OrderNumber} به مبلغ {Amount} تومان ثبت شد و در انتظار پرداخت است.",
            "/my-orders/{OrderId}", NotificationSeverity.Info, true, false,
            new[] { "OrderNumber", "Amount", "OrderId" }),

        new("order.created.admin", "سفارش و پرداخت", "سفارش جدید سایت (اطلاع سوپر ادمین‌ها)",
            "سفارش جدید ثبت شد",
            "سفارش {OrderNumber} به مبلغ {Amount} تومان توسط {CustomerName} ثبت شد. تلفن: {Phone}",
            "/admin/orders", NotificationSeverity.Info, true, true,
            new[] { "OrderNumber", "Amount", "CustomerName", "Phone", "OrderId" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

        new("order.created_by_admin", "سفارش و پرداخت", "ثبت سفارش توسط فروشگاه",
            "سفارش شما آماده پرداخت است",
            "سفارش {OrderNumber} به مبلغ {Amount} تومان توسط فروشگاه برای شما ثبت شد و آماده پرداخت است.",
            "/my-orders/{OrderId}", NotificationSeverity.Info, true, false,
            new[] { "OrderNumber", "Amount", "OrderId" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

        new("order.receipt_uploaded", "سفارش و پرداخت", "دریافت رسید پرداخت",
            "رسید پرداخت دریافت شد",
            "رسید پرداخت سفارش {OrderNumber} به مبلغ {Amount} تومان دریافت شد و در انتظار تأیید است.",
            "/my-orders/{OrderId}", NotificationSeverity.Info, true, false,
            new[] { "OrderNumber", "Amount", "OrderId" }),

        new("order.status_changed", "سفارش و پرداخت", "تغییر وضعیت سفارش",
            "وضعیت سفارش به‌روزرسانی شد",
            "وضعیت سفارش {OrderNumber} به مبلغ {Amount} تومان به «{Status}» تغییر کرد.",
            "/my-orders/{OrderId}", null, true, false,
            new[] { "OrderNumber", "Amount", "Status", "OrderId" }),

        new("order.stale", "سفارش و پرداخت", "یادآوری روزانهٔ سفارش تکمیل‌نشده",
            "سفارش {OrderNumber} در انتظار اقدام است",
            "مشتری: {CustomerName}\nتلفن: {Phone}\nتاریخ سفارش: {OrderDate}\nکالاها: {Products}\nمبلغ: {Amount} تومان\nوضعیت فعلی: {Status}\nمرحله فعلی: {Stage}\nآخرین تغییر وضعیت: {LastChange}\nکد رهگیری: {TrackingNumber}\n\nاز آخرین تغییر وضعیت {StaleDays} روز گذشته و اقدامی ثبت نشده است؛ لطفاً این سفارش را پیگیری کنید.",
            "/admin/orders", NotificationSeverity.Warning, true, true,
            new[] { "OrderNumber", "CustomerName", "Phone", "OrderDate", "Products", "Amount", "Status", "Stage", "LastChange", "StaleDays", "TrackingNumber", "OrderId" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

        new("order.note_added", "سفارش و پرداخت", "یادداشت مدیر برای مشتری",
            "یادداشت جدیدی برای سفارش شما ثبت شد",
            "مدیر برای سفارش {OrderNumber} یادداشت جدیدی ثبت کرد:\n{Message}",
            "/my-orders/{OrderId}", NotificationSeverity.Info, true, false,
            new[] { "OrderNumber", "Message", "ImageCount", "OrderId" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

        new("order.auto_cancelled", "سفارش و پرداخت", "لغو خودکار سفارش",
            "سفارش لغو شد",
            "سفارش {OrderNumber} به مبلغ {Amount} تومان به دلیل عدم پرداخت در مهلت مقرر لغو شد.",
            "/my-orders/{OrderId}", NotificationSeverity.Warning, true, false,
            new[] { "OrderNumber", "Amount", "OrderId" }),

        new("order.customer_cancelled", "سفارش و پرداخت", "لغو سفارش توسط مشتری",
            "سفارش لغو شد",
            "سفارش {OrderNumber} به مبلغ {Amount} تومان توسط مشتری لغو شد. دلیل: {Reason}",
            "/my-orders/{OrderId}", NotificationSeverity.Warning, true, true,
            new[] { "OrderNumber", "Amount", "Reason", "OrderId" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

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
            "{FullName} (تلفن: {Phone}) با موضوع «{Subject}» نوشت:\n{Content}",
            "/admin/contact-messages", NotificationSeverity.Info, true, true,
            new[] { "FullName", "Subject", "Phone", "Content" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

        new("ticket.created_admin", "پشتیبانی", "تیکت مشاوره جدید (اطلاع سوپر ادمین‌ها)",
            "تیکت مشاوره جدید",
            "{UserName} تیکت «{TicketTitle}» را ثبت کرد:\n{Content}",
            "/admin/technical/consultation", NotificationSeverity.Info, true, true,
            new[] { "UserName", "TicketTitle", "Content", "TicketId" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

        new("ticket.user_replied", "پشتیبانی", "پیام جدید کاربر در تیکت",
            "پیام جدید در تیکت مشاوره",
            "{UserName} در تیکت «{TicketTitle}» نوشت:\n{Content}",
            "/admin/technical/consultation", NotificationSeverity.Info, true, true,
            new[] { "UserName", "TicketTitle", "Content", "TicketId" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

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
            "هشدار پایش: {DeviceName}",
            "{Message}",
            "/admin/monitoring-devices", NotificationSeverity.Critical, true, true,
            new[] { "Message", "DeviceName", "WorkshopName", "ReceiptId" }),

        new("sms.low_balance", "پیامک", "کمبود اعتبار پیامک",
            "اعتبار پیامک رو به پایان است",
            "اعتبار پیامک شما {Balance} تومان است؛ برای ارسال پیامک جدید شارژ کنید.",
            "/admin/sms", NotificationSeverity.Warning, true, true,
            new[] { "Balance" }),

        new("sms.charge.requested", "پیامک", "درخواست شارژ پیامک",
            "درخواست شارژ پیامک ثبت شد",
            "درخواست شارژ به مبلغ {Amount} تومان ثبت شد و در انتظار بررسی است.",
            "/admin/sms", NotificationSeverity.Info, true, true,
            new[] { "Amount", "RequestId" }),

        new("sms.charge.approved", "پیامک", "تأیید شارژ پیامک",
            "درخواست شارژ پیامک تأیید شد",
            "درخواست شارژ شما به مبلغ {Amount} تومان تأیید شد و به اعتبار پیامک اضافه شد. موجودی فعلی: {Balance} تومان.",
            "/admin/sms", NotificationSeverity.Success, true, false,
            new[] { "Amount", "Balance", "RequestId" }),

        new("sms.charge.rejected", "پیامک", "رد شارژ پیامک",
            "درخواست شارژ پیامک رد شد",
            "درخواست شارژ شما به مبلغ {Amount} تومان رد شد. دلیل: {Reason}",
            "/admin/sms", NotificationSeverity.Warning, true, false,
            new[] { "Amount", "Reason", "RequestId" }),

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
            "نظر جدید برای محصول",
            "محصول: {ProductTitle}\n{UserName} (امتیاز {Rating} از ۵):\n{Content}\nوضعیت: {Status}",
            "{PagePath}", NotificationSeverity.Info, true, true,
            new[] { "UserName", "CustomerName", "ProductTitle", "Rating", "Content", "Status", "PagePath" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

        new("product.question_created", "محتوا", "پرسش جدید درباره محصول",
            "پرسش جدید برای محصول",
            "محصول: {ProductTitle}\n{UserName}:\n{Question}\nوضعیت: {Status}",
            "{PagePath}", NotificationSeverity.Info, true, true,
            new[] { "UserName", "ProductTitle", "Question", "Status", "PagePath" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

        new("product.question_replied", "محتوا", "پاسخ جدید به پرسش محصول",
            "پاسخ جدید به پرسش محصول",
            "محصول: {ProductTitle}\n{UserName}:\n{Reply}\n\nپرسش: {Question}",
            "{PagePath}", NotificationSeverity.Info, true, true,
            new[] { "UserName", "ProductTitle", "Reply", "Question", "PagePath" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

        new("news.comment_created", "محتوا", "نظر جدید مقاله",
            "نظر جدید برای خبر",
            "خبر: {NewsTitle}\n{UserName}:\n{Content}\nوضعیت: {Status}",
            "{PagePath}", NotificationSeverity.Info, true, true,
            new[] { "UserName", "NewsTitle", "Content", "Status", "PagePath" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

        new("academy.comment_created", "محتوا", "نظر جدید آموزش آکادمی",
            "نظر جدید برای آموزش",
            "آموزش: {ArticleTitle}\n{UserName}:\n{Content}\nوضعیت: {Status}",
            "{PagePath}", NotificationSeverity.Info, true, true,
            new[] { "UserName", "ArticleTitle", "Content", "Status", "PagePath" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

        new("admission.created", "پذیرش آنلاین", "درخواست پذیرش آنلاین",
            "درخواست پذیرش آنلاین جدید",
            "{CustomerName} درخواست «{WorkType}» با شماره {Phone} ثبت کرده است.",
            "/admin/online-admission", NotificationSeverity.Info, true, true,
            new[] { "CustomerName", "WorkType", "Phone" }),

        new("admission.status_changed", "پذیرش آنلاین", "تغییر وضعیت درخواست پذیرش",
            "وضعیت درخواست شما به‌روزرسانی شد",
            "وضعیت درخواست «{WorkType}» شما به «{Status}» تغییر کرد.",
            "/admission/track?id={Id}", null, true, false,
            new[] { "Id", "CustomerName", "WorkType", "Status", "Phone" },
            NotificationChannelFlags.Internal | NotificationChannelFlags.Bale),

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
            new[] { "SessionId", "UserName" }),

        new("product.restocked", "فروشگاه", "موجود شدن کالا",
            "کالای مورد نظرتان موجود شد",
            "کالای «{ProductName}» موجود شد؛ همین حالا سفارش دهید.",
            "/products/{ProductId}", NotificationSeverity.Success, true, false,
            new[] { "ProductName", "ProductId", "StockQuantity" })
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
