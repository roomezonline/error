using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SiteSettingsController : ControllerBase
{
    private const string SettingsCacheTag = "site-settings";

    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IOutputCacheStore _outputCache;

    public SiteSettingsController(ErrorServiceDbContext db, IWebHostEnvironment env, IOutputCacheStore outputCache)
    {
        _db = db;
        _env = env;
        _outputCache = outputCache;
    }

    [HttpGet]
    [OutputCache(Duration = 60, Tags = new[] { SettingsCacheTag })]
    public async Task<ActionResult<SiteSettingsDto>> Get()
    {
        Response.Headers.CacheControl = "private, max-age=60";
        return await BuildDtoAsync(includeSecrets: false);
    }

    // تنظیمات کامل فقط برای پنل ادمین؛ توکن‌ها و کلید AI هرگز در پاسخ عمومی قرار نمی‌گیرند.
    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpGet("admin")]
    public async Task<ActionResult<SiteSettingsDto>> GetAdmin()
        => await BuildDtoAsync(includeSecrets: true);

    private async Task<SiteSettingsDto> BuildDtoAsync(bool includeSecrets)
    {
        SiteSettings? settings;
        var homeModulesAvailable = true;
        try
        {
            settings = await _db.SiteSettings
                .Include(x => x.HomeModules)
                .FirstOrDefaultAsync();
        }
        catch
        {
            homeModulesAvailable = false;
            settings = await _db.SiteSettings.FirstOrDefaultAsync();
        }
        if (settings == null)
        {
            settings = new SiteSettings();
            _db.SiteSettings.Add(settings);
            await _db.SaveChangesAsync();
        }

        if (homeModulesAvailable)
        {
            var removedUnknown = EnsureHomeModules(settings);
            SyncLegacyFlagsFromModules(settings);
            if (removedUnknown)
                await _db.SaveChangesAsync();
        }

        return new SiteSettingsDto
        {
            ShowSpecialOffers = settings.ShowSpecialOffers,
            ShowLatestNews = settings.ShowLatestNews,
            ShowLatestProducts = settings.ShowLatestProducts,
            ShowLatestAcademy = settings.ShowLatestAcademy,
            ShowLatestCourses = settings.ShowLatestCourses,
            ShowLatestArticles = settings.ShowLatestArticles,
            ShowTeamMembers = settings.ShowTeamMembers,
            ShowHomeCategories = settings.ShowHomeCategories,
            ShowFeatureCards = settings.ShowFeatureCards,
            ShowStories = settings.ShowStories,
            AllowGuestCheckout = settings.AllowGuestCheckout,
            EnableOnlineChat = settings.EnableOnlineChat,
            AllowGuestChat = settings.AllowGuestChat,
            EnableBaleChat = settings.EnableBaleChat,
            SeoTitle = settings.SeoTitle,
            SeoDescription = settings.SeoDescription,
            SeoKeywords = settings.SeoKeywords,
            GoogleAnalyticsTag = settings.GoogleAnalyticsTag,
            DefaultOgImage = settings.DefaultOgImage,
            IsMaintenanceMode = settings.IsMaintenanceMode,
            EnableSsl = settings.EnableSsl,
            HstsMaxAgeDays = settings.HstsMaxAgeDays,
            MaxImageUploadSizeKb = settings.MaxImageUploadSizeKb,
            ChartBaseUrl = settings.ChartBaseUrl,
            BaleBotToken = includeSecrets ? settings.BaleBotToken : null,
            BaleBotGroupId = settings.BaleBotGroupId,
            EnableTelegramChat = settings.EnableTelegramChat,
            TelegramBotToken = includeSecrets ? settings.TelegramBotToken : null,
            TelegramGroupId = settings.TelegramGroupId,
            EnableEitaaChat = settings.EnableEitaaChat,
            EitaaBotToken = includeSecrets ? settings.EitaaBotToken : null,
            EitaaGroupId = settings.EitaaGroupId,
            ChatWelcomeMessage = settings.ChatWelcomeMessage,
            ChatEnableAutoMessage = settings.ChatEnableAutoMessage,
            ChatAutoMessageSeconds = settings.ChatAutoMessageSeconds,
            ChatPhoneRequired = settings.ChatPhoneRequired,
            ChatOperatorImageUrl = settings.ChatOperatorImageUrl,
            ChatGreetingEnabled = settings.ChatGreetingEnabled,
            ChatGreetingText = settings.ChatGreetingText,
            ChatSuggestedTopics = settings.ChatSuggestedTopics,
            ChatEnableAiAssistant = settings.ChatEnableAiAssistant,
            ChatAiProvider = settings.ChatAiProvider,
            ChatAiApiUrl = settings.ChatAiApiUrl,
            ChatAiModel = settings.ChatAiModel,
            ChatAiApiKey = includeSecrets ? settings.ChatAiApiKey : null,
            ChatAiSystemPrompt = settings.ChatAiSystemPrompt,
            RequireReviewApproval = settings.RequireReviewApproval,
            EnableProductReviews = settings.EnableProductReviews,
            EnableReviewMedia = settings.EnableReviewMedia,
            EnableVerifiedBuyerBadge = settings.EnableVerifiedBuyerBadge,
            EnableReviewGallery = settings.EnableReviewGallery,
            EnableProductQna = settings.EnableProductQna,
            EnableSpecsTable = settings.EnableSpecsTable,
            EnablePriceHistoryChart = settings.EnablePriceHistoryChart,
            EnableProductBadges = settings.EnableProductBadges,
            EnableTrustBar = settings.EnableTrustBar,
            EnableDeliveryEta = settings.EnableDeliveryEta,
            EnableWishlist = settings.EnableWishlist,
            EnableStockAlert = settings.EnableStockAlert,
            EnableRelatedProducts = settings.EnableRelatedProducts,
            EnableRecentlyViewed = settings.EnableRecentlyViewed,
            EnableFreeShipping = settings.EnableFreeShipping,
            FreeShippingThreshold = settings.FreeShippingThreshold,
            EnableReturnRequests = settings.EnableReturnRequests,
            EnableReorder = settings.EnableReorder,
            EnableQuickView = settings.EnableQuickView,
            EnableCompare = settings.EnableCompare,
            CompareMaxItems = settings.CompareMaxItems,
            EnableMegaMenu = settings.EnableMegaMenu,
            EnableBreadcrumb = settings.EnableBreadcrumb,
            EnableHomeStats = settings.EnableHomeStats,
            ShowOrderCountStat = settings.ShowOrderCountStat,
            EnableHomeErrorSearch = settings.EnableHomeErrorSearch,
            HomeErrorSearchMaxResults = settings.HomeErrorSearchMaxResults,
            EnableStructuredAddress = settings.EnableStructuredAddress,
            RequireProvinceCity = settings.RequireProvinceCity,
            RequirePostalCode = settings.RequirePostalCode,
            EnableCustomerOrderCancel = settings.EnableCustomerOrderCancel,
            CustomerCancelWindowHours = settings.CustomerCancelWindowHours,
            CustomerCancelRequireReason = settings.CustomerCancelRequireReason,
            EnableHomeBestSellers = settings.EnableHomeBestSellers,
            EnableHomeRestocked = settings.EnableHomeRestocked,
            ShowEnamadSeal = settings.ShowEnamadSeal,
            HomeModules = homeModulesAvailable
                ? settings.HomeModules
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new SiteHomeModuleDto
                    {
                        Key = (SiteHomeModuleKey)x.Key,
                        IsEnabled = x.IsEnabled,
                        SortOrder = x.SortOrder
                    })
                    .ToList()
                : new List<SiteHomeModuleDto>()
        };
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpPost("upload-operator-image")]
    public async Task<IActionResult> UploadOperatorImage(IFormFile file)
    {
        if (file == null || file.Length <= 0)
            return BadRequest("فایل خالی است");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".svg" };
        if (!allowed.Contains(ext))
            return BadRequest("فرمت تصویر مجاز نیست (jpg, png, webp, gif, svg)");

        const int maxKb = 512;
        if (file.Length > maxKb * 1024)
            return BadRequest($"حجم فایل نباید بیش از {maxKb} کیلوبایت باشد");

        var relativeFolder = "uploads/chat-operator";
        var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var uploadsRoot = Path.Combine(wwwroot, relativeFolder);
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsRoot, fileName);
        await using (var outStream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(outStream);
        }

        return Ok(new { Url = $"/{relativeFolder}/{fileName}" });
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpPut]
    public async Task<IActionResult> Update(SiteSettingsDto dto)
    {
        SiteSettings? settings;
        var homeModulesAvailable = true;
        try
        {
            settings = await _db.SiteSettings
                .Include(x => x.HomeModules)
                .FirstOrDefaultAsync();
        }
        catch
        {
            homeModulesAvailable = false;
            settings = await _db.SiteSettings.FirstOrDefaultAsync();
        }
        if (settings == null)
        {
            settings = new SiteSettings();
            _db.SiteSettings.Add(settings);
        }

        if (homeModulesAvailable)
        {
            EnsureHomeModules(settings);
        }

        if (homeModulesAvailable && dto.HomeModules != null && dto.HomeModules.Count > 0)
        {
            foreach (var m in dto.HomeModules)
            {
                var keyInt = (int)m.Key;
                var existing = settings.HomeModules.FirstOrDefault(x => x.Key == keyInt);
                if (existing == null)
                {
                    settings.HomeModules.Add(new SiteHomeModuleSetting
                    {
                        Key = keyInt,
                        IsEnabled = m.IsEnabled,
                        SortOrder = m.SortOrder
                    });
                }
                else
                {
                    existing.IsEnabled = m.IsEnabled;
                    existing.SortOrder = m.SortOrder;
                }
            }
        }
        else
        {
            settings.ShowSpecialOffers = dto.ShowSpecialOffers;
            settings.ShowLatestNews = dto.ShowLatestNews;
            settings.ShowLatestProducts = dto.ShowLatestProducts;
            settings.ShowLatestAcademy = dto.ShowLatestAcademy;
            settings.ShowLatestCourses = dto.ShowLatestCourses;
            settings.ShowLatestArticles = dto.ShowLatestArticles;
            settings.ShowTeamMembers = dto.ShowTeamMembers;
            settings.ShowHomeCategories = dto.ShowHomeCategories;
            settings.ShowFeatureCards = dto.ShowFeatureCards;
            settings.ShowStories = dto.ShowStories;
            if (homeModulesAvailable)
            {
                SyncModulesFromLegacyFlags(settings);
            }
        }

        if (homeModulesAvailable)
        {
            SyncLegacyFlagsFromModules(settings);
        }
        settings.AllowGuestCheckout = dto.AllowGuestCheckout;
        settings.AllowGuestChat = dto.AllowGuestChat;
        settings.EnableOnlineChat = dto.EnableOnlineChat;
        settings.EnableBaleChat = dto.EnableBaleChat;
        settings.SeoTitle = dto.SeoTitle;
        settings.SeoDescription = dto.SeoDescription;
        settings.SeoKeywords = dto.SeoKeywords;
        settings.GoogleAnalyticsTag = dto.GoogleAnalyticsTag;
        settings.DefaultOgImage = dto.DefaultOgImage;
        settings.IsMaintenanceMode = dto.IsMaintenanceMode;
        settings.EnableSsl = dto.EnableSsl;
        settings.HstsMaxAgeDays = dto.HstsMaxAgeDays;
        settings.MaxImageUploadSizeKb = dto.MaxImageUploadSizeKb;
        settings.ChartBaseUrl = dto.ChartBaseUrl;
        settings.BaleBotToken = dto.BaleBotToken;
        settings.BaleBotGroupId = dto.BaleBotGroupId;
        settings.EnableTelegramChat = dto.EnableTelegramChat;
        settings.TelegramBotToken = dto.TelegramBotToken;
        settings.TelegramGroupId = dto.TelegramGroupId;
        settings.EnableEitaaChat = dto.EnableEitaaChat;
        settings.EitaaBotToken = dto.EitaaBotToken;
        settings.EitaaGroupId = dto.EitaaGroupId;
        settings.ChatWelcomeMessage = dto.ChatWelcomeMessage;
        settings.ChatEnableAutoMessage = dto.ChatEnableAutoMessage;
        settings.ChatAutoMessageSeconds = dto.ChatAutoMessageSeconds;
        settings.ChatPhoneRequired = dto.ChatPhoneRequired;
        settings.ChatOperatorImageUrl = string.IsNullOrWhiteSpace(dto.ChatOperatorImageUrl) ? null : dto.ChatOperatorImageUrl.Trim();
        settings.ChatGreetingEnabled = dto.ChatGreetingEnabled;
        settings.ChatGreetingText = string.IsNullOrWhiteSpace(dto.ChatGreetingText) ? null : dto.ChatGreetingText.Trim();
        settings.ChatSuggestedTopics = string.IsNullOrWhiteSpace(dto.ChatSuggestedTopics) ? null : dto.ChatSuggestedTopics.Trim();
        settings.ChatEnableAiAssistant = dto.ChatEnableAiAssistant;
        settings.ChatAiProvider = dto.ChatAiProvider;
        settings.ChatAiApiUrl = dto.ChatAiApiUrl;
        settings.ChatAiModel = dto.ChatAiModel;
        settings.ChatAiApiKey = dto.ChatAiApiKey;
        settings.ChatAiSystemPrompt = dto.ChatAiSystemPrompt;
        settings.RequireReviewApproval = dto.RequireReviewApproval;
        settings.EnableProductReviews = dto.EnableProductReviews;
        settings.EnableReviewMedia = dto.EnableReviewMedia;
        settings.EnableVerifiedBuyerBadge = dto.EnableVerifiedBuyerBadge;
        settings.EnableReviewGallery = dto.EnableReviewGallery;
        settings.EnableProductQna = dto.EnableProductQna;
        settings.EnableSpecsTable = dto.EnableSpecsTable;
        settings.EnablePriceHistoryChart = dto.EnablePriceHistoryChart;
        settings.EnableProductBadges = dto.EnableProductBadges;
        settings.EnableTrustBar = dto.EnableTrustBar;
        settings.EnableDeliveryEta = dto.EnableDeliveryEta;
        settings.EnableWishlist = dto.EnableWishlist;
        settings.EnableStockAlert = dto.EnableStockAlert;
        settings.EnableRelatedProducts = dto.EnableRelatedProducts;
        settings.EnableRecentlyViewed = dto.EnableRecentlyViewed;
        settings.EnableFreeShipping = dto.EnableFreeShipping;
        settings.FreeShippingThreshold = dto.FreeShippingThreshold;
        settings.EnableReturnRequests = dto.EnableReturnRequests;
        settings.EnableReorder = dto.EnableReorder;
        settings.EnableQuickView = dto.EnableQuickView;
        settings.EnableCompare = dto.EnableCompare;
        settings.CompareMaxItems = dto.CompareMaxItems <= 0 ? 4 : Math.Clamp(dto.CompareMaxItems, 2, 6);
        settings.EnableMegaMenu = dto.EnableMegaMenu;
        settings.EnableBreadcrumb = dto.EnableBreadcrumb;
        settings.EnableHomeStats = dto.EnableHomeStats;
        settings.ShowOrderCountStat = dto.ShowOrderCountStat;
        settings.EnableHomeErrorSearch = dto.EnableHomeErrorSearch;
        settings.HomeErrorSearchMaxResults = dto.HomeErrorSearchMaxResults <= 0 ? 6 : Math.Clamp(dto.HomeErrorSearchMaxResults, 3, 10);
        settings.EnableStructuredAddress = dto.EnableStructuredAddress;
        settings.RequireProvinceCity = dto.RequireProvinceCity;
        settings.RequirePostalCode = dto.RequirePostalCode;
        settings.EnableCustomerOrderCancel = dto.EnableCustomerOrderCancel;
        settings.CustomerCancelWindowHours = Math.Clamp(dto.CustomerCancelWindowHours, 0, 720);
        settings.CustomerCancelRequireReason = dto.CustomerCancelRequireReason;
        settings.EnableHomeBestSellers = dto.EnableHomeBestSellers;
        settings.EnableHomeRestocked = dto.EnableHomeRestocked;
        settings.ShowEnamadSeal = dto.ShowEnamadSeal;

        await _db.SaveChangesAsync();
        try { await _outputCache.EvictByTagAsync(SettingsCacheTag, CancellationToken.None); } catch { }
        return NoContent();
    }

    private static bool EnsureHomeModules(SiteSettings settings)
    {
        settings.HomeModules ??= new List<SiteHomeModuleSetting>();

        var validKeys = Enum.GetValues<SiteHomeModuleKey>().Cast<int>().ToHashSet();
        var unknown = settings.HomeModules.Where(x => !validKeys.Contains(x.Key)).ToList();
        foreach (var u in unknown)
            settings.HomeModules.Remove(u);

        AddIfMissing(settings, SiteHomeModuleKey.SpecialOffers, 10);
        AddIfMissing(settings, SiteHomeModuleKey.HomeCategories, 15);
        AddIfMissing(settings, SiteHomeModuleKey.FeatureCards, 18);
        AddIfMissing(settings, SiteHomeModuleKey.PopularBrands, 19);
        AddIfMissing(settings, SiteHomeModuleKey.SomeCustomers, 21);
        AddIfMissing(settings, SiteHomeModuleKey.TechnicalGuide, 22);
        AddIfMissing(settings, SiteHomeModuleKey.LatestNews, 20);
        AddIfMissing(settings, SiteHomeModuleKey.LatestProducts, 30);
        AddIfMissing(settings, SiteHomeModuleKey.LatestAcademy, 44);
        AddIfMissing(settings, SiteHomeModuleKey.AcademyHub, 40);
        AddIfMissing(settings, SiteHomeModuleKey.LatestCourses, 41);
        AddIfMissing(settings, SiteHomeModuleKey.LatestArticles, 42);
        AddIfMissing(settings, SiteHomeModuleKey.TrustStrip, 47);
        AddIfMissing(settings, SiteHomeModuleKey.TeamMembers, 50);
        AddIfMissing(settings, SiteHomeModuleKey.Stories, 5);
        return unknown.Count > 0;
    }

    private static void AddIfMissing(SiteSettings settings, SiteHomeModuleKey key, int sortOrder)
    {
        var keyInt = (int)key;
        if (settings.HomeModules.Any(x => x.Key == keyInt)) return;

        settings.HomeModules.Add(new SiteHomeModuleSetting
        {
            Key = keyInt,
            IsEnabled = GetLegacyFlag(settings, key),
            SortOrder = sortOrder
        });
    }

    private static bool GetLegacyFlag(SiteSettings settings, SiteHomeModuleKey key)
    {
        return key switch
        {
            SiteHomeModuleKey.SpecialOffers => settings.ShowSpecialOffers,
            SiteHomeModuleKey.HomeCategories => settings.ShowHomeCategories,
            SiteHomeModuleKey.FeatureCards => settings.ShowFeatureCards,
            SiteHomeModuleKey.LatestNews => settings.ShowLatestNews,
            SiteHomeModuleKey.LatestProducts => settings.ShowLatestProducts,
            SiteHomeModuleKey.LatestAcademy => settings.ShowLatestAcademy,
            SiteHomeModuleKey.LatestCourses => settings.ShowLatestCourses,
            SiteHomeModuleKey.LatestArticles => settings.ShowLatestArticles,
            SiteHomeModuleKey.TeamMembers => settings.ShowTeamMembers,
            SiteHomeModuleKey.Stories => settings.ShowStories,
            _ => true
        };
    }

    private static void SyncLegacyFlagsFromModules(SiteSettings settings)
    {
        settings.ShowSpecialOffers = settings.HomeModules.FirstOrDefault(x => x.Key == (int)SiteHomeModuleKey.SpecialOffers)?.IsEnabled ?? settings.ShowSpecialOffers;
        settings.ShowHomeCategories = settings.HomeModules.FirstOrDefault(x => x.Key == (int)SiteHomeModuleKey.HomeCategories)?.IsEnabled ?? settings.ShowHomeCategories;
        settings.ShowFeatureCards = settings.HomeModules.FirstOrDefault(x => x.Key == (int)SiteHomeModuleKey.FeatureCards)?.IsEnabled ?? settings.ShowFeatureCards;
        settings.ShowLatestNews = settings.HomeModules.FirstOrDefault(x => x.Key == (int)SiteHomeModuleKey.LatestNews)?.IsEnabled ?? settings.ShowLatestNews;
        settings.ShowLatestProducts = settings.HomeModules.FirstOrDefault(x => x.Key == (int)SiteHomeModuleKey.LatestProducts)?.IsEnabled ?? settings.ShowLatestProducts;
        settings.ShowLatestAcademy = settings.HomeModules.FirstOrDefault(x => x.Key == (int)SiteHomeModuleKey.LatestAcademy)?.IsEnabled ?? settings.ShowLatestAcademy;
        settings.ShowLatestCourses = settings.HomeModules.FirstOrDefault(x => x.Key == (int)SiteHomeModuleKey.LatestCourses)?.IsEnabled ?? settings.ShowLatestCourses;
        settings.ShowLatestArticles = settings.HomeModules.FirstOrDefault(x => x.Key == (int)SiteHomeModuleKey.LatestArticles)?.IsEnabled ?? settings.ShowLatestArticles;
        settings.ShowTeamMembers = settings.HomeModules.FirstOrDefault(x => x.Key == (int)SiteHomeModuleKey.TeamMembers)?.IsEnabled ?? settings.ShowTeamMembers;
        settings.ShowStories = settings.HomeModules.FirstOrDefault(x => x.Key == (int)SiteHomeModuleKey.Stories)?.IsEnabled ?? settings.ShowStories;
    }

    private static void SyncModulesFromLegacyFlags(SiteSettings settings)
    {
        foreach (var m in settings.HomeModules)
        {
            m.IsEnabled = m.Key switch
            {
                (int)SiteHomeModuleKey.SpecialOffers => settings.ShowSpecialOffers,
                (int)SiteHomeModuleKey.HomeCategories => settings.ShowHomeCategories,
                (int)SiteHomeModuleKey.FeatureCards => settings.ShowFeatureCards,
                (int)SiteHomeModuleKey.LatestNews => settings.ShowLatestNews,
                (int)SiteHomeModuleKey.LatestProducts => settings.ShowLatestProducts,
                (int)SiteHomeModuleKey.LatestAcademy => settings.ShowLatestAcademy,
                (int)SiteHomeModuleKey.LatestCourses => settings.ShowLatestCourses,
                (int)SiteHomeModuleKey.LatestArticles => settings.ShowLatestArticles,
                (int)SiteHomeModuleKey.TeamMembers => settings.ShowTeamMembers,
                (int)SiteHomeModuleKey.Stories => settings.ShowStories,
                _ => m.IsEnabled
            };
        }
    }
}
