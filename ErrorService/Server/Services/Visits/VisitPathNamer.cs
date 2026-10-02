using ErrorService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services.Visits;

/// <summary>
/// تبدیل مسیر (مثل «/» یا «/products/xxx») به نام فارسی قابل نمایش (مثل «صفحه اصلی» یا نام محصول).
/// اول از نقشهٔ مسیرهای ثابت سایت استفاده می‌شود؛ برای صفحات موجودیت‌دار (محصول، خبر، مقاله، دوره، …)
/// نام از خود دیتابیس خوانده می‌شود تا دقیقاً همان چیزی نمایش داده شود که در سایت دیده می‌شود.
/// </summary>
public static class VisitPathNamer
{
    private const string KindProducts = "products";
    private const string KindNews = "news";
    private const string KindArticles = "articles";
    private const string KindCourses = "courses";
    private const string KindLesson = "lesson";
    private const string KindSensor = "sensor";

    private static readonly Dictionary<string, string> StaticNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/"] = "صفحه اصلی",
        ["/products"] = "فروشگاه (لیست محصولات)",
        ["/news"] = "اخبار و مقالات",
        ["/academy"] = "آکادمی آموزش تعمیرات",
        ["/academy/articles"] = "مقالات آکادمی",
        ["/technical/error-codes"] = "بانک کدهای خطا",
        ["/technical/sensor-finder"] = "سنسوریاب",
        ["/technical/calculators"] = "ماشین‌حساب‌های فنی",
        ["/technical/consultation"] = "درخواست مشاوره فنی",
        ["/contact"] = "تماس با ما",
        ["/about"] = "درباره ما",
        ["/faq"] = "سوالات متداول",
        ["/help/faq"] = "سوالات متداول",
        ["/privacy"] = "حریم خصوصی",
        ["/terms"] = "شرایط استفاده",
        ["/commitment"] = "تعهدنامه",
        ["/cart"] = "سبد خرید",
        ["/checkout"] = "تسویه حساب",
        ["/search"] = "نتایج جستجو",
        ["/profile"] = "پروفایل کاربری",
        ["/my-orders"] = "سفارش‌های من",
        ["/order-tracking"] = "پیگیری سفارش",
        ["/wishlist"] = "علاقه‌مندی‌ها",
        ["/trust/portfolio"] = "نمونه کارها",
        ["/admission/repair"] = "درخواست تعمیر",
        ["/admission/expertise"] = "درخواست کارشناسی",
        ["/monitoring/shared"] = "اشتراک‌گذاری پایش",
        ["/auth/login"] = "ورود",
        ["/auth/register"] = "ثبت‌نام"
    };

    /// <summary>
    /// نقشهٔ «مسیر → نام فارسی» برای مسیرهای داده‌شده.
    /// مسیرهای قابل شناسایی نشده هم با نام خوانا از روی خود آدرس (slugs فارسی) پر می‌شوند تا هیچ ردیفی خالی نماند.
    /// </summary>
    public static async Task<Dictionary<string, string>> ResolveAsync(ErrorServiceDbContext db, IEnumerable<string?> paths)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var unresolved = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unresolved.Count == 0) return map;

        foreach (var p in unresolved)
        {
            if (StaticNames.TryGetValue(p, out var title))
                map[p] = title;
        }

        var pending = unresolved.Where(p => !map.ContainsKey(p)).ToList();
        if (pending.Count == 0) return map;

        // path → (نوع موجودیت، کلید: id یا slug)
        var lookups = new Dictionary<string, (string Kind, int Id, string Slug)>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in pending)
        {
            var segs = Split(path);
            if (segs.Length == 0)
            {
                map[path] = "صفحه اصلی";
                continue;
            }

            if (segs.Length == 2 && segs[0] == "products")
            {
                if (int.TryParse(segs[1], out var pid)) lookups[path] = (KindProducts, pid, "");
                else lookups[path] = (KindProducts, 0, Unescape(segs[1]));
            }
            else if (segs.Length == 2 && segs[0] == "news")
            {
                if (int.TryParse(segs[1], out var nid)) lookups[path] = (KindNews, nid, "");
                else lookups[path] = (KindNews, 0, Unescape(segs[1]));
            }
            else if (segs.Length == 3 && segs[0] == "academy" && segs[1] == "articles")
            {
                if (int.TryParse(segs[2], out var aid)) lookups[path] = (KindArticles, aid, "");
                else lookups[path] = (KindArticles, 0, Unescape(segs[2]));
            }
            else if (segs.Length == 3 && segs[0] == "academy" && int.TryParse(segs[1], out _) && int.TryParse(segs[2], out var lid))
            {
                lookups[path] = (KindLesson, lid, "");
            }
            else if (segs.Length == 2 && segs[0] == "academy")
            {
                if (int.TryParse(segs[1], out var scid)) lookups[path] = (KindCourses, scid, "");
                else lookups[path] = (KindCourses, 0, Unescape(segs[1]));
            }
            else if (segs.Length == 3 && segs[0] == "technical" && segs[1] == "sensor-finder" && int.TryParse(segs[2], out var sid))
            {
                lookups[path] = (KindSensor, sid, "");
            }
        }

        if (lookups.Count > 0)
            await FillFromDatabaseAsync(db, lookups, map);

        // آنچه در دیتابیس پیدا نشد: نام خوانا از خود آدرس (slugs فارسی هستند)
        foreach (var path in pending)
        {
            if (!map.ContainsKey(path))
                map[path] = FriendlyFallback(path);
        }

        return map;
    }

    private static async Task FillFromDatabaseAsync(
        ErrorServiceDbContext db,
        Dictionary<string, (string Kind, int Id, string Slug)> lookups,
        Dictionary<string, string> map)
    {
        var idTitles = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
        var slugTitles = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        void Add(string kind, int id, string? slug, string title)
        {
            if (!idTitles.TryGetValue(kind, out var byId))
                idTitles[kind] = byId = new Dictionary<int, string>();
            byId[id] = title;

            if (!string.IsNullOrWhiteSpace(slug))
            {
                if (!slugTitles.TryGetValue(kind, out var bySlug))
                    slugTitles[kind] = bySlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bySlug[slug] = title;
            }
        }

        foreach (var group in lookups.GroupBy(x => x.Value.Kind))
        {
            var kind = group.Key;
            var ids = group.Where(x => x.Value.Id > 0).Select(x => x.Value.Id).Distinct().ToList();
            var slugs = group.Where(x => x.Value.Slug != "").Select(x => x.Value.Slug).Distinct().ToList();

            switch (kind)
            {
                case KindProducts:
                    if (ids.Count > 0)
                    {
                        var rows = await db.Products.AsNoTracking()
                            .Where(x => ids.Contains(x.Id))
                            .Select(x => new { x.Id, x.Name, x.Slug })
                            .ToListAsync();
                        foreach (var r in rows) Add(kind, r.Id, r.Slug, "محصول: " + r.Name);
                    }
                    if (slugs.Count > 0)
                    {
                        var rows = await db.Products.AsNoTracking()
                            .Where(x => slugs.Contains(x.Slug ?? ""))
                            .Select(x => new { x.Id, x.Name, x.Slug })
                            .ToListAsync();
                        foreach (var r in rows) Add(kind, r.Id, r.Slug, "محصول: " + r.Name);
                    }
                    break;

                case KindNews:
                    if (ids.Count > 0)
                    {
                        var rows = await db.News.AsNoTracking()
                            .Where(x => ids.Contains(x.Id))
                            .Select(x => new { x.Id, x.Title, x.Slug })
                            .ToListAsync();
                        foreach (var r in rows) Add(kind, r.Id, r.Slug, "خبر: " + r.Title);
                    }
                    if (slugs.Count > 0)
                    {
                        var rows = await db.News.AsNoTracking()
                            .Where(x => slugs.Contains(x.Slug ?? ""))
                            .Select(x => new { x.Id, x.Title, x.Slug })
                            .ToListAsync();
                        foreach (var r in rows) Add(kind, r.Id, r.Slug, "خبر: " + r.Title);
                    }
                    break;

                case KindArticles:
                    if (ids.Count > 0)
                    {
                        var rows = await db.TrainingArticles.AsNoTracking()
                            .Where(x => ids.Contains(x.Id))
                            .Select(x => new { x.Id, x.Title, x.Slug })
                            .ToListAsync();
                        foreach (var r in rows) Add(kind, r.Id, r.Slug, "مقاله: " + r.Title);
                    }
                    if (slugs.Count > 0)
                    {
                        var rows = await db.TrainingArticles.AsNoTracking()
                            .Where(x => slugs.Contains(x.Slug ?? ""))
                            .Select(x => new { x.Id, x.Title, x.Slug })
                            .ToListAsync();
                        foreach (var r in rows) Add(kind, r.Id, r.Slug, "مقاله: " + r.Title);
                    }
                    break;

                case KindCourses:
                    if (ids.Count > 0)
                    {
                        var rows = await db.TrainingCourses.AsNoTracking()
                            .Where(x => ids.Contains(x.Id))
                            .Select(x => new { x.Id, x.Title, x.Slug })
                            .ToListAsync();
                        foreach (var r in rows) Add(kind, r.Id, r.Slug, "دوره: " + r.Title);
                    }
                    if (slugs.Count > 0)
                    {
                        var rows = await db.TrainingCourses.AsNoTracking()
                            .Where(x => slugs.Contains(x.Slug ?? ""))
                            .Select(x => new { x.Id, x.Title, x.Slug })
                            .ToListAsync();
                        foreach (var r in rows) Add(kind, r.Id, r.Slug, "دوره: " + r.Title);
                    }
                    break;

                case KindLesson:
                    if (ids.Count > 0)
                    {
                        var rows = await db.TrainingLessons.AsNoTracking()
                            .Where(x => ids.Contains(x.Id))
                            .Select(x => new { x.Id, x.Title })
                            .ToListAsync();
                        foreach (var r in rows) Add(kind, r.Id, null, "درس: " + r.Title);
                    }
                    break;

                case KindSensor:
                    if (ids.Count > 0)
                    {
                        var rows = await db.SensorFinderRecords.AsNoTracking()
                            .Where(x => ids.Contains(x.Id))
                            .Select(x => new { x.Id, x.SensorName })
                            .ToListAsync();
                        foreach (var r in rows) Add(kind, r.Id, null, "سنسور: " + r.SensorName);
                    }
                    break;
            }
        }

        foreach (var kv in lookups)
        {
            if (map.ContainsKey(kv.Key)) continue;

            if (kv.Value.Id > 0 &&
                idTitles.TryGetValue(kv.Value.Kind, out var byId) &&
                byId.TryGetValue(kv.Value.Id, out var titleById))
            {
                map[kv.Key] = titleById;
            }
            else if (kv.Value.Slug != "" &&
                     slugTitles.TryGetValue(kv.Value.Kind, out var bySlug) &&
                     bySlug.TryGetValue(kv.Value.Slug, out var titleBySlug))
            {
                map[kv.Key] = titleBySlug;
            }
        }
    }

    private static string FriendlyFallback(string path)
    {
        var segs = Split(path);
        if (segs.Length == 0) return "صفحه اصلی";

        // /technical/error-codes/{brand}/{device}/{code}
        if (segs.Length == 5 && segs[0] == "technical" && segs[1] == "error-codes")
            return $"کد خطا {Decode(segs[4])} ({Decode(segs[2])} {Decode(segs[3])})";

        var last = Decode(segs[^1]);
        if (string.IsNullOrWhiteSpace(last)) return path;

        var prefix = segs[0] switch
        {
            "products" => "محصول: ",
            "news" => "خبر: ",
            "academy" => "آموزش: ",
            "technical" => "بخش فنی: ",
            "my-orders" => "سفارش ",
            "payment" => "پرداخت سفارش ",
            _ => ""
        };

        return prefix + last;
    }

    private static string[] Split(string path)
        => path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static string Decode(string segment)
    {
        string decoded;
        try { decoded = Uri.UnescapeDataString(segment); }
        catch { decoded = segment; }
        return decoded.Replace('-', ' ').Trim();
    }

    /// <summary>بازگردانی URL-encoding (برای تطبیق slugهای فارسی ذخیره‌شده در لاگ‌ها).</summary>
    private static string Unescape(string segment)
    {
        try { return Uri.UnescapeDataString(segment); }
        catch { return segment; }
    }
}
