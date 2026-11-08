using System.Collections.Generic;

namespace ErrorService.Shared;

/// <summary>یک آیتم از بخش «ارورسرویس چه می‌کند؟» در صفحه درباره ما.</summary>
public sealed class AboutFeatureDto
{
    /// <summary>کلاس آیکون Open Iconic بدون پیشوند oi- (مثلاً cart).</summary>
    public string Icon { get; set; } = "check";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>آمار واقعی و محاسبه‌شده از دیتابیس برای صفحه درباره ما.</summary>
public sealed class AboutStatsDto
{
    public int ErrorCodesCount { get; set; }
    public int ServicesCount { get; set; }
    public int ProductsCount { get; set; }
    public int AcademyItemsCount { get; set; }
    public int MonitoringCount { get; set; }
    public int WorkshopsCount { get; set; }
    public int ExperienceYears { get; set; }
}

/// <summary>محتوای قابل ویرایش صفحه درباره ما؛ مقادیر خالی با متن پیش‌فرض جایگزین می‌شوند.</summary>
public sealed class AboutPageDto
{
    public string HeroBadge { get; set; } = string.Empty;
    public string HeroTitle { get; set; } = string.Empty;
    public string HeroSubtitle { get; set; } = string.Empty;
    public string? HeroImageUrl { get; set; }

    public string StoryTitle { get; set; } = string.Empty;
    public string StoryText { get; set; } = string.Empty;

    public string MissionTitle { get; set; } = string.Empty;
    public string MissionText { get; set; } = string.Empty;
    public string VisionTitle { get; set; } = string.Empty;
    public string VisionText { get; set; } = string.Empty;
    public string ValuesTitle { get; set; } = string.Empty;
    public string ValuesText { get; set; } = string.Empty;

    public string FeaturesTitle { get; set; } = string.Empty;
    public string FeaturesSubtitle { get; set; } = string.Empty;
    public List<AboutFeatureDto> Features { get; set; } = new();

    public bool ShowStats { get; set; } = true;
    public bool ShowStatErrorCodes { get; set; } = true;
    public bool ShowStatServices { get; set; } = true;
    public bool ShowStatProducts { get; set; } = true;
    public bool ShowStatAcademy { get; set; } = true;
    public bool ShowStatMonitoring { get; set; } = true;
    public bool ShowStatWorkshops { get; set; } = true;
    public bool ShowStatExperience { get; set; } = true;

    /// <summary>سال شروع فعالیت به تقویم شمسی؛ مبنای محاسبهٔ «سال تجربه».</summary>
    public int FoundingYear { get; set; } = 1394;

    public bool ShowTeam { get; set; } = true;
    public bool ShowTestimonials { get; set; } = true;

    public string CtaTitle { get; set; } = string.Empty;
    public string CtaText { get; set; } = string.Empty;
    public string CtaButtonText { get; set; } = string.Empty;
    public string CtaButtonUrl { get; set; } = string.Empty;

    public string SeoTitle { get; set; } = string.Empty;
    public string SeoDescription { get; set; } = string.Empty;
    public string SeoKeywords { get; set; } = string.Empty;

    /// <summary>فقط در پاسخ عمومی پر می‌شود و در ذخیره نادیده گرفته می‌شود.</summary>
    public AboutStatsDto Stats { get; set; } = new();
}
