using System.Collections.Generic;
using System.Linq;

namespace ErrorService.Shared;

/// <summary>
/// عنوان بخش‌های صفحه اصلی. عنوان پیش‌فرض اینجا نگه داشته می‌شود تا هم در
/// پنل ادمین به‌عنوان راهنما نشان داده شود و هم در سایت، جای یکسانی داشته باشد.
/// </summary>
public static class HomeSectionTitles
{
    public const int MaxLength = 120;

    public static string Default(SiteHomeModuleKey key) => key switch
    {
        SiteHomeModuleKey.Stories => "استوری‌های امروز",
        SiteHomeModuleKey.SpecialOffers => "پیشنهادات شگفت‌انگیز",
        SiteHomeModuleKey.LatestNews => "آخرین اخبار",
        SiteHomeModuleKey.LatestProducts => "ویترین محصولات",
        SiteHomeModuleKey.BestSellers => "پرفروش‌ترین محصولات",
        SiteHomeModuleKey.Restocked => "محصولات تازه و موجود",
        SiteHomeModuleKey.PopularBrands => "محبوب‌ترین برندها",
        SiteHomeModuleKey.SomeCustomers => "همکاران ما",
        SiteHomeModuleKey.LatestCourses => "جدیدترین دوره‌های آموزشی",
        SiteHomeModuleKey.LatestArticles => "جدیدترین آموزش‌ها",
        SiteHomeModuleKey.AcademyHub => "آکادمی ارورسرویس",
        SiteHomeModuleKey.TrustStrip => "برندها و همکاران ارورسرویس",
        SiteHomeModuleKey.TeamMembers => "تیم ارور سرویس",
        _ => ""
    };

    /// <summary>بخش‌هایی که عنوان قابل ویرایش دارند؛ بقیه تیتر مستقلی ندارند.</summary>
    public static bool HasTitle(SiteHomeModuleKey key) => Default(key).Length > 0;

    public static string? Normalize(string? title)
    {
        var t = title?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length <= MaxLength ? t : t[..MaxLength];
    }

    public static string Resolve(IEnumerable<SiteHomeModuleDto>? modules, SiteHomeModuleKey key, string? fallback = null)
    {
        var custom = Normalize(modules?.FirstOrDefault(x => x.Key == key)?.Title);
        return custom ?? (string.IsNullOrWhiteSpace(fallback) ? Default(key) : fallback.Trim());
    }
}