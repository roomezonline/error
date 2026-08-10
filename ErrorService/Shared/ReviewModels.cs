using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class ProductReviewRequest
{
    [Required(ErrorMessage = "نام الزامی است")]
    [MinLength(2)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "ایمیل الزامی است")]
    [EmailAddress(ErrorMessage = "فرمت ایمیل صحیح نیست")]
    public string Email { get; set; } = string.Empty;

    [Range(1, 5, ErrorMessage = "امتیاز بین ۱ تا ۵")]
    public int Rating { get; set; } = 5;

    public int? ParentId { get; set; }

    [Required(ErrorMessage = "متن نظر الزامی است")]
    [MinLength(5)]
    public string Content { get; set; } = string.Empty;
}

public sealed class ProductReviewDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int? ParentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public string CreatedAtFa { get; set; } = string.Empty;
    public List<ProductReviewDto> Replies { get; set; } = new();
}

public sealed class ProductReviewSummaryDto
{
    public double AverageRating { get; set; }
    public int TotalCount { get; set; }
    public List<ProductReviewDto> Reviews { get; set; } = new();
}
