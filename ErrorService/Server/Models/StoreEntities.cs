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

    public int StockQuantity { get; set; } = 0;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Technical details
    [MaxLength(2000)]
    public string? CompatibilityInfo { get; set; } // Brands/Models compatible with this part

    [MaxLength(500)]
    public string? DatasheetUrl { get; set; }

    [MaxLength(2000)]
    public string? FailureSymptoms { get; set; } // Symptoms when this part is faulty

    [MaxLength(500)]
    public string? RelatedProductIds { get; set; } // Comma separated IDs
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

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
