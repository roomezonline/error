using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public interface IDiscountInfo
{
    decimal Price { get; }
    decimal? DiscountPrice { get; }
    DateTimeOffset? DiscountStartDate { get; }
    DateTimeOffset? DiscountExpiryDate { get; }
}

public static class DiscountHelper
{
    public static bool IsActive(IDiscountInfo p)
    {
        if (p.DiscountPrice == null) return false;
        var now = DateTimeOffset.Now;
        if (p.DiscountStartDate.HasValue && p.DiscountStartDate.Value > now) return false;
        if (p.DiscountExpiryDate.HasValue && p.DiscountExpiryDate.Value <= now) return false;
        return true;
    }

    public static bool IsTimedActive(IDiscountInfo p)
        => IsActive(p) && p.DiscountExpiryDate.HasValue;

    public static bool IsPermanentActive(IDiscountInfo p)
        => IsActive(p) && !p.DiscountExpiryDate.HasValue;

    public static bool IsExpired(IDiscountInfo p)
        => p.DiscountPrice != null && p.DiscountExpiryDate.HasValue && p.DiscountExpiryDate.Value <= DateTimeOffset.Now;

    public static bool IsScheduled(IDiscountInfo p)
        => p.DiscountPrice != null && p.DiscountStartDate.HasValue && p.DiscountStartDate.Value > DateTimeOffset.Now;

    public static decimal GetEffectivePrice(IDiscountInfo p)
        => IsActive(p) && p.DiscountPrice.HasValue ? p.DiscountPrice.Value : p.Price;

    public static TimeSpan? GetRemaining(IDiscountInfo p)
        => IsTimedActive(p) ? p.DiscountExpiryDate!.Value - DateTimeOffset.Now : null;

    public static int CalculatePercent(IDiscountInfo p)
    {
        if (!IsActive(p) || p.Price <= 0) return 0;
        var percent = (int)Math.Round((p.Price - p.DiscountPrice!.Value) / p.Price * 100);
        return percent < 0 ? 0 : percent;
    }
}

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public int ProductCount { get; set; }
    public string? Slug { get; set; }
    public int? ParentId { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ProductFacetsDto
{
    public List<CategoryFacetDto> Categories { get; set; } = new();
    public int Total { get; set; }
    public int AvailableCount { get; set; }
    public int DiscountedCount { get; set; }
}

public class CategoryFacetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public int Count { get; set; }
}

public static class StoreDefaults
{
    /// <summary>متن پیش‌فرض زمان تقریبی ارسال؛ زمانی که برای محصول مقداری ثبت نشده باشد.</summary>
    public const string DeliveryEta = "ارسال طی ۳ تا ۵ روز کاری — تهران و شهرستان";
}

public class ProductDto : IDiscountInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Sku { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public DateTimeOffset? DiscountStartDate { get; set; }
    public DateTimeOffset? DiscountExpiryDate { get; set; }
    public string? MainImageUrl { get; set; }
    public string? ImageUrl2 { get; set; }
    public string? ImageUrl3 { get; set; }
    public string? ImageUrl4 { get; set; }
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsDraft { get; set; }
    public int StockQuantity { get; set; } = 0;
    public bool IsFeatured { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Technical details
    public string? CompatibilityInfo { get; set; }
    public string? DatasheetUrl { get; set; }
    public string? FailureSymptoms { get; set; }
    public string? RelatedProductIds { get; set; }

    /// <summary>زمان تقریبی ارسال که در صفحهٔ محصول نمایش داده می‌شود.</summary>
    public string? EstimatedDeliveryText { get; set; }
    public int ViewCount { get; set; }
}

public class ProductAvailabilityCheckItem
{
    public int ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}

public sealed class ProductAdminSummaryDto
{
    public int Total { get; set; }
    public int Available { get; set; }
    public int Unavailable { get; set; }
    public int OutOfStock { get; set; }
    public int LowStock { get; set; }
    public int Discounted { get; set; }
    public int TotalStock { get; set; }
    public int LowStockThreshold { get; set; }
    public decimal InventoryValue { get; set; }
    public int CategoryCount { get; set; }
    public int EmptyCategoryCount { get; set; }
    public int QnaCount { get; set; }
    public int PendingQnaCount { get; set; }
    public int ReviewCount { get; set; }
    public int PendingReviewCount { get; set; }
}

public sealed class ProductPricePointDto
{
    public int Id { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public string DateFa { get; set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; set; }
}

public sealed class ProductPricePointRequest
{
    public decimal? Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public DateTimeOffset? RecordedAt { get; set; }
}

public sealed class ProductSpecDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string SpecKey { get; set; } = string.Empty;
    public string SpecValue { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class ProductSpecsUpsertRequest
{
    public List<ProductSpecDto> Specs { get; set; } = new();
}

public sealed class ProductQuestionDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public bool IsAnswered { get; set; }
    public int UpVotes { get; set; }
    public bool HasVoted { get; set; }
    public string CreatedAtFa { get; set; } = string.Empty;
    public string? AnsweredAtFa { get; set; }
    public List<ProductQuestionReplyDto> Replies { get; set; } = new();
}

public sealed class ProductQuestionAdminDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string? ProductImageUrl { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public bool IsApproved { get; set; }
    public bool IsAnswered { get; set; }
    public bool IsAdminRead { get; set; }
    public int UpVotes { get; set; }
    public string CreatedAtFa { get; set; } = string.Empty;
    public List<ProductQuestionReplyDto> Replies { get; set; } = new();
}

public sealed class ProductQuestionProductSummaryDto
{
    public int ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string? ProductImageUrl { get; set; }
    public string? ProductSlug { get; set; }
    public int TotalCount { get; set; }
    public int UnansweredCount { get; set; }
    public int PendingCount { get; set; }
    public int UnreadCount { get; set; }
    public string LastCreatedAtFa { get; set; } = string.Empty;
}

public sealed class ProductQuestionReplyDto
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsMine { get; set; }
    public bool IsApproved { get; set; }
    public string CreatedAtFa { get; set; } = string.Empty;
}

public sealed class ProductQuestionReplyRequest
{
    [Required(ErrorMessage = "متن پاسخ الزامی است")]
    [MinLength(3)]
    public string Body { get; set; } = string.Empty;
}

public sealed class ProductQuestionRequest
{
    [Required(ErrorMessage = "متن پرسش الزامی است")]
    [MinLength(5)]
    public string Question { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? FullName { get; set; }
}

public sealed class ProductQuestionAnswerRequest
{
    [MaxLength(1000)]
    public string? Answer { get; set; }

