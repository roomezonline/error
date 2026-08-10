using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public enum TrainingBlockType
{
    Text = 1,
    Image = 2,
    Video = 3,
    Audio = 4,
    File = 5,
    Embed = 6
}

public sealed class TrainingCourse
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Slug { get; set; }

    [MaxLength(1000)]
    public string? Summary { get; set; }

    [MaxLength(500)]
    public string? CoverImageUrl { get; set; }

    public bool IsPublished { get; set; } = false;
    public bool IsPremium { get; set; } = false;

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<TrainingLesson> Lessons { get; set; } = new List<TrainingLesson>();
}

public sealed class TrainingLesson
{
    public int Id { get; set; }

    public int CourseId { get; set; }
    public TrainingCourse Course { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Summary { get; set; }

    public bool IsPublished { get; set; } = false;
    public bool IsPremium { get; set; } = false;

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<TrainingBlock> Blocks { get; set; } = new List<TrainingBlock>();
    public ICollection<TrainingAttachment> Attachments { get; set; } = new List<TrainingAttachment>();
}

public sealed class TrainingBlock
{
    public int Id { get; set; }

    public int LessonId { get; set; }
    public TrainingLesson Lesson { get; set; } = default!;

    public TrainingBlockType BlockType { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    public string? Content { get; set; }

    [MaxLength(500)]
    public string? Url { get; set; }

    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }

    [MaxLength(255)]
    public string? FileName { get; set; }

    [MaxLength(120)]
    public string? ContentType { get; set; }

    public long? SizeBytes { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TrainingAttachment
{
    public int Id { get; set; }

    public int LessonId { get; set; }
    public TrainingLesson Lesson { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FileUrl { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? FileName { get; set; }

    [MaxLength(120)]
    public string? ContentType { get; set; }

    public long? SizeBytes { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
