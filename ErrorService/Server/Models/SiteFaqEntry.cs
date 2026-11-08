using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

/// <summary>پرسش متداول عمومی سایت؛ قابل مدیریت از پنل ادمین و قابل جستجو.</summary>
public sealed class SiteFaqEntry
{
    public int Id { get; set; }

    [Required]
    [MaxLength(300)]
    public string Question { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Answer { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string CategoryKey { get; set; } = "shop";

    [MaxLength(500)]
    public string? Keywords { get; set; }

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }
}
