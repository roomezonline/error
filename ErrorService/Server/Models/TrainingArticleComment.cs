using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class TrainingArticleComment
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public TrainingArticle Article { get; set; } = default!;

    public int? ParentId { get; set; }
    public TrainingArticleComment? Parent { get; set; }
    public List<TrainingArticleComment> Replies { get; set; } = new();

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [Range(1, 5)]
    public int? Rating { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    public bool IsApproved { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
