using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class TrainingArticleDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Summary { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool IsPublished { get; set; }
    public bool IsPremium { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<TrainingArticleBlockDto> Blocks { get; set; } = new();
    public List<TrainingArticleCommentDto> Comments { get; set; } = new();
    public double AverageRating { get; set; }
}

public sealed class TrainingArticleCommentDto
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? Rating { get; set; }
    public string CreatedAtFa { get; set; } = string.Empty;
    public List<TrainingArticleCommentDto> Replies { get; set; } = new();
}

public sealed class TrainingArticleCommentRequest
{
    [Required(ErrorMessage = "نام شما الزامی است")]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [Range(1, 5)]
    public int? Rating { get; set; }

    public int? ParentId { get; set; }

    [Required(ErrorMessage = "متن نظر الزامی است")]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}

public sealed class TrainingArticleBlockDto
{
    public int Id { get; set; }
    public TrainingBlockTypeDto BlockType { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Url { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int SortOrder { get; set; }
}

public sealed class TrainingArticleUpsertRequest
{
    [Required(ErrorMessage = "عنوان الزامی است")]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Summary { get; set; }

    [MaxLength(500)]
    public string? CoverImageUrl { get; set; }

    public bool IsPublished { get; set; }
    public bool IsPremium { get; set; }
}
