using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ErrorService.Shared;

public enum OrderStatus
{
    PendingPayment,
    ReceiptUploaded,
    Approved,
    Rejected,
    Cancelled,
    Completed,

    /// <summary>پیش‌فاکتور: بدون رزرو موجودی و بدون مصرف کوپن؛ فقط پس از انتشار یا ثبت پرداخت به سفارش واقعی تبدیل می‌شود.</summary>
    Draft = 6
}

/// <summary>منشأ ایجاد سفارش/پیش‌فاکتور برای تفکیک سفارش‌های مشتری و سفارش‌های ساخته‌شده توسط ادمین.</summary>
public enum OrderOrigin
{
    Customer = 0,
    AdminCart = 1,
    AdminProforma = 2,
    AdminPayment = 3
}

public enum OrderShipmentStatus
{
    Pending,
    Shipped,
    InTransit,
    Delivered,
    Failed,
    Returned,
    Cancelled
}

public enum OrderStage
{
    PaymentConfirmed = 1,
    Processing = 2,
    ActionPending = 3,
    Collecting = 4,
    Collected = 5,
    SentToUnit = 6,
    Resolved = 7
}

public static class OrderStageInfo
{
    public static readonly OrderStage[] Flow =
    {
        OrderStage.PaymentConfirmed,
        OrderStage.Processing,
        OrderStage.ActionPending,
        OrderStage.Collecting,
        OrderStage.Collected,
        OrderStage.SentToUnit,
        OrderStage.Resolved
    };

    public static string Label(OrderStage stage) => stage switch
    {
        OrderStage.PaymentConfirmed => "پرداخت تایید شده",
        OrderStage.Processing => "درحال پردازش",
        OrderStage.ActionPending => "در دست اقدام",
        OrderStage.Collecting => "درحال جمع‌آوری",
        OrderStage.Collected => "جمع آوری شده",
        OrderStage.SentToUnit => "ارسال به واحد حمل",
        OrderStage.Resolved => "حمل شده",
        _ => stage.ToString()
    };

    public static string Icon(OrderStage stage) => stage switch
    {
        OrderStage.PaymentConfirmed => "oi-credit-card",
        OrderStage.Processing => "oi-cog",
        OrderStage.ActionPending => "oi-bolt",
        OrderStage.Collecting => "oi-box",
        OrderStage.Collected => "oi-box",
        OrderStage.SentToUnit => "oi-truck",
        OrderStage.Resolved => "oi-truck",
        _ => "oi-arrow-right"
    };

    public static string Color(OrderStage stage) => stage switch
    {
        OrderStage.PaymentConfirmed => "#22c55e",
        OrderStage.Processing => "#3b82f6",
        OrderStage.ActionPending => "#78350f",
        OrderStage.Collecting => "#14b8a6",
        OrderStage.Collected => "#f97316",
        OrderStage.SentToUnit => "#a855f7",
        OrderStage.Resolved => "#7c3aed",
        _ => "#64748b"
    };
}

public class OrderDto
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public OrderStage? Stage { get; set; }
    public Dictionary<string, DateTimeOffset> StageDates { get; set; } = new();
    public decimal TotalAmount { get; set; }
    
    // Buyer Info
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }

    // Location
    public int? ProvinceId { get; set; }
    public int? CityId { get; set; }
    public string? ProvinceName { get; set; }
    public string? CityName { get; set; }
    public string? PostalCode { get; set; }
    
    // Payment Info
    public string? ReceiptImageUrl { get; set; }
    public List<string> ReceiptImageUrls { get; set; } = new();
    public string? TrackingNumber { get; set; }
    public DateTimeOffset? PaymentDate { get; set; }
    public string? AdminNotes { get; set; }
    public string? CustomerNotes { get; set; }

    public PaymentProvider? PaymentProvider { get; set; }
    public string? PaymentReference { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentAuthority { get; set; }
    public DateTimeOffset? PaymentInitiatedAt { get; set; }

    public OrderOrigin Origin { get; set; }
    public int? CreatedByAdminId { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();
    public List<OrderShipmentDto> Shipments { get; set; } = new();
    public List<OrderEventDto> Events { get; set; } = new();
    public List<OrderReturnDto> Returns { get; set; } = new();
    public decimal? DiscountAmount { get; set; }
    public decimal? ManualDiscountAmount { get; set; }
    public string? CouponCode { get; set; }

    public string? AccessToken { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int ShipmentCount { get; set; }
    public string? LatestTrackingNumber { get; set; }
    public string? NextAction { get; set; }

    // Shipping
    public int? ShippingMethodId { get; set; }
    public string? ShippingMethodName { get; set; }
    public decimal ShippingCost { get; set; }

    /// <summary>هزینه ارسال این سفارش رایگان بوده است؟</summary>
    public bool ShippingIsFree { get; set; }

    // Returns
    public int ReturnCount { get; set; }
}

public class OrderShipmentDto
{
    public int Id { get; set; }
    public int AttemptNumber { get; set; }
    public string Carrier { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? ShippedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class OrderEventDto
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus? ToStatus { get; set; }
    public string? Message { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public string DisplayMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Message)) return string.Empty;
            var pipe = Message.IndexOf('|');
            if (pipe < 0) return Message.Trim();
            var text = Message[..pipe].Trim();
            if (text.Length > 0) return text;
            return Message.Trim();
        }
    }
}

