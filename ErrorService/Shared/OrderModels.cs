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

public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
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
    public decimal? DiscountAmount { get; set; }
    public string? CouponCode { get; set; }

    public string? AccessToken { get; set; }
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
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
