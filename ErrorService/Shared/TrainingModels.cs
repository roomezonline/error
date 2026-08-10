using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public enum TrainingBlockTypeDto
{
    Text = 1,
    Image = 2,
    Video = 3,
    Audio = 4,
    File = 5,
    Embed = 6
}

public class TrainingCourseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Summary { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool IsPublished { get; set; }
    public bool IsPremium { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int LessonsCount { get; set; }
}

public class TrainingCourseUpsertRequest
{
    [Required(ErrorMessage = "عنوان دوره الزامی است")]
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool IsPublished { get; set; }
    public bool IsPremium { get; set; }
    public int SortOrder { get; set; }
}

public class TrainingLessonDto
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public bool IsPublished { get; set; }
    public bool IsPremium { get; set; }
    public int SortOrder { get; set; }
    public string? CourseTitle { get; set; }
    public string? CourseSlug { get; set; }
    public string? CourseCoverImageUrl { get; set; }
    public List<TrainingBlockDto> Blocks { get; set; } = new();
    public List<TrainingAttachmentDto> Attachments { get; set; } = new();
}

public class TrainingLessonUpsertRequest
{
    public int CourseId { get; set; }
    [Required(ErrorMessage = "عنوان قسمت الزامی است")]
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public bool IsPublished { get; set; }
    public bool IsPremium { get; set; }
    public int SortOrder { get; set; }
}

public class TrainingBlockDto
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public TrainingBlockTypeDto BlockType { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Url { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? FileName { get; set; }
    public long? SizeBytes { get; set; }
    public int SortOrder { get; set; }
}

public class TrainingBlockUpsertRequest
{
    public int LessonId { get; set; }
    public TrainingBlockTypeDto BlockType { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Url { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? FileName { get; set; }
    public int SortOrder { get; set; }
}

public class TrainingAttachmentDto
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public long? SizeBytes { get; set; }
    public int SortOrder { get; set; }
}

public class TrainingAttachmentUpsertRequest
{
    public int LessonId { get; set; }
    [Required(ErrorMessage = "عنوان فایل الزامی است")]
    public string Title { get; set; } = string.Empty;
    [Required(ErrorMessage = "آدرس فایل الزامی است")]
    public string FileUrl { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public int SortOrder { get; set; }
}