public class OrderShipmentCreateDto
{
    [Required(ErrorMessage = "شرکت حمل الزامی است")]
    [MaxLength(100)]
    public string Carrier { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? TrackingNumber { get; set; }
    public string? Notes { get; set; }
    public string? FailureReason { get; set; }
}

public class OrderShipmentUpdateDto
{
    [MaxLength(100)]
    public string? Carrier { get; set; }
    public string? TrackingNumber { get; set; }
    public string? Notes { get; set; }
    public string? FailureReason { get; set; }
    public OrderShipmentStatus? Status { get; set; }
}

public class OrderAdminListResponse
{
    public List<OrderDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int PendingActionCount { get; set; }
}

public class OrderAdminSummaryDto
{
    public int TotalCount { get; set; }
    public int DraftCount { get; set; }
    public int PendingActionCount { get; set; }
    public int PendingReceiptCount { get; set; }
    public int ReadyToShipCount { get; set; }
    public int FailedShipmentCount { get; set; }
    public int MissingTrackingCount { get; set; }
    public decimal TodayAmount { get; set; }
    public Dictionary<int, int> StageCounts { get; set; } = new();
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string? ImageUrl { get; set; }
}

public class OrderCancelRequestDto
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}

/// <summary>نرمال‌سازی کد پستی ایرانی (حذف فاصله/خط تیره و تبدیل ارقام فارسی و عربی).</summary>
public static class OrderAddressHelper
{
    public static string NormalizePostalCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch is >= '۰' and <= '۹')
                sb.Append((char)('0' + (ch - '۰')));
            else if (ch is >= '٠' and <= '٩')
                sb.Append((char)('0' + (ch - '٠')));
            else if (ch is >= '0' and <= '9')
                sb.Append(ch);
        }

        return sb.ToString();
    }

    public static bool IsValidPostalCode(string? value) => NormalizePostalCode(value).Length == 10;
}

public class OrderCreateRequest
{
    [Required(ErrorMessage = "نام وارد نشده است")]
    [MinLength(3, ErrorMessage = "نام کوتاه است")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "موبایل وارد نشده است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت موبایل صحیح نیست")]
    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    [Required(ErrorMessage = "آدرس وارد نشده است")]
    [MinLength(10, ErrorMessage = "آدرس دقیق نیست")]
    public string? Address { get; set; }

    public int? ProvinceId { get; set; }
    public int? CityId { get; set; }
    public string? ProvinceName { get; set; }
    public string? CityName { get; set; }

    [MaxLength(20, ErrorMessage = "کد پستی معتبر نیست")]
    public string? PostalCode { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();

    public string? CouponCode { get; set; }

    public int? ShippingMethodId { get; set; }

    /// <summary>
    /// کلید یکتای هر تلاش برای ثبت سفارش؛ با ارسال دوبارهٔ همان کلید،
    /// سفارش تکراری ساخته نمی‌شود.
    /// </summary>
    [MaxLength(64)]
    public string? IdempotencyKey { get; set; }
}

public class ReceiptUploadRequest
{
    public string? TrackingNumber { get; set; }
    public DateTimeOffset? PaymentDate { get; set; }
}

public class OrderStatusUpdateDto
{
    public OrderStatus Status { get; set; }
    public string? AdminNotes { get; set; }
}

public class OrderStageUpdateDto
{
    public OrderStage Stage { get; set; }
    public string? AdminNotes { get; set; }
}

