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
    public int StockQuantity { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; }

    // Technical details
    public string? CompatibilityInfo { get; set; }
    public string? DatasheetUrl { get; set; }
    public string? FailureSymptoms { get; set; }
    public string? RelatedProductIds { get; set; }
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

    public int StockQuantity { get; set; } = 0;

    public DateTimeOffset? CreatedAt { get; set; }

    // Technical details
    public string? CompatibilityInfo { get; set; }
    public string? DatasheetUrl { get; set; }
    public string? FailureSymptoms { get; set; }
    public string? RelatedProductIds { get; set; }
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
