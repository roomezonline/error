using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

/// <summary>
/// محتوای صفحه درباره ما؛ فقط یک ردیف نگهداری می‌شود و متن‌های خالی
/// در لایه API با پیش‌فرض‌های فارسی جایگزین می‌شوند.
/// </summary>
public sealed class AboutPageContent
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string? HeroBadge { get; set; }

    [MaxLength(200)]
    public string? HeroTitle { get; set; }

    [MaxLength(600)]
    public string? HeroSubtitle { get; set; }

    [MaxLength(500)]
    public string? HeroImageUrl { get; set; }

    [MaxLength(120)]
    public string? StoryTitle { get; set; }

    [MaxLength(6000)]
    public string? StoryText { get; set; }

    [MaxLength(120)]
    public string? MissionTitle { get; set; }

    [MaxLength(600)]
    public string? MissionText { get; set; }

    [MaxLength(120)]
    public string? VisionTitle { get; set; }

    [MaxLength(600)]
    public string? VisionText { get; set; }

    [MaxLength(120)]
    public string? ValuesTitle { get; set; }

    [MaxLength(600)]
    public string? ValuesText { get; set; }

    [MaxLength(160)]
    public string? FeaturesTitle { get; set; }

    [MaxLength(400)]
    public string? FeaturesSubtitle { get; set; }

    /// <summary>آرایه JSON از AboutFeatureDto.</summary>
    public string? FeaturesJson { get; set; }

    public bool ShowStats { get; set; } = true;
    public bool ShowStatErrorCodes { get; set; } = true;
    public bool ShowStatServices { get; set; } = true;
    public bool ShowStatProducts { get; set; } = true;
    public bool ShowStatAcademy { get; set; } = true;
    public bool ShowStatMonitoring { get; set; } = true;
    public bool ShowStatWorkshops { get; set; } = true;
    public bool ShowStatExperience { get; set; } = true;

    public int FoundingYear { get; set; } = 1394;

    public bool ShowTeam { get; set; } = true;
    public bool ShowTestimonials { get; set; } = true;

    [MaxLength(200)]
    public string? CtaTitle { get; set; }

    [MaxLength(600)]
    public string? CtaText { get; set; }

    [MaxLength(80)]
    public string? CtaButtonText { get; set; }

    [MaxLength(300)]
    public string? CtaButtonUrl { get; set; }

    [MaxLength(200)]
    public string? SeoTitle { get; set; }

    [MaxLength(500)]
    public string? SeoDescription { get; set; }

    [MaxLength(500)]
    public string? SeoKeywords { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
