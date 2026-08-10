using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class CommentRequest
{
    [Required(ErrorMessage = "نام الزامی است")]
    [MinLength(2)]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "فرمت ایمیل صحیح نیست")]
    public string? Email { get; set; }

    public int? Rating { get; set; }

    public int? ParentId { get; set; }

    [Required(ErrorMessage = "متن نظر الزامی است")]
    [MinLength(2)]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}

public sealed class CommentDto
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public int? ParentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int? Rating { get; set; }
    public string CreatedAtFa { get; set; } = string.Empty;
    public List<CommentDto> Replies { get; set; } = new();
}

public sealed class CommentSummaryDto
{
    public double AverageRating { get; set; }
    public int TotalCount { get; set; }
    public List<CommentDto> Comments { get; set; } = new();
}
