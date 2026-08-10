using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
}

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
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
    
    public string? Description { get; set; }
    
    [Range(0, 1000000000, ErrorMessage = "قیمت نمی‌تواند منفی باشد")]
    public decimal Price { get; set; }
    
    public decimal? DiscountPrice { get; set; }
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
