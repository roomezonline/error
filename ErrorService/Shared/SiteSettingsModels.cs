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
    BestSellers = 14,
    Restocked = 15
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
    public bool EnableBaleChat { get; set; } = true;
    public int MaxImageUploadSizeKb { get; set; } = 200;
    public string? ChartBaseUrl { get; set; }
    public string? BaleBotToken { get; set; }
    public string? BaleBotGroupId { get; set; }
    public bool EnableTelegramChat { get; set; }
    public string? TelegramBotToken { get; set; }
    public string? TelegramGroupId { get; set; }
    public bool EnableEitaaChat { get; set; }
    public string? EitaaBotToken { get; set; }
    public string? EitaaGroupId { get; set; }
    public string? ChatWelcomeMessage { get; set; }
    public bool ChatEnableAutoMessage { get; set; }
    public int ChatAutoMessageSeconds { get; set; } = 60;
    public bool ChatPhoneRequired { get; set; }
    public string? ChatOperatorImageUrl { get; set; }
    public bool ChatGreetingEnabled { get; set; } = true;
    public string? ChatGreetingText { get; set; }
    public string? ChatSuggestedTopics { get; set; }
    public bool ChatEnableAiAssistant { get; set; }
    public string? ChatAiProvider { get; set; }
    public string? ChatAiApiUrl { get; set; }
    public string? ChatAiModel { get; set; }
    public string? ChatAiApiKey { get; set; }
    public string? ChatAiSystemPrompt { get; set; }
    public bool ChatAiGuestEnabled { get; set; }
    public int ChatAiDailyLimit { get; set; } = 3;
    public bool RequireReviewApproval { get; set; } = true;

    // ===== تنظیمات فروشگاه =====
    public bool EnableProductReviews { get; set; } = true;
    public bool EnableReviewMedia { get; set; }
    public bool EnableProductQna { get; set; }
    public bool EnableSpecsTable { get; set; } = true;
    public bool EnablePriceHistoryChart { get; set; }
    public bool EnableProductBadges { get; set; } = true;
    public bool EnableTrustBar { get; set; } = true;
    public bool EnableDeliveryEta { get; set; } = true;
    public bool EnableWishlist { get; set; } = true;
    public bool EnableStockAlert { get; set; } = true;
    public bool EnableRelatedProducts { get; set; } = true;
    public bool EnableRecentlyViewed { get; set; } = true;
    public bool EnableFreeShipping { get; set; }
    public decimal? FreeShippingThreshold { get; set; }
    public bool EnableReturnRequests { get; set; }
    public bool EnableReorder { get; set; }
    public bool EnableHomeBestSellers { get; set; } = true;
    public bool EnableHomeRestocked { get; set; } = true;
    public bool ShowEnamadSeal { get; set; } = true;

    public List<SiteHomeModuleDto> HomeModules { get; set; } = new();
}
