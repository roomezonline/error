using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SiteSettingsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public SiteSettingsController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [OutputCache(Duration = 60)]
    public async Task<ActionResult<SiteSettingsDto>> Get()
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
            EnsureHomeModules(settings);
            SyncLegacyFlagsFromModules(settings);
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
            ShowFeaturedProducts = settings.ShowFeaturedProducts,
            AllowGuestCheckout = settings.AllowGuestCheckout,
            EnableOnlineChat = settings.EnableOnlineChat,
            AllowGuestChat = settings.AllowGuestChat,
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
            BaleBotToken = settings.BaleBotToken,
            BaleBotGroupId = settings.BaleBotGroupId,
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
            settings.ShowFeaturedProducts = dto.ShowFeaturedProducts;
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

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void EnsureHomeModules(SiteSettings settings)
    {
        settings.HomeModules ??= new List<SiteHomeModuleSetting>();

        AddIfMissing(settings, SiteHomeModuleKey.SpecialOffers, 10);
        AddIfMissing(settings, SiteHomeModuleKey.HomeCategories, 15);
        AddIfMissing(settings, SiteHomeModuleKey.FeatureCards, 18);
        AddIfMissing(settings, SiteHomeModuleKey.PopularBrands, 19);
        AddIfMissing(settings, SiteHomeModuleKey.SomeCustomers, 21);
        AddIfMissing(settings, SiteHomeModuleKey.TechnicalGuide, 22);
        AddIfMissing(settings, SiteHomeModuleKey.LatestNews, 20);
        AddIfMissing(settings, SiteHomeModuleKey.LatestProducts, 30);
        AddIfMissing(settings, SiteHomeModuleKey.LatestAcademy, 40);
        AddIfMissing(settings, SiteHomeModuleKey.LatestCourses, 41);
        AddIfMissing(settings, SiteHomeModuleKey.LatestArticles, 42);
        AddIfMissing(settings, SiteHomeModuleKey.TeamMembers, 50);
        AddIfMissing(settings, SiteHomeModuleKey.Stories, 5);
        AddIfMissing(settings, SiteHomeModuleKey.FeaturedProducts, 12);
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
            SiteHomeModuleKey.FeaturedProducts => settings.ShowFeaturedProducts,
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
        settings.ShowFeaturedProducts = settings.HomeModules.FirstOrDefault(x => x.Key == (int)SiteHomeModuleKey.FeaturedProducts)?.IsEnabled ?? settings.ShowFeaturedProducts;
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
                (int)SiteHomeModuleKey.FeaturedProducts => settings.ShowFeaturedProducts,
                _ => m.IsEnabled
            };
        }
    }
}
