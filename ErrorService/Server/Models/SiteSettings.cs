using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class SiteSettings
{
    [Key]
    public int Id { get; set; }
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
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? SeoKeywords { get; set; }
    public string? GoogleAnalyticsTag { get; set; }
    public string? DefaultOgImage { get; set; }
    public bool IsMaintenanceMode { get; set; } = false;
    public bool EnableSsl { get; set; } = true;
    public int HstsMaxAgeDays { get; set; } = 7;
    public bool AllowGuestCheckout { get; set; } = true;
    public bool EnableOnlineChat { get; set; } = true;
    public bool AllowGuestChat { get; set; } = true;
    public int MaxImageUploadSizeKb { get; set; } = 200;
    public string? ChartBaseUrl { get; set; }
    public string? BaleBotToken { get; set; }
    public string? BaleBotGroupId { get; set; }

    public List<SiteHomeModuleSetting> HomeModules { get; set; } = new();
}

public sealed class SiteHomeModuleSetting
{
    [Key]
    public int Id { get; set; }

    public int SiteSettingsId { get; set; }

    public int Key { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }
}
