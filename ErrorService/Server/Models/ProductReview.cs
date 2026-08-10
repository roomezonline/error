using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class ProductReview
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int? ParentId { get; set; }
    public ProductReview? Parent { get; set; }
    public List<ProductReview> Replies { get; set; } = new();

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    [Required]
    public string Content { get; set; } = string.Empty;

    public bool IsApproved { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