public class OrderUpdateInfoRequest
{
    [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل معتبر نیست")]
    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    [Required(ErrorMessage = "آدرس پستی الزامی است")]
    public string Address { get; set; } = string.Empty;

    public int? ProvinceId { get; set; }
    public int? CityId { get; set; }
    public string? ProvinceName { get; set; }
    public string? CityName { get; set; }
    public string? PostalCode { get; set; }
}

public class CartItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public class CartItemUpdateDto
{
    public int Quantity { get; set; }
}

public class CouponDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public int? MaxUsageCount { get; set; }
    public int CurrentUsageCount { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? ExpiryDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public string? AssignedToUserPhone { get; set; }
    public bool IsUsedByMe { get; set; }
}

public class CouponApplyRequest
{
    public string Code { get; set; } = string.Empty;
    public decimal OrderAmount { get; set; }
}

public class CouponApplyResult
{
    public bool IsValid { get; set; }
    public string? Message { get; set; }
    public decimal? DiscountAmount { get; set; }
    public CouponDto? Coupon { get; set; }
}

public class ShippingMethodDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? EstimatedDays { get; set; }
    public bool IsActive { get; set; }

    /// <summary>ارسال رایگان — فقط با تیک «رایگان» هنگام تعریف.</summary>
    public bool IsFree { get; set; }
    public int SortOrder { get; set; }
}

public class ShippingMethodCreateRequest
{
    [Required(ErrorMessage = "نام الزامی است")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public decimal Price { get; set; }

    [MaxLength(50)]
    public string? EstimatedDays { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>ارسال رایگان — فقط با تیک «رایگان» هنگام تعریف.</summary>
    public bool IsFree { get; set; }

    public int SortOrder { get; set; }
}

public class OrderReturnDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public decimal RefundAmount { get; set; }
    public string? RefundCardNumber { get; set; }
    public string? RefundBankName { get; set; }
    public DateTimeOffset? RefundDate { get; set; }
    public string? RefundReceiptImageUrl { get; set; }
    public string? ReturnReason { get; set; }
    public string? AdminNotes { get; set; }
    public bool IsRefunded { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    /// <summary>درخواست مرجوعی مشتری (بدون اطلاعات بانکی) — فقط توسط خود مشتری ثبت می‌شود.</summary>
    public bool IsCustomerRequest { get; set; }
}

public class OrderReturnCreateRequest
{
    public decimal RefundAmount { get; set; }

    [MaxLength(100)]
    public string? RefundCardNumber { get; set; }

    [MaxLength(200)]
    public string? RefundBankName { get; set; }

    public DateTimeOffset? RefundDate { get; set; }

    [MaxLength(500)]
    public string? ReturnReason { get; set; }

    [MaxLength(1000)]
    public string? AdminNotes { get; set; }
}

public class OrderReturnRequestDto
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "دلیل مرجوعی الزامی است")]
    [System.ComponentModel.DataAnnotations.MinLength(5, ErrorMessage = "دلیل مرجوعی باید حداقل ۵ حرف باشد")]
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class OrderReturnRefundRequest
{
    [Required]
    [MaxLength(100)]
    public string RefundCardNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? RefundBankName { get; set; }

    public DateTimeOffset? RefundDate { get; set; }

    [MaxLength(500)]
    public string? RefundReceiptImageUrl { get; set; }

    [MaxLength(1000)]
    public string? AdminNotes { get; set; }
}

/// <summary>
/// سفارش نیمه‌تمام کاربر: ثبت شده ولی هنوز پرداخت/تایید نشده است.
/// برای بنر «ادامه سفارش» در پروفایل، سربرگ و بعد از ورود مجدد استفاده می‌شود.
/// </summary>
public class UnfinishedOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>نام وضعیت (PendingPayment / ReceiptUploaded).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>برچسب فارسی وضعیت.</summary>
    public string StatusLabel { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemsCount { get; set; }

    /// <summary>آدرس ادامهٔ سفارش (ثبت رسید یا پرداخت مجدد).</summary>
    public string ResumeUrl { get; set; } = string.Empty;

    /// <summary>true = باید رسید آپلود/پرداخت کند؛ false = فقط منتظر تایید ادمین است.</summary>
    public bool NeedsPayment { get; set; }

    /// <summary>دقیقه‌های باقی‌مانده تا لغو خودکار (فقط برای سفارش در انتظار پرداخت).</summary>
    public int MinutesToAutoCancel { get; set; }
}

// ───────────────────────────── سبد خرید نسخه‌دار ─────────────────────────────

