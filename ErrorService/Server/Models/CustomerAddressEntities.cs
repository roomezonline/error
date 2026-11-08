using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErrorService.Server.Models;

/// <summary>آدرس ذخیره‌شدهٔ مشتری برای پرکردن سریع تسویه‌حساب.</summary>
public sealed class CustomerAddress
{
    public int Id { get; set; }

    public int AppUserId { get; set; }
    public AppUser? User { get; set; }

    /// <summary>برچسب دلخواه مشتری (خانه، محل کار، ...).</summary>
    [MaxLength(100)]
    public string? Title { get; set; }

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    public int? ProvinceId { get; set; }

    public int? CityId { get; set; }

    [MaxLength(100)]
    public string? ProvinceName { get; set; }

    [MaxLength(100)]
    public string? CityName { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Address { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum PriceDropAlertStatus
{
    Active = 0,
    Notified = 1
}

/// <summary>یادآوری «خبرم کن وقتی قیمت کم شد» برای یک محصول.</summary>
public sealed class PriceDropAlert
{
    public int Id { get; set; }

    public int AppUserId { get; set; }
    public AppUser? User { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>قیمت مؤثر در لحظهٔ ثبت یادآوری؛ کاهش نسبت به این عدد اطلاع داده می‌شود.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal SubscribedPrice { get; set; }

    public PriceDropAlertStatus Status { get; set; } = PriceDropAlertStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NotifiedAt { get; set; }
}
