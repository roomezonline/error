using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErrorService.Shared;

namespace ErrorService.Server.Models;

public class CartItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = default!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Order
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public AppUser? User { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;
    public OrderStage? Stage { get; set; }
    public bool StockDeducted { get; set; }
    public decimal TotalAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountAmount { get; set; }
    public int? CouponId { get; set; }
    public string? CouponCode { get; set; }

    // Buyer Info
    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(1000)]
    public string? Address { get; set; }

    // Location (province/city/postal) - additive & optional
    public int? ProvinceId { get; set; }
    public int? CityId { get; set; }
    [MaxLength(100)]
    public string? ProvinceName { get; set; }
    [MaxLength(100)]
    public string? CityName { get; set; }
    [MaxLength(20)]
    public string? PostalCode { get; set; }

    // Payment Info
    [MaxLength(500)]
    public string? ReceiptImageUrl { get; set; }

    [MaxLength(2000)]
    public string? ReceiptImageUrlsJson { get; set; }

    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    public DateTimeOffset? PaymentDate { get; set; }

    // Online gateway info (additive & optional)
    public PaymentProvider? PaymentProvider { get; set; }

    [MaxLength(200)]
    public string? PaymentReference { get; set; }

    [MaxLength(1000)]
    public string? AdminNotes { get; set; }

    [MaxLength(64)]
    public string? AccessToken { get; set; }

    // Shipping
    public int? ShippingMethodId { get; set; }
    [MaxLength(100)]
    public string? ShippingMethodName { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingCost { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderShipment> Shipments { get; set; } = new List<OrderShipment>();
    public ICollection<OrderEvent> Events { get; set; } = new List<OrderEvent>();
    public ICollection<OrderReturn> Returns { get; set; } = new List<OrderReturn>();
}

public class OrderShipment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public int AttemptNumber { get; set; } = 1;
    public string Carrier { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public OrderShipmentStatus Status { get; set; } = OrderShipmentStatus.Pending;
    public string? FailureReason { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? ShippedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
}

public class OrderEvent
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public string EventType { get; set; } = string.Empty;
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus? ToStatus { get; set; }
    public string? Message { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class OrderReturn
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = default!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal RefundAmount { get; set; }

    [MaxLength(100)]
    public string? RefundCardNumber { get; set; }

    [MaxLength(200)]
    public string? RefundBankName { get; set; }

    public DateTimeOffset? RefundDate { get; set; }

    [MaxLength(500)]
    public string? RefundReceiptImageUrl { get; set; }

    [MaxLength(500)]
    public string? ReturnReason { get; set; }

    [MaxLength(1000)]
    public string? AdminNotes { get; set; }

    public bool IsRefunded { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(200)]
    public string? CreatedBy { get; set; }
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public class Coupon
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? DiscountPercent { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MinOrderAmount { get; set; }

    public int? MaxUsageCount { get; set; }
    public int CurrentUsageCount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ExpiryDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public int? AssignedToUserId { get; set; }
    public AppUser? AssignedToUser { get; set; }
}

public sealed class PaymentGateway
{
    public int Id { get; set; }

    [Required]
    [MaxLength(30)]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ConfigJson { get; set; }

    public bool Sandbox { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public bool IsConfigured { get; set; }

    [MaxLength(500)]
    public string? CallbackBaseUrl { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ShippingMethod
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [MaxLength(50)]
    public string? EstimatedDays { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
