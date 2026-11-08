using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErrorService.Shared;

namespace ErrorService.Server.Models;

public class Category
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    [MaxLength(200)]
    public string? Slug { get; set; }

    public int? ParentId { get; set; }

    [MaxLength(200)]
    public string? SeoTitle { get; set; }

    [MaxLength(500)]
    public string? SeoDescription { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product : IDiscountInfo
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Slug { get; set; }

    [MaxLength(100)]
    public string? Sku { get; set; }

    [MaxLength(10000)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountPrice { get; set; }

    public DateTimeOffset? DiscountStartDate { get; set; }

    public DateTimeOffset? DiscountExpiryDate { get; set; }

    [MaxLength(500)]
    public string? MainImageUrl { get; set; }

    [MaxLength(500)]
    public string? ImageUrl2 { get; set; }

    [MaxLength(500)]
    public string? ImageUrl3 { get; set; }

    [MaxLength(500)]
    public string? ImageUrl4 { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public bool IsAvailable { get; set; } = true;

    /// <summary>پیش‌نویس: محصول فقط در پنل مدیریت دیده می‌شود و در فروشگاه نمایش داده نمی‌شود.</summary>
    public bool IsDraft { get; set; }

    public int StockQuantity { get; set; } = 0;

    public bool IsFeatured { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Technical details
    [MaxLength(2000)]
    public string? CompatibilityInfo { get; set; } // Brands/Models compatible with this part

    [MaxLength(500)]
    public string? DatasheetUrl { get; set; }

    [MaxLength(2000)]
    public string? RelatedProductIds { get; set; } // Comma separated IDs

    [MaxLength(2000)]
    public string? FailureSymptoms { get; set; } // Symptoms when this part is faulty

    [MaxLength(300)]
    public string? EstimatedDeliveryText { get; set; } // زمان تقریبی ارسال (متن اختصاصی هر محصول)

    public int ViewCount { get; set; } = 0;
}

public class News
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Slug { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Summary { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; }

    [MaxLength(100)]
    public string? AuthorName { get; set; }

    public bool IsPublished { get; set; } = true;

    public ContentType ContentType { get; set; } = ContentType.News;

    public int? CategoryId { get; set; }
    public ContentCategory? Category { get; set; }

    /// <summary>انتشار از این زمان به بعد؛ خالی یعنی بلافاصله.</summary>
    public DateTimeOffset? PublishAt { get; set; }

    /// <summary>آرشیو خودکار از این زمان؛ خالی یعنی بدون انقضا.</summary>
    public DateTimeOffset? ExpireAt { get; set; }

    public bool IsFeatured { get; set; }

    /// <summary>سنجاق در بالای فهرست تا این زمان.</summary>
    public DateTimeOffset? PinnedUntil { get; set; }

    public int ViewCount { get; set; }

    [MaxLength(200)]
    public string? SeoTitle { get; set; }

    [MaxLength(500)]
    public string? SeoDescription { get; set; }

    [MaxLength(500)]
    public string? CanonicalUrl { get; set; }

    [MaxLength(200)]
    public string? SourceName { get; set; }

    [MaxLength(500)]
    public string? SourceUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ContentCategory
{
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? Slug { get; set; }

    /// <summary>خالی یعنی مشترک بین همه نوع‌های محتوا.</summary>
    public ContentType? ContentType { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<News> Contents { get; set; } = new List<News>();
}

/// <summary>تنظیمات تک‌ردیفی مرکز محتوا؛ فقط از پنل ادمین تغییر می‌کند.</summary>
public sealed class ContentSettings
{
    public int Id { get; set; }

    public bool TipsEnabled { get; set; } = true;
    public bool AnnouncementsEnabled { get; set; } = true;

    public bool NewsCommentsEnabled { get; set; } = true;
    public bool TipsCommentsEnabled { get; set; } = true;
    public bool AnnouncementsCommentsEnabled { get; set; }

    public bool ShowViewCount { get; set; } = true;
    public bool ShowTableOfContents { get; set; } = true;
    public bool ShowAuthorBox { get; set; } = true;
    public bool ShowRelatedContent { get; set; } = true;
    public bool ShowReadingTime { get; set; } = true;
    public bool ShowReadingProgress { get; set; } = true;
    public bool FeaturedEnabled { get; set; } = true;
    public bool PinnedEnabled { get; set; } = true;
    public bool ScheduledPublishingEnabled { get; set; } = true;

    public int PageSize { get; set; } = 12;
    public int RelatedCount { get; set; } = 3;
    public int HomeCardCount { get; set; } = 3;
    public int HomeListCount { get; set; } = 5;
    public string HomeTitle { get; set; } = "آخرین اخبار";
    public string HomeTypes { get; set; } = "News,Tip,Announcement";
    public string DefaultAuthorName { get; set; } = "تحریریه ارورسرویس";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum StockAlertStatus
{
    Active = 1,
    Notified = 2
}

public class StockAlert
{
    public int Id { get; set; }

    public int AppUserId { get; set; }
    public AppUser? User { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public StockAlertStatus Status { get; set; } = StockAlertStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NotifiedAt { get; set; }
}

/// <summary>جدول مشخصات فنی ساختاریافته (کلید/مقدار) برای صفحه محصول.</summary>
public class ProductSpec
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Required]
    [MaxLength(120)]
    public string SpecKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string SpecValue { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>پرسش و پاسخ محصول — پرسش مشتری + پاسخ پشتیبانی (تک‌سطحی).</summary>
public class ProductQuestion
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int? AppUserId { get; set; }
    public AppUser? User { get; set; }

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Question { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Answer { get; set; }

    public bool IsApproved { get; set; }
    public bool IsAnswered { get; set; }
    public bool IsAdminRead { get; set; }

    public int UpVotes { get; set; }

    /// <summary>شناسه کاربرانی که رأی داده‌اند — جدا شده با کاما.</summary>
    [MaxLength(4000)]
    public string? VotedUserIds { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AnsweredAt { get; set; }
}

/// <summary>پاسخ کاربران به یک پرسش — جدا از پاسخ رسمی فروشگاه.</summary>
public class ProductQuestionReply
{
    public int Id { get; set; }

    public int QuestionId { get; set; }
    public ProductQuestion? Question { get; set; }

    public int? AppUserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Body { get; set; } = string.Empty;

    public bool IsApproved { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>تاریخچه کامل قیمت محصول برای نمودار.</summary>
public class ProductPriceHistory
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountPrice { get; set; }

    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}
