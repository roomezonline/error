using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class StoryDto
{
    public int Id { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public bool IsVideo { get; set; }
    public string? Title { get; set; }
    public string? LinkUrl { get; set; }
    public int DisplayDuration { get; set; }
    public bool IsActive { get; set; }
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class StoryUpsertRequest
{
    [Required(ErrorMessage = "فایل رسانه الزامی است")]
    public string MediaUrl { get; set; } = string.Empty;

    public bool IsVideo { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(500)]
    public string? LinkUrl { get; set; }

    [Range(1, 60, ErrorMessage = "زمان نمایش باید بین 1 تا 60 ثانیه باشد")]
    public int DisplayDuration { get; set; } = 5;

    public bool IsActive { get; set; } = true;
}