    public bool? IsApproved { get; set; }
}

public sealed class ProductBadgeDto
{
    public bool IsNew { get; set; }
    public bool IsBestSeller { get; set; }
    public bool OnSale { get; set; }
    public bool IsRestocked { get; set; }
}

public class ProductAvailabilityDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public int StockQuantity { get; set; }
    public bool HasSufficientStock { get; set; }
}

public class ProductUpsertRequest
{
    [Required(ErrorMessage = "نام محصول الزامی است")]
    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string? Description { get; set; }

    [Range(0, 1000000000, ErrorMessage = "قیمت نمی‌تواند منفی باشد")]
    public decimal Price { get; set; }

    public decimal? DiscountPrice { get; set; }
    public DateTimeOffset? DiscountStartDate { get; set; }
    public DateTimeOffset? DiscountExpiryDate { get; set; }
    public string? MainImageUrl { get; set; }
    public string? ImageUrl2 { get; set; }
    public string? ImageUrl3 { get; set; }
    public string? ImageUrl4 { get; set; }
    
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب دسته‌بندی الزامی است")]
    public int CategoryId { get; set; }
    
    public bool IsAvailable { get; set; } = true;

    /// <summary>وقتی روشن باشد، محصول پیش‌نویس می‌شود و در فروشگاه نمایش داده نمی‌شود.</summary>
    public bool IsDraft { get; set; }

    public int StockQuantity { get; set; } = 0;

    public bool IsFeatured { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    // Technical details
    public string? CompatibilityInfo { get; set; }
    public string? DatasheetUrl { get; set; }
    public string? FailureSymptoms { get; set; }
    public string? RelatedProductIds { get; set; }

    /// <summary>زمان تقریبی ارسال اختصاصی این محصول؛ مثال: «ارسال طی ۱ تا ۳ روز کاری — تهران و شهرستان».</summary>
    [System.ComponentModel.DataAnnotations.StringLength(300)]
    public string? EstimatedDeliveryText { get; set; }
}

public class NewsDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Content { get; set; }
    public string? Summary { get; set; }
    public string? ImageUrl { get; set; }
    public string? Tags { get; set; }
    public string? AuthorName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsPublished { get; set; } = true;
    public double AverageRating { get; set; }
    public int CommentCount { get; set; }

    public NewsDto? PreviousNews { get; set; }
    public NewsDto? NextNews { get; set; }
    public List<CommentDto> Comments { get; set; } = new();
}

public class NewsUpsertRequest
{
    [Required(ErrorMessage = "عنوان خبر الزامی است")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "متن خبر الزامی است")]
    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Summary { get; set; }

    public string? ImageUrl { get; set; }
    public string? Tags { get; set; }
    public string? AuthorName { get; set; }
    public bool IsPublished { get; set; } = true;

    public DateTimeOffset? CreatedAt { get; set; }
}

public class StockAlertMineDto
{
    public List<int> ActiveProductIds { get; set; } = new();
}

public class StockAlertConnectRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "شناسه محصول نامعتبر است")]
    public int ProductId { get; set; }
}

public class MessengerConnectChannelDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? BotUrl { get; set; }
    public bool SelfService { get; set; }
    public string Instruction { get; set; } = string.Empty;
}
