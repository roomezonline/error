using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public enum OrderStatus
{
    PendingPayment,
    ReceiptUploaded,
    Approved,
    Rejected,
    Cancelled,
    Completed
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

    public PaymentProvider? PaymentProvider { get; set; }
    public string? PaymentReference { get; set; }

    public List<OrderItemDto> Items { get; set; } = new();
    public List<OrderShipmentDto> Shipments { get; set; } = new();
    public List<OrderEventDto> Events { get; set; } = new();
    public List<OrderReturnDto> Returns { get; set; } = new();
    public decimal? DiscountAmount { get; set; }
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