/// <summary>وضعیت سبد سروری به‌همراه نسخه؛ کلاینت با مقایسهٔ نسخه، تغییرات ادمین را بازنویسی نمی‌کند.</summary>
public class CartStateDto
{
    public long Revision { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public List<CartItemDto> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
}

/// <summary>همگام‌سازی کامل سبد؛ اگر BaseRevision با نسخهٔ سرور نخواند، سرور برنده است.</summary>
public class CartSyncRequest
{
    public List<CartItemDto> Items { get; set; } = new();
    /// <summary>۰ = کاربر مهمان است و سبد محلی باید با سبد حساب merge شود.</summary>
    public long BaseRevision { get; set; }
}

public class CartSyncResult
{
    public bool Accepted { get; set; }
    public string? Message { get; set; }
    public CartStateDto State { get; set; } = new();
}

// ───────────────────────────── جست‌وجوی مشتری و سبد ادمین ─────────────────────────────

/// <summary>اطلاعات مشتری برای انتخاب در پنل ادمین (بدون رمز یا داده‌های حساس).</summary>
public class AdminCustomerLookupDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public int CartItemCount { get; set; }
    public DateTimeOffset? LastOrderAt { get; set; }
}

public class AdminCartItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public int Quantity { get; set; }
    public int StockQuantity { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsDraft { get; set; }
    /// <summary>مشکل فعلی آیتم (ناموجود، بیش از موجودی، حذف‌شده) برای دکمهٔ «ترمیم».</summary>
    public string? Issue { get; set; }
}

public class AdminCartDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public long Revision { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public List<AdminCartItemDto> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
}

public class AdminCartSaveRequest
{
    public List<AdminCartItemDto> Items { get; set; } = new();
    /// <summary>نسخه‌ای که ادمین دیده است؛ در صورت تغییر همزمان سبد، ذخیره رد می‌شود.</summary>
    public long ExpectedRevision { get; set; }
    public string? Note { get; set; }
}

public class AdminCartSaveResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public AdminCartDto? Cart { get; set; }
}

/// <summary>تبدیل سبد (اصلاح‌شده) به پیش‌فاکتور یا سفارش آمادهٔ پرداخت.</summary>
public class AdminCartCheckoutRequest
{
    public string? CouponCode { get; set; }
    public decimal? ManualDiscountAmount { get; set; }
    public int? ShippingMethodId { get; set; }
    public string? CustomerNotes { get; set; }
    public string? AdminNotes { get; set; }
    /// <summary>true = سفارش آمادهٔ پرداخت؛ false = پیش‌فاکتور (Draft).</summary>
    public bool Publish { get; set; } = true;
    public int? ExpiresInHours { get; set; }
}

public class AdminCartNotifyRequest
{
    public string? Message { get; set; }
}

// ───────────────────────────── سفارش/پیش‌فاکتور ادمین ─────────────────────────────

public class AdminOrderSaveRequest
{
    /// <summary>فقط در ایجاد سفارش جدید: مشتری مقصد.</summary>
    public int? UserId { get; set; }

    [Required(ErrorMessage = "نام وارد نشده است")]
    [MinLength(3, ErrorMessage = "نام کوتاه است")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "موبایل وارد نشده است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت موبایل صحیح نیست")]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "آدرس وارد نشده است")]
    [MinLength(10, ErrorMessage = "آدرس دقیق نیست")]
    public string Address { get; set; } = string.Empty;

    public int? ProvinceId { get; set; }
    public int? CityId { get; set; }
    public string? ProvinceName { get; set; }
    public string? CityName { get; set; }
    [MaxLength(20)]
    public string? PostalCode { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();
    public string? CouponCode { get; set; }

    /// <summary>تخفیف دستی ادمین (تومان) — مستقل از کوپن و روی مبلغ کالاها.</summary>
    public decimal? ManualDiscountAmount { get; set; }

    public int? ShippingMethodId { get; set; }
    public string? CustomerNotes { get; set; }
    public string? AdminNotes { get; set; }

    /// <summary>در ایجاد: true = آمادهٔ پرداخت، false = پیش‌فاکتور.</summary>
    public bool Publish { get; set; }
    /// <summary>مهلت پرداخت به ساعت؛ پیش‌فرض ۲۴ و حداکثر ۱۶۸ ساعت.</summary>
    public int? ExpiresInHours { get; set; }
}

public class AdminOrderMarkPaidRequest
{
    /// <summary>cash | card_to_card | pos | transfer | other</summary>
    [Required(ErrorMessage = "روش پرداخت الزامی است")]
    [MaxLength(30)]
    public string PaymentMethod { get; set; } = "cash";

    [MaxLength(200)]
    public string? Reference { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    [MaxLength(500)]
    public string? ReceiptUrl { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class AdminOrderExtendRequest
{
    /// <summary>تمدید مهلت پرداخت به ساعت (۱ تا ۱۶۸).</summary>
    public int Hours { get; set; } = 24;
}

public class AdminPaymentLinkDto
{
    public int OrderId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
}
