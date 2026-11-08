using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

/// <summary>یک اکشن صوتی (مثل دیفراست یا استارت موتور) با فایل صدای ضبط‌شده</summary>
public sealed class ServiceVoiceActionDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string TitleFa { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int RepeatCount { get; set; }
    public int Priority { get; set; }
    public string? AudioUrl { get; set; }
    public long AudioSizeBytes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ServiceVoiceActionUpsertRequest
{
    public int Id { get; set; }

    [Required(ErrorMessage = "کلید اکشن الزامی است")]
    [RegularExpression(@"^[a-z0-9_-]{2,40}$", ErrorMessage = "کلید باید ۲ تا ۴۰ کاراکتر و شامل حروف انگلیسی کوچک، عدد، خط تیره یا زیرخط باشد")]
    public string Key { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام اکشن الزامی است")]
    [MaxLength(120, ErrorMessage = "نام اکشن حداکثر ۱۲۰ کاراکتر است")]
    public string TitleFa { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [Range(1, 50, ErrorMessage = "تعداد تکرار باید بین ۱ تا ۵۰ باشد")]
    public int RepeatCount { get; set; } = 1;

    [Range(0, 1000, ErrorMessage = "اولویت باید بین ۰ تا ۱۰۰۰ باشد")]
    public int Priority { get; set; } = 10;
}
