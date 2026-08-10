using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using ErrorService.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ErrorService.Server.Infrastructure;

public sealed class SeoFallbackMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SeoFallbackMiddleware> _logger;
    private string? _indexHtmlTemplate;
    private readonly ConcurrentDictionary<string, (string html, int statusCode, DateTime cachedAt)> _cache = new();
    private readonly string _baseUrl;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly Dictionary<string, (string title, string description, string keywords, string jsonLdType)> StaticPages = new()
    {
        [""] = ("ارورسرویس | تعمیر و سرویس لوازم خانگی، کد خطا و قطعات یدکی",
                "ارورسرویس، مرجع تخصصی کدهای خطای لوازم خانگی، آموزش تعمیرات، قطعات یدکی و سرویس در منزل. بانک جامع ارور ماشین لباسشویی، یخچال، ظرفشویی و ماکروفر.",
                "کد خطای لوازم خانگی, تعمیر لوازم خانگی, قطعات یدکی, سرویس لوازم خانگی, ارور ماشین لباسشویی, ارور یخچال, تعمیرات لوازم خانگی",
                "Organization"),
        ["/products"] = ("فروشگاه قطعات یدکی | ارورسرویس",
                         "خرید قطعات یدکی اصل لوازم خانگی سامسونگ، ال‌جی، بوش و... با ارسال سریع و ضمانت اصالت",
                         "قطعات یدکی لوازم خانگی, خرید قطعات یدکی, لوازم یدکی سامسونگ, لوازم یدکی ال جی, بوش",
                         "CollectionPage"),
        ["/news"] = ("اخبار و مقالات تعمیرات لوازم خانگی | ارورسرویس",
                     "جدیدترین اخبار، مقالات آموزشی و راهنمای تعمیرات لوازم خانگی در ارورسرویس",
                     "اخبار لوازم خانگی, مقالات تعمیرات, آموزش تعمیر لوازم خانگی",
                     "CollectionPage"),
        ["/academy"] = ("آکادمی تعمیرات لوازم خانگی | ارورسرویس",
                        "دوره‌های آموزشی تعمیرات لوازم خانگی، مقالات تخصصی و ویدیوهای آموزشی",
                        "آموزش تعمیرات لوازم خانگی, دوره تعمیر یخچال, دوره تعمیر لباسشویی, آکادمی تعمیرات",
                        "CollectionPage"),
        ["/technical/error-codes"] = ("بانک کدهای خطا | عیب‌یابی هوشمند لوازم خانگی",
                                      "جستجو و مشاهده لیست کامل کدهای خطای یخچال، ماشین لباسشویی، ظرفشویی و پکیج برندهای سامسونگ، ال‌جی، بوش و...",
                                      "کد خطا، ارور یخچال، ارور لباسشویی، لیست کدهای خطا، تعمیرات لوازم خانگی",
                                      "CollectionPage"),
        ["/technical/sensor-finder"] = ("سنسور یاب هوشمند | ارورسرویس",
                                       "سنسور یاب هوشمند ارورسرویس، جستجو و شناسایی سنسورهای لوازم خانگی با مشخصات فنی کامل",
                                       "سنسور لوازم خانگی, سنسور یاب, شناسایی سنسور, سنسور ماشین لباسشویی, سنسور یخچال",
                                       "CollectionPage"),
        ["/technical/calculators"] = ("ماشین‌حساب تعمیرات | ارورسرویس",
                                      "ابزارهای محاسباتی برای تعمیرکاران لوازم خانگی",
                                      "ماشین حساب تعمیرات, محاسبه هزینه تعمیر",
                                      "WebPage"),
        ["/technical/consultation"] = ("مشاوره تعمیرات لوازم خانگی | ارورسرویس",
                                      "دریافت مشاوره تخصصی تعمیرات لوازم خانگی از کارشناسان ارورسرویس",
                                      "مشاوره تعمیرات, راهنمای تعمیر لوازم خانگی",
                                      "ContactPage"),
        ["/contact"] = ("تماس با ما | ارورسرویس",
                        "اطلاعات تماس، آدرس و شماره تلفن‌های ارورسرویس",
                        "تماس با ارورسرویس, آدرس ارورسرویس, شماره تلفن ارورسرویس",
                        "ContactPage"),
        ["/about"] = ("درباره ما | ارورسرویس",
                      "آشنایی با تیم ارورسرویس، خدمات و سوابق ما در زمینه تعمیرات لوازم خانگی",
                      "درباره ارورسرویس, تیم تعمیرات لوازم خانگی, تاریخچه ارورسرویس",
                      "AboutPage"),
        ["/faq"] = ("سوالات متداول | ارورسرویس",
                    "پاسخ به سوالات متداول شما درباره خدمات تعمیرات لوازم خانگی ارورسرویس",
                    "سوالات متداول, پشتیبانی ارورسرویس, راهنمای استفاده",
                    "FAQPage"),
        ["/privacy"] = ("حریم خصوصی | ارورسرویس",
                        "سیاست حفظ حریم خصوصی کاربران در ارورسرویس",
                        "حریم خصوصی, سیاست حفاظت از داده ها",
                        "WebPage"),
        ["/terms"] = ("شرایط استفاده | ارورسرویس",
                      "شرایط و قوانین استفاده از خدمات ارورسرویس",
                      "شرایط استفاده, قوانین سایت, ضوابط استفاده",
                      "WebPage"),
        ["/trust/portfolio"] = ("نمونه کارها | ارورسرویس",
                                "نمونه کارهای انجام شده توسط تیم ارورسرویس در زمینه تعمیرات لوازم خانگی",
                                "نمونه کار تعمیرات, پروژه های ارورسرویس",
                                "CollectionPage"),
        ["/admission/repair"] = ("درخواست تعمیر | ارورسرویس",
                                 "ثبت درخواست تعمیر لوازم خانگی در ارورسرویس",
                                 "درخواست تعمیر, ثبت سفارش تعمیر, تعمیر لوازم خانگی در منزل",
                                 "WebPage"),
        ["/admission/expertise"] = ("درخواست کارشناسی | ارورسرویس",
                                    "ثبت درخواست کارشناسی و عیب‌یابی لوازم خانگی",
                                    "کارشناسی لوازم خانگی, عیب‌یابی, بررسی فنی",
                                    "WebPage"),
    };

    public SeoFallbackMiddleware(RequestDelegate next, IWebHostEnvironment env, ILogger<SeoFallbackMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _env = env;
        _logger = logger;
        _baseUrl = configuration.GetValue<string>("Site:_baseUrl") ?? "https://errorservice.ir";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (IsPassthroughOnly(path))
        {
            await _next(context);
            return;
        }

        // Strip trailing slash for canonical consistency
        if (path.Length > 1 && path.EndsWith('/'))
        {
            path = path.TrimEnd('/');
        }

        var db = context.RequestServices.GetRequiredService<ErrorServiceDbContext>();
        var cleanPath = path.TrimStart('/').ToLowerInvariant().TrimEnd('/');

        // Permanent redirect for legacy numeric entity URLs -> keyword-rich slug URLs
        var redirect = await TryResolveRedirect(cleanPath, db);
        if (redirect != null)
        {
            context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
            context.Response.Headers.Location = _baseUrl + string.Join("/", redirect.Split('/').Select(segment => Uri.EscapeDataString(segment)));
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("Moved Permanently");
            return;
        }

        var (html, statusCode) = await GetSeoHtml(path, db);
        if (html != null)
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync(html);
            return;
        }

        await _next(context);
    }

    private static bool IsPassthroughOnly(string path)
    {
        return path.StartsWith("/api/") ||
               path.StartsWith("/_blazor") ||
               path.StartsWith("/hubs/") ||
               path.StartsWith("/upload") ||
               path.Contains('.') ||
               path.StartsWith("/_framework") ||
               path.StartsWith("/_content");
    }

    private async Task<(string? html, int statusCode)> GetSeoHtml(string path, ErrorServiceDbContext db)
    {
        var cacheKey = path.ToLowerInvariant();

        if (_cache.TryGetValue(cacheKey, out var cached) && (DateTime.UtcNow - cached.cachedAt) < CacheDuration)
            return (cached.html, cached.statusCode);

        var template = await GetTemplate();
        if (template == null) return (null, 200);

        var cleanPath = path.TrimStart('/').ToLowerInvariant();

        // Normalize: remove trailing slash
        if (cleanPath.EndsWith('/'))
            cleanPath = cleanPath.TrimEnd('/');

        var canonicalPath = string.Join("/", cleanPath.Split('/').Select(Uri.EscapeDataString));
        var canonical = $"{_baseUrl}/{(canonicalPath == "" ? "" : canonicalPath)}";

        string title, description, keywords, jsonLd;
        string ogImage = "";
        var statusCode = 200;

        if (TryGetDynamicMeta(cleanPath, await TryGetEntityData(cleanPath, db), out var dynMeta, out var entityFound))
        {
            (title, description, keywords, jsonLd, ogImage) = dynMeta;
            if (!entityFound)
            {
                statusCode = 404;
                title = "صفحه مورد نظر یافت نشد | ارورسرویس";
                description = "متاسفانه صفحه مورد نظر شما وجود ندارد یا حذف شده است.";
                keywords = "404, صفحه یافت نشد, ارورسرویس";
                jsonLd = BuildWebPageJsonLd(canonical, title, description);
            }
        }
        else
        {
            var staticKey = cleanPath == "" ? "" : "/" + cleanPath;
            if (StaticPages.TryGetValue(staticKey, out var staticMeta))
            {
                (title, description, keywords, _) = staticMeta;
                jsonLd = BuildStaticJsonLd(staticMeta.jsonLdType, canonical, title, description);
            }
            else
            {
                statusCode = 404;
                title = "صفحه مورد نظر یافت نشد | ارورسرویس";
                description = "متاسفانه صفحه مورد نظر شما وجود ندارد یا حذف شده است.";
                keywords = "404, صفحه یافت نشد, ارورسرویس";
                jsonLd = BuildWebPageJsonLd(canonical, title, description);
            }
        }

        var result = ApplyMetaTags(template, title, description, keywords, canonical, jsonLd, ogImage,
            noindex: statusCode == 404 || cleanPath.StartsWith("admin"));

        _cache[cacheKey] = (result, statusCode, DateTime.UtcNow);
        return (result, statusCode);
    }

    private async Task<string?> TryResolveRedirect(string cleanPath, ErrorServiceDbContext db)
    {
        try
        {
            if (cleanPath.StartsWith("products/"))
            {
                var seg = cleanPath["products/".Length..].Split('/')[0];
                if (int.TryParse(seg, out var pid))
                {
                    var slug = await db.Products
                        .Where(p => p.Id == pid && p.IsAvailable)
                        .Select(p => p.Slug)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrEmpty(slug)) return $"/products/{slug}";
                }
                return null;
            }

            if (cleanPath.StartsWith("news/"))
            {
                var seg = cleanPath["news/".Length..].Split('/')[0];
                if (int.TryParse(seg, out var nid))
                {
                    var slug = await db.News
                        .Where(n => n.Id == nid && n.IsPublished)
                        .Select(n => n.Slug)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrEmpty(slug)) return $"/news/{slug}";
                }
                return null;
            }

            if (cleanPath.StartsWith("academy/articles/"))
            {
                var seg = cleanPath["academy/articles/".Length..].Split('/')[0];
                if (int.TryParse(seg, out var aid))
                {
                    var slug = await db.TrainingArticles
                        .Where(a => a.Id == aid && a.IsPublished)
                        .Select(a => a.Slug)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrEmpty(slug)) return $"/academy/articles/{slug}";
                }
                return null;
            }

            if (cleanPath.StartsWith("academy/"))
            {
                var segments = cleanPath["academy/".Length..].Split('/');
                if (segments.Length == 1 && int.TryParse(segments[0], out var cid))
                {
                    var slug = await db.TrainingCourses
                        .Where(c => c.Id == cid && c.IsPublished)
                        .Select(c => c.Slug)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrEmpty(slug)) return $"/academy/{slug}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SEO middleware: failed to resolve redirect for {Path}", cleanPath);
        }

        return null;
    }

    private async Task<object?> TryGetEntityData(string cleanPath, ErrorServiceDbContext db)
    {
        try
        {
            if (cleanPath.StartsWith("products/"))
            {
                var seg = cleanPath["products/".Length..].Split('/')[0];
                if (int.TryParse(seg, out var pid))
                    return await db.Products.Where(p => p.Id == pid && p.IsAvailable).Select(p => new { p.Id, p.Name, p.Description, p.Price, p.MainImageUrl, p.UpdatedAt }).FirstOrDefaultAsync();

                return await db.Products.Where(p => p.Slug == seg && p.IsAvailable).Select(p => new { p.Id, p.Name, p.Description, p.Price, p.MainImageUrl, p.UpdatedAt }).FirstOrDefaultAsync();
            }

            if (cleanPath.StartsWith("news/"))
            {
                var seg = cleanPath["news/".Length..].Split('/')[0];
                if (int.TryParse(seg, out var nid))
                    return await db.News.Where(n => n.Id == nid && n.IsPublished).Select(n => new { n.Id, n.Title, n.Summary, n.ImageUrl, n.CreatedAt, n.UpdatedAt }).FirstOrDefaultAsync();

                return await db.News.Where(n => n.Slug == seg && n.IsPublished).Select(n => new { n.Id, n.Title, n.Summary, n.ImageUrl, n.CreatedAt, n.UpdatedAt }).FirstOrDefaultAsync();
            }

            if (cleanPath.StartsWith("academy/articles/"))
            {
                var seg = cleanPath["academy/articles/".Length..].Split('/')[0];
                if (int.TryParse(seg, out var aid))
                    return await db.TrainingArticles.Where(a => a.Id == aid && a.IsPublished).Select(a => new { a.Id, a.Title, a.Summary, a.CoverImageUrl, a.UpdatedAt }).FirstOrDefaultAsync();

                return await db.TrainingArticles.Where(a => a.Slug == seg && a.IsPublished).Select(a => new { a.Id, a.Title, a.Summary, a.CoverImageUrl, a.UpdatedAt }).FirstOrDefaultAsync();
            }

            if (cleanPath.StartsWith("academy/"))
            {
                var segments = cleanPath.Replace("academy/", "").Split('/');

                if (segments.Length == 1)
                {
                    if (int.TryParse(segments[0], out var courseId))
                        return await db.TrainingCourses.Where(c => c.Id == courseId && c.IsPublished).Select(c => new { c.Id, c.Title, c.Summary, c.CoverImageUrl, c.UpdatedAt }).FirstOrDefaultAsync();

                    return await db.TrainingCourses.Where(c => c.Slug == segments[0] && c.IsPublished).Select(c => new { c.Id, c.Title, c.Summary, c.CoverImageUrl, c.UpdatedAt }).FirstOrDefaultAsync();
                }

                if (segments.Length == 2 && int.TryParse(segments[1], out var lessonId))
                {
                    var isNumericCourse = int.TryParse(segments[0], out var numericCourseId);
                    return await db.TrainingLessons
                        .Where(l => l.Id == lessonId && l.IsPublished &&
                                    (isNumericCourse ? l.CourseId == numericCourseId : l.Course.Slug == segments[0]))
                        .Select(l => new { l.Id, l.Title, l.CourseId, CourseTitle = l.Course.Title, l.UpdatedAt })
                        .FirstOrDefaultAsync();
                }
            }

            if (cleanPath.StartsWith("technical/error-codes/"))
            {
                var parts = cleanPath.Replace("technical/error-codes/", "").Split('/');
                if (parts.Length >= 3)
                {
                    var brand = Uri.UnescapeDataString(parts[0].Trim());
                    var device = Uri.UnescapeDataString(parts[1].Trim());
                    var code = Uri.UnescapeDataString(parts[2].Trim());
                    return await db.ErrorCodes.Where(e => e.Brand == brand && e.DeviceType == device && e.Code == code)
                        .Select(e => new { e.Id, e.Brand, e.DeviceType, e.Code, e.Description, e.Solution, e.ImageUrl }).FirstOrDefaultAsync();
                }
            }

            if (cleanPath.StartsWith("technical/sensor-finder/") && int.TryParse(cleanPath.Replace("technical/sensor-finder/", "").Split('/')[0], out var sid))
                return await db.SensorFinderRecords.Where(s => s.Id == sid).Select(s => new { s.Id, s.SensorName, s.SensorType, s.SizeText, s.Notes }).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SEO middleware: failed to fetch entity data for {Path}", cleanPath);
        }

        return null;
    }

    private bool TryGetDynamicMeta(string cleanPath, object? entity, out (string title, string description, string keywords, string jsonLd, string ogImage) meta, out bool entityFound)
    {
        meta = default;
        entityFound = entity != null;

        if (entity == null) return false;

        if (cleanPath.StartsWith("products/"))
        {
            var p = JsonSerializer.SerializeToNode(entity)!;
            var pName = p["Name"]?.GetValue<string>() ?? "محصول";
            var pDesc = p["Description"]?.GetValue<string>() ?? "";
            var price = p["Price"]?.GetValue<decimal>() ?? 0;
            var img = p["MainImageUrl"]?.GetValue<string>() ?? "";
            var updated = p["UpdatedAt"]?.GetValue<DateTime>() ?? DateTime.UtcNow;
            var pTitle = $"{pName} | ارورسرویس";
            var pDescription = string.IsNullOrEmpty(pDesc) ? $"خرید {pName} با ضمانت اصالت کالا" : pDesc.Length > 150 ? pDesc[..150] + "..." : pDesc;
            var pKeywords = $"خرید {pName}, قطعات یدکی, {pName}";
            var pBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{{""@type"":""ListItem"",""position"":1,""name"":""فروشگاه"",""item"":""{_baseUrl}/products""}},{{""@type"":""ListItem"",""position"":2,""name"":""{EscapeJson(pName)}"",""item"":""{_baseUrl}/{cleanPath}""}}]}}";
            var pJsonLd = $@"[{{""@context"":""https://schema.org"",""@type"":""Product"",""name"":""{EscapeJson(pName)}"",""description"":""{EscapeJson(pDescription)}"",""image"":""{img}"",""offers"":{{""@type"":""Offer"",""price"":{price},""priceCurrency"":""IRR"",""availability"":""https://schema.org/InStock""}},""url"":""{_baseUrl}/{cleanPath}""}},{pBreadcrumb}]";
            meta = (pTitle, pDescription, pKeywords, pJsonLd, img);
            return true;
        }

        if (cleanPath.StartsWith("news/"))
        {
            var n = JsonSerializer.SerializeToNode(entity)!;
            var nTitleText = n["Title"]?.GetValue<string>() ?? "خبر";
            var summary = n["Summary"]?.GetValue<string>() ?? "";
            var img = n["ImageUrl"]?.GetValue<string>() ?? "";
            var created = n["CreatedAt"]?.GetValue<DateTime>() ?? DateTime.UtcNow;
            var nTitle = $"{nTitleText} | ارورسرویس";
            var nDesc = string.IsNullOrEmpty(summary) ? $"مطالعه خبر {nTitleText}" : summary.Length > 150 ? summary[..150] + "..." : summary;
            var nKeywords = $"خبر, {nTitleText}, اخبار لوازم خانگی";
            var nBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{{""@type"":""ListItem"",""position"":1,""name"":""اخبار"",""item"":""{_baseUrl}/news""}},{{""@type"":""ListItem"",""position"":2,""name"":""{EscapeJson(nTitleText)}"",""item"":""{_baseUrl}/{cleanPath}""}}]}}";
            var nJsonLd = $@"[{{""@context"":""https://schema.org"",""@type"":""NewsArticle"",""headline"":""{EscapeJson(nTitleText)}"",""description"":""{EscapeJson(nDesc)}"",""image"":""{img}"",""datePublished"":""{created:yyyy-MM-dd}"",""author"":{{""@type"":""Organization"",""name"":""ارورسرویس""}},""publisher"":{{""@type"":""Organization"",""name"":""ارورسرویس""}},""url"":""{_baseUrl}/{cleanPath}""}},{nBreadcrumb}]";
            meta = (nTitle, nDesc, nKeywords, nJsonLd, img);
            return true;
        }

        if (cleanPath.StartsWith("academy/articles/"))
        {
            var a = JsonSerializer.SerializeToNode(entity)!;
            var aTitleText = a["Title"]?.GetValue<string>() ?? "مقاله آموزشی";
            var summary = a["Summary"]?.GetValue<string>() ?? "";
            var img = a["CoverImageUrl"]?.GetValue<string>() ?? "";
            var updated = a["UpdatedAt"]?.GetValue<DateTime>() ?? DateTime.UtcNow;
            var aTitle = $"{aTitleText} | آکادمی ارورسرویس";
            var aDesc = string.IsNullOrEmpty(summary) ? $"مطالعه مقاله آموزشی {aTitleText}" : summary.Length > 150 ? summary[..150] + "..." : summary;
            var aKeywords = $"مقاله آموزشی, {aTitleText}, آموزش تعمیرات";
            var aBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{{""@type"":""ListItem"",""position"":1,""name"":""آکادمی"",""item"":""{_baseUrl}/academy""}},{{""@type"":""ListItem"",""position"":2,""name"":""{EscapeJson(aTitleText)}"",""item"":""{_baseUrl}/{cleanPath}""}}]}}";
            var aJsonLd = $@"[{{""@context"":""https://schema.org"",""@type"":""Article"",""headline"":""{EscapeJson(aTitleText)}"",""description"":""{EscapeJson(aDesc)}"",""image"":""{img}"",""dateModified"":""{updated:yyyy-MM-dd}"",""author"":{{""@type"":""Organization"",""name"":""ارورسرویس""}},""publisher"":{{""@type"":""Organization"",""name"":""ارورسرویس""}},""url"":""{_baseUrl}/{cleanPath}""}},{aBreadcrumb}]";
            meta = (aTitle, aDesc, aKeywords, aJsonLd, img);
            return true;
        }

        if (cleanPath.StartsWith("academy/"))
        {
            if (cleanPath.Contains('/'))
            {
                var parts = cleanPath.Replace("academy/", "").Split('/');
                if (parts.Length > 1)
                {
                    var l = JsonSerializer.SerializeToNode(entity)!;
                    var titleText = l["Title"]?.GetValue<string>() ?? "قسمت آموزشی";
                    var courseTitle = l["CourseTitle"]?.GetValue<string>() ?? "";
                    var lTitle = $"{titleText} - {courseTitle} | آکادمی ارورسرویس";
                    var lDesc = $"مشاهده قسمت {titleText} از دوره {courseTitle} در آکادمی ارورسرویس";
                    var lKeywords = $"آموزش تعمیرات, {titleText}, {courseTitle}";
                    var lBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{{""@type"":""ListItem"",""position"":1,""name"":""آکادمی"",""item"":""{_baseUrl}/academy""}},{{""@type"":""ListItem"",""position"":2,""name"":""{EscapeJson(courseTitle)}"",""item"":""{_baseUrl}/academy/{parts[0]}""}},{{""@type"":""ListItem"",""position"":3,""name"":""{EscapeJson(titleText)}"",""item"":""{_baseUrl}/{cleanPath}""}}]}}";
                    var lJsonLd = $@"[{{""@context"":""https://schema.org"",""@type"":""LearningResource"",""name"":""{EscapeJson(titleText)}"",""description"":""{EscapeJson(lDesc)}"",""learningResourceType"":""Lesson"",""url"":""{_baseUrl}/{cleanPath}""}},{lBreadcrumb}]";
                    meta = (lTitle, lDesc, lKeywords, lJsonLd, "");
                    return true;
                }
            }

            var c = JsonSerializer.SerializeToNode(entity)!;
            var courseName = c["Title"]?.GetValue<string>() ?? "دوره آموزشی";
            var courseSummary = c["Summary"]?.GetValue<string>() ?? "";
            var courseImg = c["CoverImageUrl"]?.GetValue<string>() ?? "";
            var cUpdated = c["UpdatedAt"]?.GetValue<DateTime>() ?? DateTime.UtcNow;
            var cTitle = $"{courseName} | آکادمی ارورسرویس";
            var cDesc = string.IsNullOrEmpty(courseSummary) ? $"دوره آموزشی {courseName}" : courseSummary.Length > 150 ? courseSummary[..150] + "..." : courseSummary;
            var cKeywords = $"دوره آموزشی, {courseName}, آموزش تعمیرات";
            var cBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{{""@type"":""ListItem"",""position"":1,""name"":""آکادمی"",""item"":""{_baseUrl}/academy""}},{{""@type"":""ListItem"",""position"":2,""name"":""{EscapeJson(courseName)}"",""item"":""{_baseUrl}/{cleanPath}""}}]}}";
            var cJsonLd = $@"[{{""@context"":""https://schema.org"",""@type"":""Course"",""name"":""{EscapeJson(courseName)}"",""description"":""{EscapeJson(cDesc)}"",""image"":""{courseImg}"",""dateModified"":""{cUpdated:yyyy-MM-dd}"",""provider"":{{""@type"":""Organization"",""name"":""ارورسرویس""}},""url"":""{_baseUrl}/{cleanPath}""}},{cBreadcrumb}]";
            meta = (cTitle, cDesc, cKeywords, cJsonLd, courseImg);
            return true;
        }

        if (cleanPath.StartsWith("technical/error-codes/"))
        {
            var e = JsonSerializer.SerializeToNode(entity)!;
            var brand = e["Brand"]?.GetValue<string>() ?? "";
            var device = e["DeviceType"]?.GetValue<string>() ?? "";
            var code = e["Code"]?.GetValue<string>() ?? "";
            var eDesc = e["Description"]?.GetValue<string>() ?? "";
            var solution = e["Solution"]?.GetValue<string>() ?? "";
            var img = e["ImageUrl"]?.GetValue<string>() ?? "";
            var eTitle = $"کد خطای {code} {brand} {device} | ارورسرویس";
            var eDescription = string.IsNullOrEmpty(eDesc) ? $"کد خطای {code} {brand} {device} و راه حل تعمیر" : eDesc.Length > 150 ? eDesc[..150] + "..." : eDesc;
            var eKeywords = $"کد خطای {code}, {brand}, {device}, عیب‌یابی, تعمیرات";
            var eBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{{""@type"":""ListItem"",""position"":1,""name"":""کدهای خطا"",""item"":""{_baseUrl}/technical/error-codes""}},{{""@type"":""ListItem"",""position"":2,""name"":""کد خطای {EscapeJson(code)} {EscapeJson(brand)} {EscapeJson(device)}"",""item"":""{_baseUrl}/{cleanPath}""}}]}}";
            var eJsonLd = $@"[{{""@context"":""https://schema.org"",""@type"":""TechArticle"",""headline"":""{EscapeJson(eTitle)}"",""description"":""{EscapeJson(eDescription)}"",""image"":""{img}"",""proficiencyLevel"":""Beginner"",""dependencies"":""{EscapeJson(brand)} {EscapeJson(device)}"",""url"":""{_baseUrl}/{cleanPath}""}},{eBreadcrumb}]";
            meta = (eTitle, eDescription, eKeywords, eJsonLd, img);
            return true;
        }

        if (cleanPath.StartsWith("technical/sensor-finder/"))
        {
            var s = JsonSerializer.SerializeToNode(entity)!;
            var sName = s["SensorName"]?.GetValue<string>() ?? "سنسور";
            var sType = s["SensorType"]?.GetValue<string>() ?? "";
            var size = s["SizeText"]?.GetValue<string>() ?? "";
            var sTitle = $"سنسور {sName} | ارورسرویس";
            var sDesc = $"مشخصات فنی سنسور {sName}" + (!string.IsNullOrEmpty(sType) ? $" - نوع: {sType}" : "") + (!string.IsNullOrEmpty(size) ? $" - سایز: {size}" : "");
            var sKeywords = $"سنسور, {sName}, مشخصات فنی";
            var sBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{{""@type"":""ListItem"",""position"":1,""name"":""سنسور یاب"",""item"":""{_baseUrl}/technical/sensor-finder""}},{{""@type"":""ListItem"",""position"":2,""name"":""{EscapeJson(sName)}"",""item"":""{_baseUrl}/{cleanPath}""}}]}}";
            var sJsonLd = $@"[{{""@context"":""https://schema.org"",""@type"":""Product"",""name"":""{EscapeJson(sName)}"",""description"":""{EscapeJson(sDesc)}"",""category"":""{EscapeJson(sType)}"",""url"":""{_baseUrl}/{cleanPath}""}},{sBreadcrumb}]";
            meta = (sTitle, sDesc, sKeywords, sJsonLd, "");
            return true;
        }

        return false;
    }

    private string BuildStaticJsonLd(string type, string canonical, string title, string description)
    {
        if (type == "Organization")
        {
            return $@"[{{""@context"":""https://schema.org"",""@type"":""Organization"",""name"":""ارورسرویس"",""url"":""{_baseUrl}"",""logo"":""{_baseUrl}/images/branding/logo.svg"",""contactPoint"":{{""@type"":""ContactPoint"",""telephone"":""+98-21-12345678"",""contactType"":""customer service"",""areaServed"":""IR""}}}},{{""@context"":""https://schema.org"",""@type"":""WebSite"",""name"":""ارورسرویس"",""url"":""{_baseUrl}"",""potentialAction"":{{""@type"":""SearchAction"",""target"":""{_baseUrl}/search?q={{search_term_string}}"",""query-input"":""required name=search_term_string""}}}}]";
        }

        if (type == "FAQPage")
        {
            return $@"{{""@context"":""https://schema.org"",""@type"":""FAQPage"",""mainEntity"":[],""url"":""{canonical}""}}";
        }

        return $@"{{""@context"":""https://schema.org"",""@type"":""{type}"",""name"":""{EscapeJson(title)}"",""description"":""{EscapeJson(description)}"",""url"":""{canonical}""}}";
    }

    private string BuildWebPageJsonLd(string canonical, string title, string description)
    {
        return $@"{{""@context"":""https://schema.org"",""@type"":""WebPage"",""name"":""{EscapeJson(title)}"",""description"":""{EscapeJson(description)}"",""url"":""{canonical}""}}";
    }

    private static string ApplyMetaTags(string template, string title, string description, string keywords, string canonical, string jsonLd, string ogImage = "", bool noindex = false)
    {
        var result = template;

        result = Regex.Replace(result, @"<title>.*?</title>", $"<title>{EscapeHtml(title)}</title>", RegexOptions.Singleline);

        result = Regex.Replace(result,
            @"<meta name=""description"" content=""[^""]*"" />",
            $"<meta name=\"description\" content=\"{EscapeHtml(description)}\" />");

        result = Regex.Replace(result,
            @"<meta name=""keywords"" content=""[^""]*"" />",
            $"<meta name=\"keywords\" content=\"{EscapeHtml(keywords)}\" />");

        result = Regex.Replace(result,
            @"<link rel=""canonical"" href=""[^""]*"" />",
            $"<link rel=\"canonical\" href=\"{canonical}\" />");

        result = Regex.Replace(result,
            @"<meta property=""og:title"" content=""[^""]*"" />",
            $"<meta property=\"og:title\" content=\"{EscapeHtml(title)}\" />");

        result = Regex.Replace(result,
            @"<meta property=""og:description"" content=""[^""]*"" />",
            $"<meta property=\"og:description\" content=\"{EscapeHtml(description)}\" />");

        result = Regex.Replace(result,
            @"<meta property=""og:url"" content=""[^""]*"" />",
            $"<meta property=\"og:url\" content=\"{canonical}\" />");

        if (!string.IsNullOrEmpty(ogImage))
        {
            result = Regex.Replace(result,
                @"<meta property=""og:image"" content=""[^""]*"" />",
                $"<meta property=\"og:image\" content=\"{EscapeHtml(ogImage)}\" />");

            result = Regex.Replace(result,
                @"<meta name=""twitter:image"" content=""[^""]*"" />",
                $"<meta name=\"twitter:image\" content=\"{EscapeHtml(ogImage)}\" />");
        }

        result = Regex.Replace(result,
            @"<meta name=""twitter:title"" content=""[^""]*"" />",
            $"<meta name=\"twitter:title\" content=\"{EscapeHtml(title)}\" />");

        result = Regex.Replace(result,
            @"<meta name=""twitter:description"" content=""[^""]*"" />",
            $"<meta name=\"twitter:description\" content=\"{EscapeHtml(description)}\" />");

        result = Regex.Replace(result,
            @"<meta name=""twitter:url"" content=""[^""]*"" />",
            $"<meta name=\"twitter:url\" content=\"{canonical}\" />");

        if (noindex)
        {
            result = Regex.Replace(result,
                @"<meta name=""robots"" content=""[^""]*"" />",
                "<meta name=\"robots\" content=\"noindex, nofollow\" />");
        }

        result = Regex.Replace(result,
            @"(</head>)",
            $"<script type=\"application/ld+json\">{jsonLd}</script>$1",
            RegexOptions.Singleline);

        return result;
    }

    private async Task<string?> GetTemplate()
    {
        if (_indexHtmlTemplate != null) return _indexHtmlTemplate;

        var indexPath = Path.Combine(_env.WebRootPath, "index.html");
        if (!File.Exists(indexPath)) return null;

        _indexHtmlTemplate = await File.ReadAllTextAsync(indexPath);
        return _indexHtmlTemplate;
    }

    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string EscapeJson(string text) =>
        text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
}
