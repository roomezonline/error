using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class TrainingArticle
{
    public int Id { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Slug { get; set; }

    [MaxLength(1000)]
    public string? Summary { get; set; }

    [MaxLength(500)]
    public string? CoverImageUrl { get; set; }

    public bool IsPublished { get; set; } = false;
    public bool IsPremium { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<TrainingArticleBlock> Blocks { get; set; } = new List<TrainingArticleBlock>();
}

public sealed class TrainingArticleBlock
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public TrainingArticle Article { get; set; } = default!;

    public TrainingBlockType BlockType { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    public string? Content { get; set; }

    [MaxLength(500)]
    public string? Url { get; set; }

    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }

    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
