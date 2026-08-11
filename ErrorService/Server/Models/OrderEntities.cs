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

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
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
