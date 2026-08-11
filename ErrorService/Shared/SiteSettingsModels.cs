using System.Collections.Generic;

namespace ErrorService.Shared;

public enum SiteHomeModuleKey
{
    SpecialOffers = 1,
    LatestNews = 2,
    LatestProducts = 3,
    LatestAcademy = 4,
    TeamMembers = 5,
    HomeCategories = 6,
    FeatureCards = 7,
    Stories = 8,
    LatestCourses = 9,
    LatestArticles = 10,
    PopularBrands = 11,
    SomeCustomers = 12,
    TechnicalGuide = 13,
    FeaturedProducts = 14
}

public sealed class SiteHomeModuleDto
{
    public SiteHomeModuleKey Key { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class SiteSettingsDto
{
    public bool ShowSpecialOffers { get; set; } = true;
    public bool ShowLatestNews { get; set; } = true;
    public bool ShowLatestProducts { get; set; } = true;
    public bool ShowLatestAcademy { get; set; } = true;
    public bool ShowLatestCourses { get; set; } = true;
    public bool ShowLatestArticles { get; set; } = true;
    public bool ShowTeamMembers { get; set; } = true;
    public bool ShowHomeCategories { get; set; } = true;
    public bool ShowFeatureCards { get; set; } = true;
    public bool ShowStories { get; set; } = true;
    public bool ShowFeaturedProducts { get; set; } = true;
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? SeoKeywords { get; set; }
    public string? GoogleAnalyticsTag { get; set; }
    public string? DefaultOgImage { get; set; }
    public bool IsMaintenanceMode { get; set; }
    public bool EnableSsl { get; set; } = true;
    public int HstsMaxAgeDays { get; set; } = 7;
    public bool AllowGuestCheckout { get; set; } = true;
    public bool EnableOnlineChat { get; set; } = true;
    public bool AllowGuestChat { get; set; } = true;
    public int MaxImageUploadSizeKb { get; set; } = 200;
    public string? ChartBaseUrl { get; set; }
    public string? BaleBotToken { get; set; }
    public string? BaleBotGroupId { get; set; }

    public List<SiteHomeModuleDto> HomeModules { get; set; } = new();
}
