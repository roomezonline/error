using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private readonly IConfiguration _config;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private sealed record SeoContactData(string? Phone1, string? Phone2, string? Email, string? Address,
        string? Instagram, string? Telegram, string? Youtube);

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
        _config = configuration;
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

        var (html, statusCode) = await GetSeoHtml(path, db, IsCrawler(context));
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

    private static bool IsCrawler(HttpContext context)
    {
        var ua = context.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(ua)) return false;
        return ua.Contains("Googlebot", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("bingbot", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("Baiduspider", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("YandexBot", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("DuckDuckBot", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("Slurp", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("facebookexternalhit", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("Twitterbot", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("LinkedInBot", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("TelegramBot", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("PetalBot", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("AhrefsBot", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("MJ12bot", StringComparison.OrdinalIgnoreCase) ||
               ua.Contains("SemrushBot", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(string? html, int statusCode)> GetSeoHtml(string path, ErrorServiceDbContext db, bool isCrawler)
    {
        var cacheKey = path.ToLowerInvariant() + (isCrawler ? "|crawler" : "|min");

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
        var entity = await TryGetEntityData(cleanPath, db);

        if (TryGetDynamicMeta(cleanPath, entity, out var dynMeta, out var entityFound))
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
                jsonLd = staticMeta.jsonLdType == "Organization"
                    ? BuildOrganizationJsonLd(canonical, title, description, await LoadContactDataAsync(db))
                    : BuildStaticJsonLd(staticMeta.jsonLdType, canonical, title, description);
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

        var crawlerContent = "";
        var isPublicPage = statusCode == 200 && !cleanPath.StartsWith("admin");
        if (isPublicPage)
        {
            if (isCrawler && entityFound && entity != null)
            {
                crawlerContent = BuildPrerenderBody(cleanPath, entity);
            }

            if (string.IsNullOrEmpty(crawlerContent))
            {
                crawlerContent = $"<h1 class=\"seo-prerender\">{EscapeHtml(title)}</h1>\n" +
                                 $"<p class=\"seo-prerender-desc\">{EscapeHtml(description)}</p>";
            }
        }

        var result = ApplyMetaTags(template, title, description, keywords, canonical, jsonLd, ogImage,
            noindex: !isPublicPage, crawlerContent: crawlerContent);

        _cache[cacheKey] = (result, statusCode, DateTime.UtcNow);
        return (result, statusCode);
    }

    private string BuildPrerenderBody(string cleanPath, object entity)
    {
        var node = JsonSerializer.SerializeToNode(entity);
        if (node == null) return "";

        var sb = new StringBuilder();

        if (cleanPath.StartsWith("products/"))
        {
            var name = J(node, "Name") ?? "محصول";
            var desc = J(node, "Description") ?? "";
            var price = node["Price"]?.GetValue<decimal>() ?? 0;
            var compat = J(node, "CompatibilityInfo") ?? "";
            var symptoms = J(node, "FailureSymptoms") ?? "";
            sb.Append($"<h1 class=\"seo-prerender\">{EscapeHtml(name)}</h1>\n");
            if (!string.IsNullOrEmpty(desc))
                sb.Append(TextToParagraphs(desc, "seo-prerender-desc"));
            sb.Append("<h2 class=\"seo-prerender-h2\">قیمت و مشخصات</h2>\n<ul class=\"seo-prerender-list\">");
            sb.Append($"<li>قیمت: {price:N0} ریال</li>");
            if (!string.IsNullOrEmpty(compat)) sb.Append($"<li>سازگاری: {EscapeHtml(compat)}</li>");
            sb.Append("</ul>\n");
            if (!string.IsNullOrEmpty(symptoms))
            {
                sb.Append("<h2 class=\"seo-prerender-h2\">علائم خرابی این قطعه</h2>\n");
                sb.Append(TextToParagraphs(symptoms, "seo-prerender-desc"));
            }
            return sb.ToString();
        }

        if (cleanPath.StartsWith("news/"))
        {
            var newsTitle = J(node, "Title") ?? "خبر";
            var summary = J(node, "Summary") ?? "";
            var content = J(node, "Content") ?? "";
            sb.Append($"<h1 class=\"seo-prerender\">{EscapeHtml(newsTitle)}</h1>\n");
            if (!string.IsNullOrEmpty(summary))
                sb.Append(TextToParagraphs(summary, "seo-prerender-desc"));
            if (!string.IsNullOrEmpty(content))
                sb.Append(SanitizeHtml(content, "seo-prerender-article"));
            return sb.ToString();
        }

        if (cleanPath.StartsWith("academy/articles/"))
        {
            var artTitle = J(node, "Title") ?? "مقاله آموزشی";
            var artSummary = J(node, "Summary") ?? "";
            sb.Append($"<h1 class=\"seo-prerender\">{EscapeHtml(artTitle)}</h1>\n");
            if (!string.IsNullOrEmpty(artSummary))
                sb.Append(TextToParagraphs(artSummary, "seo-prerender-desc"));
            sb.Append(AppendBlocks(node, "seo-prerender-article"));
            return sb.ToString();
        }

        if (cleanPath.StartsWith("academy/"))
        {
            var academyParts = cleanPath.Replace("academy/", "").Split('/');
            if (academyParts.Length > 1)
            {
                var lessonTitle = J(node, "Title") ?? "قسمت آموزشی";
                var courseTitle = J(node, "CourseTitle") ?? "";
                var lessonSummary = J(node, "LessonSummary") ?? "";
                sb.Append($"<h1 class=\"seo-prerender\">{EscapeHtml(lessonTitle)}</h1>\n");
                if (!string.IsNullOrEmpty(courseTitle))
                    sb.Append($"<p class=\"seo-prerender-desc\">دوره: {EscapeHtml(courseTitle)}</p>\n");
                if (!string.IsNullOrEmpty(lessonSummary))
                    sb.Append(TextToParagraphs(lessonSummary, "seo-prerender-desc"));
                sb.Append(AppendBlocks(node, "seo-prerender-article"));
                return sb.ToString();
            }

            var courseName = J(node, "Title") ?? "دوره آموزشی";
            var courseSummary = J(node, "Summary") ?? "";
            sb.Append($"<h1 class=\"seo-prerender\">{EscapeHtml(courseName)}</h1>\n");
            if (!string.IsNullOrEmpty(courseSummary))
                sb.Append(TextToParagraphs(courseSummary, "seo-prerender-desc"));
            return sb.ToString();
        }

        if (cleanPath.StartsWith("technical/error-codes/"))
        {
            var brand = J(node, "Brand") ?? "";
            var device = J(node, "DeviceType") ?? "";
            var code = J(node, "Code") ?? "";
            var description = J(node, "Description") ?? "";
            var solution = J(node, "Solution") ?? "";
            var technicalNotes = J(node, "TechnicalNotes") ?? "";
            var models = J(node, "ModelNames") ?? "";
            sb.Append($"<h1 class=\"seo-prerender\">کد خطای {EscapeHtml(code)} {EscapeHtml(brand)} {EscapeHtml(device)}</h1>\n");
            if (!string.IsNullOrEmpty(description))
                sb.Append(TextToParagraphs(description, "seo-prerender-desc"));
            if (!string.IsNullOrEmpty(solution))
            {
                sb.Append("<h2 class=\"seo-prerender-h2\">راه‌حل تعمیر</h2>\n");
                sb.Append(TextToParagraphs(solution, "seo-prerender-desc"));
            }
            if (!string.IsNullOrEmpty(technicalNotes))
            {
                sb.Append("<h2 class=\"seo-prerender-h2\">نکات فنی</h2>\n");
                sb.Append(TextToParagraphs(technicalNotes, "seo-prerender-desc"));
            }
            if (!string.IsNullOrEmpty(models))
                sb.Append($"<p class=\"seo-prerender-desc\">مناسب برای مدل‌ها: {EscapeHtml(models)}</p>\n");
            return sb.ToString();
        }

        if (cleanPath.StartsWith("technical/sensor-finder/"))
        {
            var sensorName = J(node, "SensorName") ?? "سنسور";
            var sensorType = J(node, "SensorType") ?? "";
            var size = J(node, "SizeText") ?? "";
            var notes = J(node, "Notes") ?? "";
            sb.Append($"<h1 class=\"seo-prerender\">سنسور {EscapeHtml(sensorName)}</h1>\n");
            sb.Append("<ul class=\"seo-prerender-list\">");
            if (!string.IsNullOrEmpty(sensorType)) sb.Append($"<li>نوع: {EscapeHtml(sensorType)}</li>");
            if (!string.IsNullOrEmpty(size)) sb.Append($"<li>سایز: {EscapeHtml(size)}</li>");
            sb.Append("</ul>\n");
            if (!string.IsNullOrEmpty(notes))
                sb.Append(TextToParagraphs(notes, "seo-prerender-desc"));
            return sb.ToString();
        }

        return "";
    }

    private string AppendBlocks(JsonNode node, string wrapperClass)
    {
        var sb = new StringBuilder();
        sb.Append($"<div class=\"{wrapperClass}\">\n");
        foreach (var block in node["Blocks"]?.AsArray() ?? new JsonArray())
        {
            var blockTitle = J(block, "Title") ?? "";
            var blockContent = J(block, "Content") ?? "";
            var blockType = block?["BlockType"]?.GetValue<int>() ?? 0;
            var thumbnail = J(block, "ThumbnailUrl") ?? "";

            if (blockType == 2 && !string.IsNullOrEmpty(thumbnail))
            {
                sb.Append($"<img class=\"seo-prerender-img\" src=\"{EscapeHtml(thumbnail)}\" alt=\"{EscapeHtml(blockTitle)}\" />\n");
                continue;
            }

            if (blockType != 1) continue;

            if (!string.IsNullOrEmpty(blockTitle))
                sb.Append($"<h2 class=\"seo-prerender-h2\">{EscapeHtml(blockTitle)}</h2>\n");
            if (!string.IsNullOrEmpty(blockContent))
                sb.Append(TextToParagraphs(blockContent, "seo-prerender-desc"));
        }
        sb.Append("</div>\n");
        return sb.ToString();
    }

    private static string TextToParagraphs(string text, string className)
    {
        text = text.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        if (string.IsNullOrEmpty(text)) return "";
        var paragraphs = Regex.Split(text, @"\n\s*\n")
            .SelectMany(p => p.Split('\n'))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => $"<p class=\"{className}\">{EscapeHtml(p.Trim())}</p>");
        return string.Join("\n", paragraphs) + "\n";
    }

    private static string SanitizeHtml(string html, string wrapperClass)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        html = Regex.Replace(html, @"<script[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<iframe[\s\S]*?</iframe>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<object[\s\S]*?</object>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"\son\w+\s*=\s*""[^""]*""", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"\son\w+\s*=\s*'[^']*'", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"(href|src)\s*=\s*""\s*javascript:[^""]*""", "$1=\"#\"", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"(href|src)\s*=\s*'[^']*javascript:[^']*'", "$1='#'", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<([a-zA-Z][a-zA-Z0-9]*)[^>]*>", "<$1>");
        return $"<div class=\"{wrapperClass}\">\n{html}\n</div>\n";
    }

    private static string? J(JsonNode? node, string prop) =>
        node?[prop]?.GetValue<string>();

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
                    return await db.Products.Where(p => p.Id == pid && p.IsAvailable).Select(p => new { p.Id, p.Name, p.Description, p.Price, p.MainImageUrl, p.CompatibilityInfo, p.FailureSymptoms, p.UpdatedAt }).FirstOrDefaultAsync();

                return await db.Products.Where(p => p.Slug == seg && p.IsAvailable).Select(p => new { p.Id, p.Name, p.Description, p.Price, p.MainImageUrl, p.CompatibilityInfo, p.FailureSymptoms, p.UpdatedAt }).FirstOrDefaultAsync();
            }

            if (cleanPath.StartsWith("news/"))
            {
                var seg = cleanPath["news/".Length..].Split('/')[0];
                if (int.TryParse(seg, out var nid))
                    return await db.News.Where(n => n.Id == nid && n.IsPublished).Select(n => new { n.Id, n.Title, n.Summary, n.Content, n.ImageUrl, n.CreatedAt, n.UpdatedAt }).FirstOrDefaultAsync();

                return await db.News.Where(n => n.Slug == seg && n.IsPublished).Select(n => new { n.Id, n.Title, n.Summary, n.Content, n.ImageUrl, n.CreatedAt, n.UpdatedAt }).FirstOrDefaultAsync();
            }

            if (cleanPath.StartsWith("academy/articles/"))
            {
                var seg = cleanPath["academy/articles/".Length..].Split('/')[0];
                if (int.TryParse(seg, out var aid))
                    return await db.TrainingArticles.Where(a => a.Id == aid && a.IsPublished).Select(a => new { a.Id, a.Title, a.Summary, a.CoverImageUrl, a.UpdatedAt, Blocks = a.Blocks.OrderBy(b => b.SortOrder).Select(b => new { b.Title, b.Content, b.BlockType, b.ThumbnailUrl }).ToList() }).FirstOrDefaultAsync();

                return await db.TrainingArticles.Where(a => a.Slug == seg && a.IsPublished).Select(a => new { a.Id, a.Title, a.Summary, a.CoverImageUrl, a.UpdatedAt, Blocks = a.Blocks.OrderBy(b => b.SortOrder).Select(b => new { b.Title, b.Content, b.BlockType, b.ThumbnailUrl }).ToList() }).FirstOrDefaultAsync();
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
                        .Select(l => new { l.Id, l.Title, l.CourseId, CourseTitle = l.Course.Title, LessonSummary = l.Summary, l.UpdatedAt, Blocks = l.Blocks.OrderBy(b => b.SortOrder).Select(b => new { b.Title, b.Content, b.BlockType, b.ThumbnailUrl }).ToList() })
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
                        .Select(e => new { e.Id, e.Brand, e.DeviceType, e.Code, e.Description, e.Solution, e.TechnicalNotes, e.ModelNames, e.ImageUrl }).FirstOrDefaultAsync();
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
        if (type == "FAQPage")
        {
            return $@"{{""@context"":""https://schema.org"",""@type"":""FAQPage"",""mainEntity"":[],""url"":""{canonical}""}}";
        }

        return $@"{{""@context"":""https://schema.org"",""@type"":""{type}"",""name"":""{EscapeJson(title)}"",""description"":""{EscapeJson(description)}"",""url"":""{canonical}""}}";
    }

    private async Task<SeoContactData> LoadContactDataAsync(ErrorServiceDbContext db)
    {
        try
        {
            var f = await db.FooterSettings.FirstOrDefaultAsync(s => s.IsActive);
            if (f != null)
            {
                return new SeoContactData(f.PhoneNumber1, f.PhoneNumber2, f.Email, f.Address,
                    f.InstagramUrl, f.TelegramUrl, f.YouTubeUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SEO middleware: failed to load footer contact data");
        }
        return new SeoContactData(null, null, null, null, null, null, null);
    }

    private string BuildOrganizationJsonLd(string canonical, string title, string description, SeoContactData c)
    {
        var mapsUrl = _config.GetValue<string>("Site:GoogleMapsUrl") ?? "";
        var openingHours = _config.GetValue<string>("Site:OpeningHours") ?? "";

        var parts = new List<string>
        {
            $@"""@context"":""https://schema.org""",
            $@"""@type"":""ElectronicsStore""",
            $@"""@id"":""{_baseUrl}/#organization""",
            $@"""name"":""ارورسرویس""",
            $@"""url"":""{_baseUrl}""",
            $@"""description"":""{EscapeJson(description)}""",
            $@"""logo"":""{_baseUrl}/images/branding/logo.svg"""
        };

        var phone1 = NormalizePhone(c.Phone1);
        var phone2 = NormalizePhone(c.Phone2);
        if (!string.IsNullOrEmpty(phone1))
        {
            parts.Add($@"""telephone"":""{phone1}""");
            parts.Add($@"""contactPoint"":{{""@type"":""ContactPoint"",""telephone"":""{phone1}"",""contactType"":""customer service"",""areaServed"":""IR""}}");
        }
        if (!string.IsNullOrEmpty(c.Email)) parts.Add($@"""email"":""{EscapeJson(c.Email)}""");
        if (!string.IsNullOrEmpty(c.Address))
        {
            parts.Add($@"""address"":{{""@type"":""PostalAddress"",""streetAddress"":""{EscapeJson(c.Address)}"",""addressCountry"":""IR""}}");
        }

        if (double.TryParse(_config.GetValue<string>("Site:Latitude"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lat) &&
            double.TryParse(_config.GetValue<string>("Site:Longitude"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lng))
        {
            parts.Add($@"""geo"":{{""@type"":""GeoCoordinates"",""latitude"":{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},""longitude"":{lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
        }

        if (!string.IsNullOrEmpty(mapsUrl)) parts.Add($@"""hasMap"":""{EscapeJson(mapsUrl)}""");

        if (TryParseOpeningHours(openingHours, out var open, out var close))
        {
            parts.Add($@"""openingHoursSpecification"":[{{""@type"":""OpeningHoursSpecification"",""dayOfWeek"":[""Monday"",""Tuesday"",""Wednesday"",""Thursday"",""Friday"",""Saturday"",""Sunday""],""opens"":""{open}"",""closes"":""{close}""}}]");
        }

        var sameAs = new List<string>();
        if (!string.IsNullOrEmpty(mapsUrl)) sameAs.Add(mapsUrl);
        if (!string.IsNullOrEmpty(c.Instagram)) sameAs.Add(c.Instagram);
        if (!string.IsNullOrEmpty(c.Telegram)) sameAs.Add(c.Telegram);
        if (!string.IsNullOrEmpty(c.Youtube)) sameAs.Add(c.Youtube);
        if (sameAs.Count > 0)
        {
            parts.Add($@"""sameAs"":[{string.Join(",", sameAs.Select(s => $@"""{EscapeJson(s)}"""))}]");
        }

        var business = $"{{{string.Join(",", parts)}}}";

        var webSite = $@"{{""@context"":""https://schema.org"",""@type"":""WebSite"",""name"":""ارورسرویس"",""url"":""{_baseUrl}"",""potentialAction"":{{""@type"":""SearchAction"",""target"":""{_baseUrl}/search?q={{search_term_string}}"",""query-input"":""required name=search_term_string""}}}}";

        return $"[{business},{webSite}]";
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;
        if (digits.StartsWith("+")) return digits;
        if (digits.StartsWith("00")) return "+" + digits[2..];
        if (digits.StartsWith("0") && digits.Length > 3) return "+98" + digits[1..];
        return digits;
    }

    private static bool TryParseOpeningHours(string raw, out string open, out string close)
    {
        open = close = "";
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var sep = raw.IndexOf('-');
        if (sep <= 0) return false;
        open = raw[..sep].Trim();
        close = raw[(sep + 1)..].Trim();
        return TimeOnly.TryParse(open, out _) && TimeOnly.TryParse(close, out _);
    }

    private string BuildWebPageJsonLd(string canonical, string title, string description)
    {
        return $@"{{""@context"":""https://schema.org"",""@type"":""WebPage"",""name"":""{EscapeJson(title)}"",""description"":""{EscapeJson(description)}"",""url"":""{canonical}""}}";
    }

    private static string ApplyMetaTags(string template, string title, string description, string keywords, string canonical, string jsonLd, string ogImage = "", bool noindex = false, string? crawlerContent = null)
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

        if (!string.IsNullOrEmpty(crawlerContent))
        {
            result = Regex.Replace(result,
                @"<div id=""app""></div>",
                $"<div id=\"app\">{crawlerContent}</div>",
                RegexOptions.Singleline);
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
