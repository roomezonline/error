using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
    public bool EnableBaleChat { get; set; } = true;
    public string? BaleBotToken { get; set; }
    public string? BaleBotGroupId { get; set; }
    public bool EnableTelegramChat { get; set; }
    public string? TelegramBotToken { get; set; }
    public string? TelegramGroupId { get; set; }
    public bool EnableEitaaChat { get; set; }
    public string? EitaaBotToken { get; set; }
    public string? EitaaGroupId { get; set; }
    public bool EnableBaleNotifications { get; set; } = true;
    public bool EnableTelegramNotifications { get; set; } = true;
    public bool EnableEitaaNotifications { get; set; } = true;
    public string? BaleSafirApiKey { get; set; }
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

    // ===== ابزارهای کنترلی دستیار (محدودسازی سوال/جواب) =====
    public int ChatAiMaxQuestionChars { get; set; } = 300;
    public int ChatAiMaxTokens { get; set; } = 450;
    public double ChatAiTemperature { get; set; } = 0.6;
    public int ChatAiSessionLimit { get; set; } = 12;
    public string? ChatAiBlockKeywords { get; set; }

    public bool RequireReviewApproval { get; set; } = true;

    // ===== تنظیمات فروشگاه (کلیدهای فعال/غیرفعال سازی امکانات) =====
    public bool EnableProductReviews { get; set; } = true;
    public bool EnableReviewMedia { get; set; }
    public bool EnableVerifiedBuyerBadge { get; set; } = true;
    public bool EnableReviewGallery { get; set; } = true;
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
    [Column(TypeName = "decimal(18,2)")]
    public decimal? FreeShippingThreshold { get; set; }
    public bool EnableReturnRequests { get; set; }
    public bool EnableReorder { get; set; }

    // ===== کارت محصول: نمایش سریع و مقایسه =====
    public bool EnableQuickView { get; set; } = true;
    public bool EnableCompare { get; set; } = true;
    public int CompareMaxItems { get; set; } = 4;

    // ===== ناوبری و صفحه اصلی: مگامنو، بردکرامب، شمارنده‌ها =====
    public bool EnableMegaMenu { get; set; } = true;
    public bool EnableBreadcrumb { get; set; } = true;
    public bool EnableHomeStats { get; set; } = true;
    public bool ShowOrderCountStat { get; set; } = true;
    public bool EnableHomeErrorSearch { get; set; } = true;
    public int HomeErrorSearchMaxResults { get; set; } = 6;

    // ===== تسویه‌حساب: آدرس ساختاریافته =====
    public bool EnableStructuredAddress { get; set; } = true;
    public bool RequireProvinceCity { get; set; }
    public bool RequirePostalCode { get; set; }

    // ===== لغو سفارش توسط مشتری =====
    public bool EnableCustomerOrderCancel { get; set; } = true;
    /// <summary>مهلت لغو از زمان ثبت سفارش (ساعت)؛ صفر = بدون محدودیت تا مهلت پرداخت.</summary>
    public int CustomerCancelWindowHours { get; set; } = 24;
    public bool CustomerCancelRequireReason { get; set; } = true;

    public bool EnableHomeBestSellers { get; set; } = true;
    public bool EnableHomeRestocked { get; set; } = true;
    public bool ShowEnamadSeal { get; set; } = true;

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
