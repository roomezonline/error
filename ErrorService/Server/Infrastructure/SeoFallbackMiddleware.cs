using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ErrorService.Server.Infrastructure;

public sealed class SeoFallbackMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SeoFallbackMiddleware> _logger;
    private string? _indexHtmlTemplate;
    private static readonly ConcurrentDictionary<string, (string html, int statusCode, DateTime cachedAt)> _cache = new();
    private static int _cacheVersion;
    private readonly string _baseUrl;
    private readonly IConfiguration _config;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private string? _frameworkPreloadHtml;
    private static readonly object _frameworkPreloadLock = new();

    public static void InvalidateHtmlCache() => Interlocked.Increment(ref _cacheVersion);

    private sealed record SeoContactData(string? Phone1, string? Phone2, string? Email, string? Address,
        string? Instagram, string? Telegram, string? Youtube);

    private readonly Dictionary<string, (string title, string description, string keywords, string jsonLdType)> StaticPages = new()
    {
        [""] = ("ارورسرویس | تعمیر و سرویس لوازم خانگی، کد خطا و قطعات یدکی",
                "ارورسرویس، مرجع تخصصی کدهای خطای لوازم خانگی، آموزش تعمیرات، قطعات یدکی و سرویس در منزل. بانک جامع ارور ماشین لباسشویی، یخچال، ظرفشویی و ماکروفر.",
                "کد خطای لوازم خانگی, تعمیر لوازم خانگی, قطعات یدکی, سرویس لوازم خانگی, ارور ماشین لباسشویی, ارور یخچال, تعمیرات لوازم خانگی",
                "Organization"),
        // متاتگ /products به‌صورت داده‌محور از ShopListingSeo ساخته می‌شود (دسته/صفحه).
        ["/news"] = ("اخبار و مقالات لوازم الکترونیک و تعمیر لوازم خانگی | ارورسرویس",
                     "آخرین اخبار، مقالات و راهنمای خرید لوازم الکترونیکی، قطعات الکترونیک، تجهیزات ابزار دقیق و نکات فنی تعمیر لوازم خانگی و بردهای الکترونیکی",
                     "اخبار الکترونیک, مقالات لوازم الکترونیک, راهنمای خرید قطعات, تعمیر لوازم خانگی, تعمیر بردها, ابزار دقیق",
                     "CollectionPage"),
        ["/tips"] = ("ترفندهای تعمیر و نگهداری لوازم خانگی و الکترونیک | ارورسرویس",
                     "ترفندهای کاربردی، نکات نگهداری و راهکارهای سریع برای تعمیر و افزایش عمر لوازم خانگی و تجهیزات الکترونیکی.",
                     "ترفند لوازم خانگی, نکات نگهداری, تعمیر سریع, راهکار عملی, ارورسرویس",
                     "CollectionPage"),
        ["/announcements"] = ("اطلاعیه‌ها و اخبار رسمی ارورسرویس",
                              "اطلاعیه‌های رسمی ارورسرویس درباره خدمات، تغییرات سرویس‌ها، رویدادها و شرایط فروشگاه.",
                              "اطلاعیه ارورسرویس, اخبار رسمی, اطلاع‌رسانی خدمات, ارورسرویس",
                              "CollectionPage"),
        ["/academy"] = ("آکادمی آموزش تعمیر لوازم خانگی و الکترونیک | ارورسرویس",
                        "دوره‌های گام‌به‌گام و آموزش‌های تخصصی تعمیر لوازم خانگی، بردهای الکترونیک و عیب‌یابی؛ دسته‌بندی‌شده بر اساس نوع دستگاه با جستجوی سریع.",
                        "آموزش تعمیر لوازم خانگی, دوره تعمیرات, تعمیر بردهای الکترونیک, آموزش تعمیر یخچال, آموزش تعمیر لباسشویی, عیب‌یابی لوازم خانگی",
                        "CollectionPage"),
        ["/technical/error-codes"] = ("بانک کدهای خطا | عیب‌یابی هوشمند لوازم خانگی",
                                      "جستجو و مشاهده لیست کامل کدهای خطای یخچال، ماشین لباسشویی، ظرفشویی و پکیج برندهای سامسونگ، ال‌جی، بوش و...",
                                      "کد خطا، ارور یخچال، ارور لباسشویی، لیست کدهای خطا، تعمیرات لوازم خانگی",
                                      "CollectionPage"),
        ["/technical/sensor-finder"] = ("سنسور یاب هوشمند | ارورسرویس",
                                       "سنسور یاب هوشمند ارورسرویس، جستجو و شناسایی سنسورهای لوازم خانگی با مشخصات فنی کامل",
                                       "سنسور لوازم خانگی, سنسور یاب, شناسایی سنسور, سنسور ماشین لباسشویی, سنسور یخچال",
                                       "CollectionPage"),
        ["/technical/calculators"] = ("ماشین‌حساب تعمیرکاران: ۱۳ ابزار محاسبه برق و الکترونیک | ارورسرویس",
                                      "۱۳ ماشین‌حساب رایگان برای تعمیرکاران و تکنسین‌ها: کد رنگ مقاومت، قانون اهم، مقاومت LED، افت ولتاژ کابل، جریان و خازن موتور، مصرف برق و عمر باتری. بدون نصب و مناسب موبایل.",
                                      "ماشین حساب تعمیرات, محاسبه قانون اهم, کد رنگ مقاومت, افت ولتاژ کابل, محاسبه سطح مقطع سیم, جریان موتور, خازن موتور تک فاز, محاسبه مصرف برق, ابزار تعمیرکار",
                                      "WebApplication"),
        ["/technical/consultation"] = ("سیستم مشاوره فنی تعمیرات لوازم خانگی | ارورسرویس",
                                      "ارسال سوال فنی و دریافت پاسخ از متخصصین تعمیرات لوازم خانگی. مشاوره رایگان عیب‌یابی",
                                      "مشاوره فنی, مشاوره تعمیرات, سوال فنی لوازم خانگی",
                                      "ContactPage"),
        ["/contact"] = ("تماس با ارورسرویس | مشاوره لوازم الکترونیک و درخواست تعمیر لوازم خانگی",
                        "تماس با ارورسرویس برای مشاوره خرید لوازم الکترونیکی و تجهیزات ابزار دقیق، درخواست تعمیر لوازم خانگی و بردهای الکترونیکی. پاسخگویی سریع",
                        "تماس ارورسرویس, شماره تماس, تعمیر لوازم خانگی, تعمیر بردهای الکترونیک, خرید لوازم الکترونیک",
                        "ContactPage"),
        ["/about"] = ("درباره ارورسرویس | فروشگاه قطعات، تعمیرات و آموزش لوازم خانگی",
                      "ارورسرویس؛ بانک کدهای خطا، فروشگاه قطعات یدکی و خدمات تعمیر تخصصی لوازم خانگی با تیمی از کارشناسان فنی.",
                      "درباره ارورسرویس, تعمیر لوازم خانگی, کدهای خطا, فروشگاه قطعات الکترونیک, آموزش تعمیرات",
                      "AboutPage"),
        ["/faq"] = ("سوالات متداول | خرید لوازم الکترونیک و تعمیر لوازم خانگی | ارورسرویس",
                    "پاسخ به سوالات متداول درباره خرید لوازم الکترونیکی، شرایط ارسال، ضمانت اصالت کالا، تعمیر لوازم خانگی، مرجوعی و پشتیبانی ارورسرویس",
                    "سوالات متداول, FAQ, راهنمای خرید, لوازم الکترونیک, تعمیر لوازم خانگی, ضمانت اصالت",
                    "WebPage"),
        ["/privacy"] = ("حریم خصوصی | ارورسرویس",
                        "قوانین حریم خصوصی ارورسرویس - نحوه جمع‌آوری و استفاده از اطلاعات کاربران",
                        "حریم خصوصی, قوانین سایت, ارورسرویس",
                        "WebPage"),
        ["/terms"] = ("قوانین و مقررات | ارورسرویس",
                      "قوانین و مقررات استفاده از خدمات ارورسرویس",
                      "قوانین, مقررات, شرایط استفاده, ارورسرویس",
                      "WebPage"),
        ["/trust/portfolio"] = ("نمونه کارهای تعمیر لوازم خانگی و بردهای الکترونیک | ارورسرویس",
                                "گزارش تصویری از تعمیرات تخصصی لوازم خانگی، بردهای الکترونیکی و تجهیزات صنعتی انجام شده در کارگاه ارورسرویس",
                                "نمونه کار تعمیرات, تعمیر لوازم خانگی, تعمیر بردهای الکترونیک, نمونه کار تعمیر یخچال, تعمیر لباسشویی",
                                "CollectionPage"),
        ["/admission/repair"] = ("درخواست تعمیر آنلاین لوازم خانگی | ارورسرویس",
                                 "ثبت درخواست تعمیر آنلاین لوازم خانگی با مشاوره رایگان. تعمیر ماشین لباسشویی، یخچال، ظرفشویی و ماکروفر",
                                 "تعمیر آنلاین, درخواست تعمیر لوازم خانگی, تعمیر در منزل",
                                 "WebPage"),
        ["/admission/expertise"] = ("درخواست کارشناسی آنلاین لوازم خانگی | ارورسرویس",
                                    "ثبت درخواست کارشناسی و عیب‌یابی آنلاین لوازم خانگی. بررسی تخصصی و اعلام هزینه تعمیر",
                                    "کارشناسی لوازم خانگی, عیب‌یابی آنلاین, بررسی تعمیرات",
                                    "WebPage"),
        ["/commitment"] = ("تعهدنامه دیجیتال | ارورسرویس",
                           "تعهدنامه و اصول ارائه خدمات تعمیرات ارورسرویس",
                           "تعهدنامه ارورسرویس, اصول خدمات تعمیرات, ضمانت خدمات",
                           "WebPage"),
        ["/workshops"] = ("همکاران ارورسرویس | تعمیرکاران لوازم خانگی در شهرهای مختلف",
                          "فهرست همکاران و تعمیرکاران همکار ارورسرویس در سراسر کشور؛ آدرس، شماره تماس و پروفایل هر مجموعه را مشاهده کنید.",
                          "همکاران ارورسرویس, تعمیرکار لوازم خانگی, تعمیرگاه لوازم خانگی, سرویس در منزل, تعمیر ماشین لباسشویی, تعمیر یخچال",
                          "CollectionPage"),
    };

    public SeoFallbackMiddleware(RequestDelegate next, IWebHostEnvironment env, ILogger<SeoFallbackMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _env = env;
        _logger = logger;
        _config = configuration;
        _baseUrl = (configuration.GetValue<string>("Site:BaseUrl") ?? "https://errorservice.ir").TrimEnd('/');
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

        // Permanent redirect for non-lowercase path variants (e.g. .../IE -> .../ie)
        // so Google only sees one canonical URL instead of "Alternate page".
        var lowerPath = path.ToLowerInvariant();
        if (!string.Equals(path, lowerPath, StringComparison.Ordinal))
        {
            var qs = context.Request.QueryString.Value ?? "";
            var encoded = _baseUrl + string.Join("/", lowerPath.Split('/').Select(Uri.EscapeDataString));
            context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
            context.Response.Headers.Location = encoded + qs;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("Moved Permanently");
            return;
        }

        var db = context.RequestServices.GetRequiredService<ErrorServiceDbContext>();
        var cleanPath = path.TrimStart('/').ToLowerInvariant().TrimEnd('/');

        // Permanent redirect for legacy numeric entity URLs -> keyword-rich slug URLs
        var redirect = await TryResolveRedirect(cleanPath, db);
        if (redirect != null)
        {
            var redirectQs = context.Request.QueryString.Value ?? "";
            context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
            context.Response.Headers.Location = _baseUrl + string.Join("/", redirect.Split('/').Select(segment => Uri.EscapeDataString(segment))) + redirectQs;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync("Moved Permanently");
            return;
        }

        // فقط فهرست فروشگاه پارامتر معنادار (دسته/صفحه) در canonical دارد؛ بقیه
        // صفحات با پارامترهای اضافی (مثل ?page= یا فیلترهای جستجو) یک URL تکراری نسازند.
        var isShopListing = string.Equals(path.Trim('/'), "products", StringComparison.OrdinalIgnoreCase);
        var seoQuery = isShopListing ? BuildSeoQuery(context.Request.QueryString.Value) : "";
        var (html, statusCode) = await GetSeoHtml(path, seoQuery, db, IsCrawler(context));
        if (html != null)
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsync(html);
            return;
        }

        await _next(context);
    }

    // فقط دسته و شماره صفحه در canonical و کلید کش می‌آیند؛ مرتب‌سازی، بازه قیمت،
    // فیلترهای موجودی، جستجو و پارامترهای ردیابی (utm و...) حذف می‌شوند تا
    // ده‌ها URL تقریباً یکسان با یک کپشن در گوگل ایندکس نشوند.
    private static readonly string[] SeoQueryKeys = ["categoryId", "page"];

    private static string BuildSeoQuery(string? rawQuery)
    {
        if (string.IsNullOrWhiteSpace(rawQuery)) return "";

        string? categoryId = null;
        string? page = null;
        foreach (var pair in rawQuery.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(eq >= 0 ? pair[..eq] : pair).Trim();
            var value = Uri.UnescapeDataString(eq >= 0 ? pair[(eq + 1)..] : "").Trim();

            if (!SeoQueryKeys.Contains(key, StringComparer.OrdinalIgnoreCase) || value.Length == 0) continue;

            if (string.Equals(key, "page", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(value, out var pageNumber) && pageNumber > 1)
            {
                page = pageNumber.ToString();
            }
            else if (string.Equals(key, "categoryId", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(value, out var cid) && cid > 0)
            {
                categoryId = cid.ToString();
            }
        }

        // ترتیب ثابت تا کلاینت و خزنده یک canonical یکسان بسازند.
        var parts = new List<string>();
        if (categoryId != null) parts.Add($"categoryId={categoryId}");
        if (page != null) parts.Add($"page={page}");
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    private static (int? CategoryId, int Page) ParseShopListingQuery(string seoQuery)
    {
        int? categoryId = null;
        var page = 1;
        if (seoQuery.Length == 0) return (categoryId, page);

        foreach (var pair in seoQuery.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(eq >= 0 ? pair[..eq] : pair);
            var value = Uri.UnescapeDataString(eq >= 0 ? pair[(eq + 1)..] : "");

            if (key.Equals("categoryId", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(value, out var parsedCategoryId) && parsedCategoryId > 0)
                categoryId = parsedCategoryId;
            else if (key.Equals("page", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(value, out var parsedPage) && parsedPage > 1)
                page = parsedPage;
        }

        return (categoryId, page);
    }

    private static bool IsPrivateAppPage(string cleanPath) =>
        cleanPath == "cart" ||
        cleanPath == "checkout" ||
        cleanPath == "wishlist" ||
        cleanPath == "compare" ||
        cleanPath == "order-tracking" ||
        cleanPath == "admission/track" ||
        cleanPath == "technical/error-codes/saved" ||
        cleanPath == "search" ||
        cleanPath == "my-orders" || cleanPath.StartsWith("my-orders/") ||
        cleanPath == "monitoring/shared" || cleanPath.StartsWith("monitoring/shared/") ||
        cleanPath.StartsWith("payment/offline/");

    private static (string title, string description, string keywords) GetPrivateAppMeta(string cleanPath)
    {
        if (cleanPath == "cart")
            return ("سبد خرید | ارورسرویس",
                    "مشاهده و ویرایش سبد خرید در فروشگاه ارورسرویس",
                    "سبد خرید, ارورسرویس");
        if (cleanPath == "checkout")
            return ("تسویه حساب | ارورسرویس",
                    "تکمیل اطلاعات و ثبت سفارش در فروشگاه ارورسرویس",
                    "تسویه حساب, ثبت سفارش, ارورسرویس");
        if (cleanPath == "wishlist")
            return ("علاقه‌مندی‌ها | ارورسرویس",
                    "فهرست محصولات مورد علاقه شما در ارورسرویس",
                    "علاقه مندی, لیست خرید, ارورسرویس");
        if (cleanPath == "compare")
            return ("مقایسه محصولات | ارورسرویس",
                    "محصولات انتخابی خود را از نظر قیمت، موجودی و مشخصات فنی کنار هم مقایسه کنید.",
                    "مقایسه محصولات, مقایسه قیمت, مشخصات فنی, ارورسرویس");
        if (cleanPath == "admission/track")
            return ("پیگیری درخواست پذیرش | ارورسرویس",
                    "پیگیری وضعیت درخواست تعمیر یا کارشناسی ثبت‌شده در ارورسرویس",
                    "پیگیری درخواست, وضعیت تعمیر, ارورسرویس");
        if (cleanPath == "technical/error-codes/saved")
            return ("کدهای خطای ذخیره‌شده | ارورسرویس",
                    "فهرست کدهای خطایی که برای مطالعه بعدی در این مرورگر ذخیره کرده‌اید.",
                    "کد خطای ذخیره شده, کدهای خطا, ارورسرویس");
        if (cleanPath == "order-tracking")
            return ("پیگیری سفارش | ارورسرویس",
                    "پیگیری وضعیت سفارش با شماره سفارش و شماره تلفن",
                    "پیگیری سفارش, وضعیت سفارش, ارورسرویس");
        if (cleanPath == "search")
            return ("جستجو | ارورسرویس",
                    "جستجو در محصولات، مقالات، اخبار و کدهای خطای ارورسرویس",
                    "جستجو, محصولات, مقالات, ارورسرویس");
        if (cleanPath == "monitoring/shared" || cleanPath.StartsWith("monitoring/shared/"))
            return ("مشاهده پایش | ارورسرویس",
                    "مشاهده گزارش پایش اشتراک‌گذاری‌شده در ارورسرویس",
                    "پایش, گزارش, ارورسرویس");
        if (cleanPath.StartsWith("my-orders/"))
            return ("جزئیات سفارش | ارورسرویس",
                    "مشاهده جزئیات و وضعیت سفارش شما در ارورسرویس",
                    "جزئیات سفارش, وضعیت سفارش, ارورسرویس");
        if (cleanPath == "my-orders")
            return ("سفارش‌های من | ارورسرویس",
                    "مشاهده تاریخچه و جزئیات سفارش‌های شما در ارورسرویس",
                    "سفارش های من, تاریخچه سفارش, ارورسرویس");
        return ("پرداخت سفارش | ارورسرویس",
                "ثبت رسید و پیگیری پرداخت سفارش در ارورسرویس",
                "پرداخت, رسید, ارورسرویس");
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

    private async Task<(string? html, int statusCode)> GetSeoHtml(string path, string seoQuery, ErrorServiceDbContext db, bool isCrawler)
    {
        var cacheKey = path.ToLowerInvariant() + seoQuery + "|" + _cacheVersion + (isCrawler ? "|crawler" : "|min");

        if (_cache.TryGetValue(cacheKey, out var cached) && (DateTime.UtcNow - cached.cachedAt) < CacheDuration)
            return (cached.html, cached.statusCode);

        var template = await GetTemplate();
        if (template == null) return (null, 200);

        var cleanPath = path.TrimStart('/').ToLowerInvariant();

        // Normalize: remove trailing slash
        if (cleanPath.EndsWith('/'))
            cleanPath = cleanPath.TrimEnd('/');

        // /help/faq مسیر جایگزین صفحه سوالات متداول است؛ برای جلوگیری از محتوای
        // تکراری، متاتگ و canonical هر دو مسیر به /faq اشاره می‌کنند.
        if (cleanPath == "help/faq") cleanPath = "faq";

        var canonicalPath = string.Join("/", cleanPath.Split('/').Select(Uri.EscapeDataString));
        var canonical = $"{_baseUrl}/{(canonicalPath == "" ? "" : canonicalPath)}{seoQuery}";

        string title, description, keywords, jsonLd;
        string ogImage = "";
        string ogType = "website";
        var statusCode = 200;
        var entity = await TryGetEntityData(cleanPath, db);
        var entityFound = entity != null;
        var isPrivateAppPage = IsPrivateAppPage(cleanPath);

        if (cleanPath == "admin" || cleanPath.StartsWith("admin/"))
        {
            title = "پنل مدیریت | ارورسرویس";
            description = "پنل مدیریت ارورسرویس";
            keywords = "پنل مدیریت, ارورسرویس";
            jsonLd = BuildWebPageJsonLd(canonical, title, description);
        }
        else if (cleanPath == "auth" || cleanPath.StartsWith("auth/"))
        {
            title = "حساب کاربری | ارورسرویس";
            description = "ورود و ثبت‌نام در ارورسرویس";
            keywords = "ورود, ثبت نام, ارورسرویس";
            jsonLd = BuildWebPageJsonLd(canonical, title, description);
        }
        else if (cleanPath == "profile" || cleanPath.StartsWith("profile/"))
        {
            title = "پروفایل کاربری | ارورسرویس";
            description = "پروفایل کاربری ارورسرویس";
            keywords = "پروفایل, حساب کاربری, ارورسرویس";
            jsonLd = BuildWebPageJsonLd(canonical, title, description);
        }
        else if (cleanPath == "payment/result")
        {
            title = "نتیجه پرداخت | ارورسرویس";
            description = "نتیجه پرداخت سفارش در ارورسرویس";
            keywords = "پرداخت, نتیجه پرداخت, ارورسرویس";
            jsonLd = BuildWebPageJsonLd(canonical, title, description);
        }
        else if (isPrivateAppPage)
        {
            (title, description, keywords) = GetPrivateAppMeta(cleanPath);
            jsonLd = BuildWebPageJsonLd(canonical, title, description);
        }
        else if (TryGetDynamicMeta(cleanPath, entity, out var dynMeta, out entityFound))
        {
            (title, description, keywords, jsonLd, ogImage, ogType) = dynMeta;
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
            if (staticKey == "")
            {
                // متاتگ‌های صفحه اصلی از تنظیمات ادمین خوانده می‌شوند تا با
                // خروجی کلاینت (SiteSettingsService) یکی باشند.
                var site = await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
                (title, description, keywords, _) = StaticPages[""];
                if (!string.IsNullOrWhiteSpace(site?.SeoTitle)) title = site!.SeoTitle!.Trim();
                if (!string.IsNullOrWhiteSpace(site?.SeoDescription)) description = site!.SeoDescription!.Trim();
                if (!string.IsNullOrWhiteSpace(site?.SeoKeywords)) keywords = site!.SeoKeywords!.Trim();
                if (!string.IsNullOrWhiteSpace(site?.DefaultOgImage)) ogImage = AbsUrl(site.DefaultOgImage) ?? "";
                jsonLd = BuildOrganizationJsonLd(canonical, title, description, await LoadContactDataAsync(db));
            }
            else if (staticKey == "/products")
            {
                var (categoryId, page) = ParseShopListingQuery(seoQuery);
                var category = categoryId is int cid
                    ? await db.Categories.AsNoTracking()
                        .Where(c => c.Id == cid)
                        .Select(c => new { c.Name, c.SeoTitle, c.SeoDescription })
                        .FirstOrDefaultAsync()
                    : null;
                var listing = ShopListingSeo.Build(category?.Name, page, category?.SeoTitle, category?.SeoDescription);
                (title, description, keywords) = (listing.Title, listing.Description, listing.Keywords);
                jsonLd = BuildStaticJsonLd("CollectionPage", canonical, title, description);
            }
            else if (staticKey == "/about")
            {
                // متاتگ صفحه درباره ما از محتوای قابل ویرایش ادمین خوانده می‌شود.
                var about = await db.AboutPageContents.AsNoTracking()
                    .Select(a => new { a.SeoTitle, a.SeoDescription, a.SeoKeywords, a.HeroImageUrl })
                    .FirstOrDefaultAsync();
                (title, description, keywords, _) = StaticPages["/about"];
                if (!string.IsNullOrWhiteSpace(about?.SeoTitle)) title = about!.SeoTitle!.Trim();
                if (!string.IsNullOrWhiteSpace(about?.SeoDescription)) description = about!.SeoDescription!.Trim();
                if (!string.IsNullOrWhiteSpace(about?.SeoKeywords)) keywords = about!.SeoKeywords!.Trim();
                if (!string.IsNullOrWhiteSpace(about?.HeroImageUrl)) ogImage = AbsUrl(about.HeroImageUrl) ?? "";
                jsonLd = BuildStaticJsonLd("AboutPage", canonical, title, description);
            }
            else if (TryGetContentListingSegment(cleanPath, out var listingType, out var listingKind, out var listingSlug))
            {
                var settingsEntity = await db.ContentSettings.AsNoTracking().FirstOrDefaultAsync();
                var contentSettings = settingsEntity == null ? new ContentSettingsDto() : ContentSettingsMapper.ToDto(settingsEntity);
                var typePlural = ContentTypeCatalog.PluralLabel(listingType);

                if (!contentSettings.IsTypeEnabled(listingType))
                {
                    statusCode = 404;
                    title = "صفحه مورد نظر یافت نشد | ارورسرویس";
                    description = "متاسفانه صفحه مورد نظر شما وجود ندارد یا حذف شده است.";
                    keywords = "404, صفحه یافت نشد, ارورسرویس";
                    jsonLd = BuildWebPageJsonLd(canonical, title, description);
                }
                else
                {
                    var label = listingKind == "category"
                        ? await db.ContentCategories.AsNoTracking()
                            .Where(c => c.IsActive && c.Slug != null && c.Slug.ToLower() == listingSlug)
                            .Select(c => c.Title)
                            .FirstOrDefaultAsync()
                        : listingSlug.Replace('-', ' ');

                    if (string.IsNullOrWhiteSpace(label) && listingKind == "category")
                    {
                        statusCode = 404;
                        title = "دسته مورد نظر یافت نشد | ارورسرویس";
                        description = "متاسفانه دسته مورد نظر شما وجود ندارد یا حذف شده است.";
                        keywords = "404, دسته یافت نشد, ارورسرویس";
                        jsonLd = BuildWebPageJsonLd(canonical, title, description);
                    }
                    else
                    {
                        label = string.IsNullOrWhiteSpace(label) ? typePlural : label.Trim();
                        title = listingKind == "category"
                            ? $"{label} | {typePlural} | ارورسرویس"
                            : $"برچسب {label} | {typePlural} | ارورسرویس";
                        description = $"آخرین {typePlural} با موضوع {label} در ارورسرویس؛ مطالب تخصصی، راهنما و اخبار مرتبط.";
                        keywords = $"{label}, {typePlural}, ارورسرویس";
                        jsonLd = BuildStaticJsonLd("CollectionPage", canonical, title, description);
                    }
                }
            }
            else if (StaticPages.TryGetValue(staticKey, out var staticMeta))
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

        // LCP optimization: on the home page, tell the browser to start downloading
        // the first hero slide image immediately (instead of after the WASM boot + API call).
        // Also used by the home prerender body (alt-bearing <img> for crawlers).
        string? preloadImage = null;
        string? preloadImageMobile = null;
        string? preloadSlideTitle = null;
        if (cleanPath == "")
        {
            try
            {
                var firstSlide = await db.SliderItems
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.SortOrder)
                    .ThenByDescending(x => x.Id)
                    .Select(x => new { x.ImageUrl, x.MobileImageUrl, x.Title })
                    .FirstOrDefaultAsync();

                if (firstSlide != null && !string.IsNullOrWhiteSpace(firstSlide.ImageUrl))
                {
                    preloadImage = firstSlide.ImageUrl;
                    preloadImageMobile = string.IsNullOrWhiteSpace(firstSlide.MobileImageUrl) ? null : firstSlide.MobileImageUrl;
                    preloadSlideTitle = string.IsNullOrWhiteSpace(firstSlide.Title) ? null : firstSlide.Title;
                }
            }
            catch
            {
                // Preload is best-effort; never fail the page because of it.
            }
        }

        var crawlerContent = "";
        var isPublicPage = statusCode == 200 && !isPrivateAppPage && !cleanPath.StartsWith("admin") && !cleanPath.StartsWith("auth") && !cleanPath.StartsWith("profile")
            && !string.Equals(cleanPath, "payment/result", StringComparison.Ordinal);
        if (isPublicPage)
        {
            if (isCrawler)
            {
                if (entityFound && entity != null)
                {
                    crawlerContent = await BuildPrerenderBodyAsync(cleanPath, entity, db);
                }

                if (cleanPath == "technical/error-codes")
                {
                    crawlerContent = await BuildErrorCodeHubContent(db);
                }

                if (cleanPath == "workshops")
                {
                    crawlerContent = await BuildWorkshopsHubContent(db);
                }

                if (cleanPath == "technical/calculators")
                {
                    crawlerContent = await BuildCalculatorsContentAsync(db);
                }

                if (cleanPath == "academy")
                {
                    crawlerContent = await BuildAcademyHubContentAsync(db);
                }

                if (string.IsNullOrEmpty(crawlerContent))
                {
                    if (cleanPath == "")
                    {
                        crawlerContent = BuildHomePrerenderContent(title, description, preloadImage, preloadSlideTitle);
                    }
                    else
                    {
                        crawlerContent = $"<h1 class=\"seo-prerender\">{EscapeHtml(title)}</h1>\n" +
                                         $"<p class=\"seo-prerender-desc\">{EscapeHtml(description)}</p>";
                    }
                }
            }
            else if (cleanPath == "")
            {
                crawlerContent = BuildHomeShell(title, description, preloadImage, preloadImageMobile, preloadSlideTitle);
            }
        }

        // صفحه ابزارهای محاسباتی: ساختار داده غنی‌تر (WebApplication + FAQ + Breadcrumb)
        if (cleanPath == "technical/calculators" && statusCode == 200)
        {
            jsonLd = BuildCalculatorsJsonLd(canonical);
        }

        // صفحه آکادمی: CollectionPage + Breadcrumb + ItemList محتوا
        if (cleanPath == "academy" && statusCode == 200)
        {
            jsonLd = await BuildAcademyJsonLdAsync(db, canonical);
        }

        // Inject product review structured data for crawlers (rich results)
        if (isCrawler && cleanPath.StartsWith("products/") && entityFound && entity != null)
        {
            jsonLd = await EnrichProductJsonLdWithReviewsAsync(jsonLd, entity, db);
        }

        var result = ApplyMetaTags(template, title, description, keywords, canonical, jsonLd, ogImage,
            noindex: !isPublicPage, crawlerContent: crawlerContent,
            preloadImage: preloadImage, preloadImageMobile: preloadImageMobile, ogType: ogType,
            headExtra: GetFrameworkPreloadLinks());

        _cache[cacheKey] = (result, statusCode, DateTime.UtcNow);
        return (result, statusCode);
    }

    private async Task<string> BuildErrorCodeHubContent(ErrorServiceDbContext db)
    {
        var errors = await db.ErrorCodes
            .OrderByDescending(e => e.Id)
            .Take(24)
            .Select(e => new { e.Brand, e.DeviceType, e.Code, e.Description })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.Append("<h1 class=\"seo-prerender\">بانک کدهای خطا</h1>\n");
        sb.Append("<p class=\"seo-prerender-desc\">جستجو و مشاهده کدهای خطای لوازم خانگی بر اساس برند، نوع دستگاه و کد خطا.</p>\n");
        sb.Append("<h2 class=\"seo-prerender-h2\">کدهای خطای پرکاربرد</h2>\n<ul class=\"seo-prerender-list\">");

        foreach (var error in errors)
        {
            var brand = Uri.EscapeDataString(error.Brand.Trim().ToLowerInvariant());
            var device = Uri.EscapeDataString(error.DeviceType.Trim().ToLowerInvariant());
            var code = Uri.EscapeDataString(error.Code.Trim().ToLowerInvariant());
            var url = $"{_baseUrl}/technical/error-codes/{brand}/{device}/{code}";
            var label = $"کد خطای {error.Code} {error.Brand} {error.DeviceType}";
            if (!string.IsNullOrWhiteSpace(error.Description))
                label += $" - {error.Description}";
            sb.Append($"<li><a href=\"{EscapeHtml(url)}\">{EscapeHtml(label)}</a></li>");
        }

        sb.Append("</ul>\n");
        return sb.ToString();
    }

    private async Task<string> BuildWorkshopsHubContent(ErrorServiceDbContext db)
    {
        var list = await db.Workshops
            .AsNoTracking()
            .Where(w => !w.IsDeleted)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new { w.Id, w.WorkshopName, CityName = w.City.Name, ProvinceName = w.Province.Name })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.Append("<h1 class=\"seo-prerender\">همکاران ما</h1>\n");
        sb.Append("<p class=\"seo-prerender-desc\">فهرست همکاران و تعمیرکاران همکار ارورسرویس در سراسر کشور؛ آدرس، شماره تماس و پروفایل هر مجموعه را مشاهده کنید.</p>\n");
        sb.Append("<ul class=\"seo-prerender-list\">");

        foreach (var w in list)
        {
            var place = string.Join("، ", new[] { w.CityName, w.ProvinceName }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .Distinct());
            var label = place.Length > 0 ? $"{w.WorkshopName} - {place}" : w.WorkshopName;
            sb.Append($"<li><a href=\"{EscapeHtml($"{_baseUrl}/workshops/{w.Id}")}\">{EscapeHtml(label)}</a></li>");
        }

        sb.Append("</ul>\n");
        return sb.ToString();
    }

    private async Task<string> BuildCalculatorsContentAsync(ErrorServiceDbContext db)
    {
        HashSet<string> disabled;
        try
        {
            var raw = await db.SiteSettings.Select(s => s.CalculatorsDisabled).FirstOrDefaultAsync();
            disabled = CalculatorCatalog.ParseDisabled(raw);
        }
        catch
        {
            disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var sb = new StringBuilder();
        sb.Append("<h1 class=\"seo-prerender\">ابزارهای محاسباتی تعمیرکاران</h1>\n");
        sb.Append("<p class=\"seo-prerender-desc\">۱۳ ماشین‌حساب رایگان و بدون نصب برای برق، الکترونیک و لوازم خانگی: کد رنگ مقاومت، قانون اهم، مقاومت LED، افت ولتاژ کابل، جریان و خازن موتور، مصرف برق و عمر باتری. ورودی‌ها در مرورگر ذخیره می‌شوند.</p>\n");

        foreach (var category in CalculatorCatalog.Categories)
        {
            var tools = CalculatorCatalog.InCategory(category.Key)
                .Where(t => !disabled.Contains(t.Key))
                .ToList();

            if (tools.Count == 0)
                continue;

            sb.Append($"<h2 class=\"seo-prerender-h2\">{EscapeHtml(category.Title)}</h2>\n");
            sb.Append("<ul class=\"seo-prerender-list\">");

            foreach (var tool in tools)
            {
                var url = $"{_baseUrl}/technical/calculators#{tool.Key}";
                sb.Append($"<li><a href=\"{EscapeHtml(url)}\">{EscapeHtml(tool.Title)}</a> — {EscapeHtml(tool.ShortDescription)}</li>");
            }

            sb.Append("</ul>\n");
        }

        sb.Append("<h2 class=\"seo-prerender-h2\">سوالات متداول ابزارهای محاسباتی</h2>\n");
        foreach (var item in CalculatorCatalog.Faq)
        {
            sb.Append($"<h3 class=\"seo-prerender-h3\">{EscapeHtml(item.Question)}</h3>\n");
            sb.Append($"<p>{EscapeHtml(item.Answer)}</p>\n");
        }

        return sb.ToString();
    }

    private string BuildCalculatorsJsonLd(string canonical)
    {
        var faqEntities = CalculatorCatalog.Faq
            .Select(item => new Dictionary<string, object?>
            {
                ["@type"] = "Question",
                ["name"] = item.Question,
                ["acceptedAnswer"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Answer",
                    ["text"] = item.Answer
                }
            })
            .ToArray();

        var graph = new object[]
        {
            new Dictionary<string, object?>
            {
                ["@type"] = "WebApplication",
                ["name"] = "ماشین‌حساب تعمیرکاران ارورسرویس",
                ["url"] = canonical,
                ["applicationCategory"] = "UtilitiesApplication",
                ["operatingSystem"] = "Web",
                ["inLanguage"] = "fa-IR",
                ["offers"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Offer",
                    ["price"] = "0",
                    ["priceCurrency"] = "IRR"
                }
            },
            new Dictionary<string, object?>
            {
                ["@type"] = "BreadcrumbList",
                ["itemListElement"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["@type"] = "ListItem",
                        ["position"] = 1,
                        ["name"] = "خانه",
                        ["item"] = $"{_baseUrl}/"
                    },
                    new Dictionary<string, object?>
                    {
                        ["@type"] = "ListItem",
                        ["position"] = 2,
                        ["name"] = "ابزارهای محاسباتی",
                        ["item"] = canonical
                    }
                }
            },
            new Dictionary<string, object?>
            {
                ["@type"] = "FAQPage",
                ["mainEntity"] = faqEntities
            }
        };

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = graph
        });
    }

    private async Task<string> BuildAcademyHubContentAsync(ErrorServiceDbContext db)
    {
        var sb = new StringBuilder();
        sb.Append("<h1 class=\"seo-prerender\">آکادمی آموزش تعمیر لوازم خانگی و الکترونیک</h1>\n");
        sb.Append("<p class=\"seo-prerender-desc\">دوره‌ها و آموزش‌های تخصصی تعمیر لوازم خانگی، بردهای الکترونیک و عیب‌یابی؛ دسته‌بندی‌شده بر اساس نوع دستگاه تا سریع به محتوای موردنظر برسید.</p>\n");

        try
        {
            var courses = await db.TrainingCourses
                .AsNoTracking()
                .Where(c => c.IsPublished)
                .OrderBy(c => c.SortOrder)
                .ThenByDescending(c => c.CreatedAt)
                .Take(100)
                .Select(c => new { c.Id, c.Title, c.Slug, c.Category })
                .ToListAsync();

            var articles = await db.TrainingArticles
                .AsNoTracking()
                .Where(a => a.IsPublished)
                .OrderByDescending(a => a.CreatedAt)
                .Take(100)
                .Select(a => new { a.Id, a.Title, a.Slug, a.Category })
                .ToListAsync();

            if (courses.Count > 0)
            {
                sb.Append("<h2 class=\"seo-prerender-h2\">دوره‌های آموزشی</h2>\n<ul class=\"seo-prerender-list\">");
                foreach (var c in courses)
                {
                    var label = string.IsNullOrWhiteSpace(c.Category) ? c.Title : $"{c.Title} — {c.Category}";
                    sb.Append($"<li><a href=\"{EscapeHtml(AcademyCourseUrl(c.Id, c.Slug))}\">{EscapeHtml(label)}</a></li>");
                }
                sb.Append("</ul>\n");
            }

            if (articles.Count > 0)
            {
                sb.Append("<h2 class=\"seo-prerender-h2\">آموزش‌های تخصصی</h2>\n<ul class=\"seo-prerender-list\">");
                foreach (var a in articles)
                {
                    var label = string.IsNullOrWhiteSpace(a.Category) ? a.Title : $"{a.Title} — {a.Category}";
                    sb.Append($"<li><a href=\"{EscapeHtml(AcademyArticleUrl(a.Id, a.Slug))}\">{EscapeHtml(label)}</a></li>");
                }
                sb.Append("</ul>\n");
            }

            if (courses.Count == 0 && articles.Count == 0)
            {
                sb.Append("<p class=\"seo-prerender-desc\">به‌زودی دوره‌ها و آموزش‌های آکادمی منتشر می‌شوند.</p>\n");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SEO middleware: failed to build academy hub content");
        }

        return sb.ToString();
    }

    private string AcademyCourseUrl(int id, string? slug)
        => string.IsNullOrWhiteSpace(slug) ? $"{_baseUrl}/academy/{id}" : $"{_baseUrl}/academy/{slug}";

    private string AcademyArticleUrl(int id, string? slug)
        => string.IsNullOrWhiteSpace(slug) ? $"{_baseUrl}/academy/articles/{id}" : $"{_baseUrl}/academy/articles/{slug}";

    private async Task<string> BuildAcademyJsonLdAsync(ErrorServiceDbContext db, string canonical)
    {
        const string title = "آکادمی آموزش تعمیر لوازم خانگی و الکترونیک | ارورسرویس";
        const string description = "دوره‌ها و آموزش‌های تخصصی تعمیر لوازم خانگی، بردهای الکترونیک و عیب‌یابی";

        try
        {
            var courses = await db.TrainingCourses
                .AsNoTracking()
                .Where(c => c.IsPublished)
                .OrderBy(c => c.SortOrder)
                .ThenByDescending(c => c.CreatedAt)
                .Take(30)
                .Select(c => new { c.Id, c.Title, c.Slug })
                .ToListAsync();

            var articles = await db.TrainingArticles
                .AsNoTracking()
                .Where(a => a.IsPublished)
                .OrderByDescending(a => a.CreatedAt)
                .Take(30)
                .Select(a => new { a.Id, a.Title, a.Slug })
                .ToListAsync();

            var listItems = courses
                .Select(c => new Dictionary<string, object?>
                {
                    ["name"] = c.Title,
                    ["url"] = AcademyCourseUrl(c.Id, c.Slug)
                })
                .Concat(articles.Select(a => new Dictionary<string, object?>
                {
                    ["name"] = a.Title,
                    ["url"] = AcademyArticleUrl(a.Id, a.Slug)
                }))
                .Select((item, index) =>
                {
                    item["@type"] = "ListItem";
                    item["position"] = index + 1;
                    return (object)item;
                })
                .ToList();

            var graph = new List<object>
            {
                new Dictionary<string, object?>
                {
                    ["@type"] = "CollectionPage",
                    ["name"] = "آکادمی آموزش تعمیرات لوازم خانگی و الکترونیک",
                    ["description"] = description,
                    ["url"] = canonical,
                    ["inLanguage"] = "fa-IR",
                    ["isPartOf"] = new Dictionary<string, object?>
                    {
                        ["@type"] = "WebSite",
                        ["name"] = "ارورسرویس",
                        ["url"] = _baseUrl + "/"
                    }
                },
                new Dictionary<string, object?>
                {
                    ["@type"] = "BreadcrumbList",
                    ["itemListElement"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["@type"] = "ListItem",
                            ["position"] = 1,
                            ["name"] = "خانه",
                            ["item"] = _baseUrl + "/"
                        },
                        new Dictionary<string, object?>
                        {
                            ["@type"] = "ListItem",
                            ["position"] = 2,
                            ["name"] = "آکادمی آموزش",
                            ["item"] = canonical
                        }
                    }
                }
            };

            if (listItems.Count > 0)
            {
                graph.Add(new Dictionary<string, object?>
                {
                    ["@type"] = "ItemList",
                    ["name"] = "دوره‌ها و آموزش‌های آکادمی",
                    ["itemListElement"] = listItems
                });
            }

            return JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@graph"] = graph
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SEO middleware: failed to build academy json-ld");
            return BuildStaticJsonLd("CollectionPage", canonical, title, description);
        }
    }

    private string BuildHomeShell(string title, string description, string? slideImageUrl, string? slideImageMobileUrl, string? slideTitle)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"seo-shell\" dir=\"rtl\">");
        sb.Append("<div class=\"seo-shell__top\"><img src=\"/images/branding/logo.svg\" alt=\"ارورسرویس\" width=\"40\" height=\"40\" /><span>ارورسرویس</span></div>");

        if (!string.IsNullOrWhiteSpace(slideImageUrl))
        {
            var alt = string.IsNullOrWhiteSpace(slideTitle) ? title : slideTitle!;
            sb.Append("<div class=\"seo-shell__hero\">");
            sb.Append("<picture>");
            if (!string.IsNullOrWhiteSpace(slideImageMobileUrl))
            {
                sb.Append($"<source media=\"(max-width: 899.98px)\" srcset=\"{EscapeHtml(AbsUrl(slideImageMobileUrl))}\" />");
            }
            sb.Append($"<img class=\"seo-shell__img\" src=\"{EscapeHtml(AbsUrl(slideImageUrl))}\" alt=\"{EscapeHtml(alt)}\" width=\"2880\" height=\"600\" fetchpriority=\"high\" decoding=\"async\" />");
            sb.Append("</picture>");
            sb.Append("</div>");
        }

        // H1 مستقل از <title> است تا عنوان SERP (قابل‌ویرایش ادمین) تکراری نشود؛
        // عنوان صفحات موتور جستجو برندمحور است و H1 موضوع اصلی صفحه را می‌گوید.
        const string homeHeading = "مرجع تخصصی تعمیر لوازم خانگی، بانک کد خطای دستگاه‌ها و قطعات یدکی";
        var desc = EscapeHtml(description).Replace("ارورسرویس", "<strong>ارورسرویس</strong>");

        sb.Append("<div class=\"seo-shell__text\">");
        sb.Append($"<h1 class=\"seo-shell__title\">{homeHeading}</h1>");
        sb.Append($"<p class=\"seo-shell__desc\">{desc}</p>");

        sb.Append("<h2 class=\"seo-shell__h2\">خدمات ارورسرویس</h2>");
        sb.Append("<ul class=\"seo-shell__list\">");
        sb.Append("<li><a href=\"/technical/error-codes\">بانک کدهای خطا</a>: جستجوی <strong>کد خطای یخچال، ماشین لباسشویی و ظرفشویی</strong> بر اساس برند و نوع دستگاه.</li>");
        sb.Append("<li><a href=\"/products\">فروشگاه قطعات یدکی</a>: خرید <strong>قطعات اصل لوازم خانگی</strong> با مشخصات فنی، قیمت به‌روز و ضمانت اصالت.</li>");
        sb.Append("<li><a href=\"/admission/repair\">درخواست تعمیر آنلاین</a>: ثبت درخواست، بررسی کارشناس و پیگیری شفاف وضعیت.</li>");
        sb.Append("<li><a href=\"/technical/consultation\">مشاوره فنی</a>: پرسش از کارشناسان تعمیرات و عیب‌یابی اولیه دستگاه.</li>");
        sb.Append("</ul>");

        sb.Append("<h2 class=\"seo-shell__h2\">آموزش و همکاران</h2>");
        sb.Append("<p class=\"seo-shell__desc\">در <a href=\"/academy\">آکادمی ارورسرویس</a> دوره‌ها و مقاله‌های گام‌به‌گام تعمیرات را ببینید و از <a href=\"/workshops\">فهرست همکاران</a> تعمیرکار نزدیک خود را پیدا کنید.</p>");
        sb.Append("</div>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private string BuildHomePrerenderContent(string title, string description, string? slideImageUrl, string? slideTitle)
    {
        var sb = new StringBuilder();
        sb.Append("<h1 class=\"seo-prerender\">مرجع تخصصی تعمیر لوازم خانگی و بانک کد خطا</h1>\n");

        var desc = EscapeHtml(description).Replace("ارورسرویس", "<strong>ارورسرویس</strong>");
        sb.Append($"<p class=\"seo-prerender-desc\">{desc}</p>\n");

        if (!string.IsNullOrWhiteSpace(slideImageUrl))
        {
            var alt = string.IsNullOrWhiteSpace(slideTitle) ? title : slideTitle!;
            sb.Append($"<img class=\"seo-prerender-img\" src=\"{EscapeHtml(AbsUrl(slideImageUrl))}\" alt=\"{EscapeHtml(alt)}\" width=\"2880\" height=\"600\" />\n");
        }

        sb.Append("<h2 class=\"seo-prerender-h2\">تعمیر و سرویس لوازم خانگی در محل</h2>\n");
        sb.Append("<p class=\"seo-prerender-desc\"><strong>تعمیر لوازم خانگی</strong> شامل یخچال، ماشین لباسشویی، ظرفشویی، اجاق گاز و مایکروویو توسط تکنسین‌های مجرب <strong>ارورسرویس</strong> در محل و با ضمانت کار.</p>\n");

        sb.Append("<h2 class=\"seo-prerender-h2\">فروشگاه قطعات یدکی و لوازم الکترونیک</h2>\n");
        sb.Append("<p class=\"seo-prerender-desc\">خرید <strong>قطعات یدکی اصل</strong>، قطعات الکترونیک و تجهیزات ابزار دقیق با ضمانت اصالت کالا و ارسال سریع.</p>\n");

        sb.Append("<h2 class=\"seo-prerender-h2\">بانک کدهای خطا و درخواست تعمیر</h2>\n");
        sb.Append("<p class=\"seo-prerender-desc\">جستجوی <strong>کد خطا</strong> لوازم خانگی بر اساس برند و مدل، آموزش تعمیر در آکادمی و ثبت <strong>درخواست تعمیر</strong> آنلاین.</p>\n");

        sb.Append("<h2 class=\"seo-prerender-h2\">بخش‌های اصلی سایت</h2>\n<ul class=\"seo-prerender-list\">");
        var links = new (string Text, string Url)[]
        {
            ("فروشگاه قطعات یدکی", "/products"),
            ("آکادمی تعمیرات", "/academy"),
            ("بانک کدهای خطا", "/technical/error-codes"),
            ("اخبار و مقالات", "/news"),
            ("همکاران ما", "/workshops"),
            ("درخواست تعمیر", "/admission/repair"),
            ("مشاوره فنی", "/technical/consultation"),
            ("تماس با ما", "/contact"),
        };
        foreach (var (text, url) in links)
        {
            sb.Append($"<li><a href=\"{EscapeHtml(_baseUrl + url)}\">{EscapeHtml(text)}</a></li>");
        }

        sb.Append("</ul>");
        return sb.ToString();
    }

    private async Task<string> BuildPrerenderBodyAsync(string cleanPath, object entity, ErrorServiceDbContext db)
    {
        var body = BuildPrerenderBody(cleanPath, entity);
        var reviewsHtml = await BuildReviewsHtmlAsync(cleanPath, entity, db);
        return body + reviewsHtml;
    }

    private async Task<string> BuildReviewsHtmlAsync(string cleanPath, object entity, ErrorServiceDbContext db)
    {
        try
        {
            if (cleanPath.StartsWith("products/"))
            {
                var node = JsonSerializer.SerializeToNode(entity);
                var pid = node?["Id"]?.GetValue<int>() ?? 0;
                if (pid <= 0) return "";

                var roots = await db.ProductReviews
                    .Where(r => r.ProductId == pid && r.IsApproved && r.ParentId == null)
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new { r.Id, r.FullName, r.Rating, r.Content, r.CreatedAt })
                    .ToListAsync();

                if (roots.Count == 0) return "";

                var ids = roots.Select(r => r.Id).ToList();
                var replies = await db.ProductReviews
                    .Where(r => r.ProductId == pid && r.IsApproved && r.ParentId != null && ids.Contains(r.ParentId.Value))
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new { r.Id, r.ParentId, r.FullName, r.Content, r.CreatedAt })
                    .ToListAsync();

                return BuildCommentSectionHtml(
                    "نظرات کاربران",
                    roots.Select(r => new SeoComment(
                        r.FullName, r.Content, r.CreatedAt,
                        r.Rating,
                        replies.Where(x => x.ParentId == r.Id)
                            .Select(x => new SeoComment(x.FullName, x.Content, x.CreatedAt, null, new List<SeoComment>()))
                            .ToList())),
                    showRating: true);
            }

            if (TryGetContentDetailSegment(cleanPath, out _, out _))
            {
                var node = JsonSerializer.SerializeToNode(entity);
                var nid = node?["Id"]?.GetValue<int>() ?? 0;
                if (nid <= 0) return "";

                var roots = await db.NewsComments
                    .Where(c => c.NewsId == nid && c.IsApproved && c.ParentId == null)
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new { c.Id, c.FullName, c.Rating, c.Content, c.CreatedAt })
                    .ToListAsync();

                if (roots.Count == 0) return "";

                var ids = roots.Select(c => c.Id).ToList();
                var replies = await db.NewsComments
                    .Where(c => c.NewsId == nid && c.IsApproved && c.ParentId != null && ids.Contains(c.ParentId.Value))
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new { c.Id, c.ParentId, c.FullName, c.Content, c.CreatedAt })
                    .ToListAsync();

                return BuildCommentSectionHtml(
                    "نظرات کاربران",
                    roots.Select(c => new SeoComment(
                        c.FullName, c.Content, c.CreatedAt,
                        c.Rating,
                        replies.Where(x => x.ParentId == c.Id)
                            .Select(x => new SeoComment(x.FullName, x.Content, x.CreatedAt, null, new List<SeoComment>()))
                            .ToList())),
                    showRating: true);
            }

            if (cleanPath.StartsWith("academy/articles/"))
            {
                var node = JsonSerializer.SerializeToNode(entity);
                var aid = node?["Id"]?.GetValue<int>() ?? 0;
                if (aid <= 0) return "";

                var roots = await db.TrainingArticleComments
                    .Where(c => c.ArticleId == aid && c.IsApproved && c.ParentId == null)
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new { c.Id, c.FullName, c.Rating, c.Content, c.CreatedAt })
                    .ToListAsync();

                if (roots.Count == 0) return "";

                var ids = roots.Select(c => c.Id).ToList();
                var replies = await db.TrainingArticleComments
                    .Where(c => c.ArticleId == aid && c.IsApproved && c.ParentId != null && ids.Contains(c.ParentId.Value))
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new { c.Id, c.ParentId, c.FullName, c.Content, c.CreatedAt })
                    .ToListAsync();

                return BuildCommentSectionHtml(
                    "نظرات کاربران",
                    roots.Select(c => new SeoComment(
                        c.FullName, c.Content, c.CreatedAt,
                        c.Rating,
                        replies.Where(x => x.ParentId == c.Id)
                            .Select(x => new SeoComment(x.FullName, x.Content, x.CreatedAt, null, new List<SeoComment>()))
                            .ToList())),
                    showRating: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SEO middleware: failed to load reviews for {Path}", cleanPath);
        }

        return "";
    }

    private sealed record SeoComment(string FullName, string Content, DateTimeOffset CreatedAt, int? Rating, List<SeoComment> Replies);

    private static string BuildCommentSectionHtml(string heading, IEnumerable<SeoComment> comments, bool showRating)
    {
        var list = comments.ToList();
        if (list.Count == 0) return "";

        var sb = new StringBuilder();
        sb.Append($"<section class=\"seo-prerender-reviews\" itemscope itemtype=\"https://schema.org/Review\">");
        sb.Append($"<h2 class=\"seo-prerender-h2\">{EscapeHtml(heading)}</h2>\n");

        foreach (var c in list)
        {
            var date = c.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd");
            sb.Append("<article class=\"seo-prerender-review\" itemprop=\"review\" itemscope itemtype=\"https://schema.org/Review\">");
            sb.Append($"<h3 class=\"seo-prerender-review-author\" itemprop=\"author\">{EscapeHtml(c.FullName)}</h3>");
            if (showRating && c.Rating.HasValue)
            {
                sb.Append($"<div class=\"seo-prerender-review-rating\" itemprop=\"reviewRating\" itemscope itemtype=\"https://schema.org/Rating\">");
                sb.Append($"<meta itemprop=\"ratingValue\" content=\"{c.Rating.Value}\" />");
                sb.Append($"<meta itemprop=\"bestRating\" content=\"5\" />");
                sb.Append($"{c.Rating.Value} از ۵</div>\n");
            }
            sb.Append($"<div class=\"seo-prerender-review-body\" itemprop=\"reviewBody\">{EscapeHtml(c.Content)}</div>");
            sb.Append($"<meta itemprop=\"datePublished\" content=\"{date}\" />");

            if (c.Replies.Count > 0)
            {
                sb.Append("<div class=\"seo-prerender-review-replies\">");
                foreach (var r in c.Replies)
                {
                    sb.Append("<div class=\"seo-prerender-review-reply\">");
                    sb.Append($"<strong>پاسخ {EscapeHtml(r.FullName)}:</strong> ");
                    sb.Append(EscapeHtml(r.Content));
                    sb.Append("</div>");
                }
                sb.Append("</div>");
            }

            sb.Append("</article>\n");
        }

        sb.Append("</section>\n");
        return sb.ToString();
    }

    private async Task<string> EnrichProductJsonLdWithReviewsAsync(string jsonLd, object entity, ErrorServiceDbContext db)
    {
        try
        {
            var node = JsonSerializer.SerializeToNode(entity);
            var pid = node?["Id"]?.GetValue<int>() ?? 0;
            if (pid <= 0) return jsonLd;

            var reviews = await db.ProductReviews
                .Where(r => r.ProductId == pid && r.IsApproved)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new { r.FullName, r.Rating, r.Content, r.CreatedAt })
                .ToListAsync();

            if (reviews.Count == 0) return jsonLd;

            var avg = Math.Round(reviews.Average(r => (double)r.Rating), 1);
            var reviewArr = string.Join(",", reviews.Take(20).Select(r =>
            {
                var author = EscapeJson(r.FullName);
                var body = EscapeJson(r.Content);
                var date = r.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd");
                return $@"{{""@type"":""Review"",""author"":{{""@type"":""Person"",""name"":""{author}""}},""reviewBody"":""{body}"",""reviewRating"":{{""@type"":""Rating"",""ratingValue"":{r.Rating},""bestRating"":5}},""datePublished"":""{date}""}}";
            }));

            var aggregate = $@"{{""@type"":""AggregateRating"",""ratingValue"":{avg.ToString(System.Globalization.CultureInfo.InvariantCulture)},""reviewCount"":{reviews.Count},""bestRating"":5,""worstRating"":1}}";
            var reviewBlock = $@",""aggregateRating"":{aggregate},""review"":[{reviewArr}]";

            // Inject into the first Product object of the JSON-LD array (or single object)
            if (jsonLd.StartsWith("[") && jsonLd.Contains("\"@type\":\"Product\""))
            {
                var idx = jsonLd.IndexOf("\"@type\":\"Product\"", StringComparison.Ordinal);
                if (idx > 0)
                {
                    // Find matching closing brace of the Product object by scanning braces from its opening {
                    var objStart = jsonLd.LastIndexOf('{', idx);
                    if (objStart > 0)
                    {
                        var depth = 0;
                        var objEnd = -1;
                        for (var i = objStart; i < jsonLd.Length; i++)
                        {
                            var ch = jsonLd[i];
                            if (ch == '{') depth++;
                            else if (ch == '}')
                            {
                                depth--;
                                if (depth == 0) { objEnd = i; break; }
                            }
                        }
                        if (objEnd > 0)
                            return jsonLd[..objEnd] + reviewBlock + jsonLd[objEnd..];
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SEO middleware: failed to enrich product JSON-LD with reviews");
        }

        return jsonLd;
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
            sb.Append($"<li>قیمت: {price:N0} تومان</li>");
            if (!string.IsNullOrEmpty(compat)) sb.Append($"<li>سازگاری: {EscapeHtml(compat)}</li>");
            sb.Append("</ul>\n");
            if (!string.IsNullOrEmpty(symptoms))
            {
                sb.Append("<h2 class=\"seo-prerender-h2\">علائم خرابی این قطعه</h2>\n");
                sb.Append(TextToParagraphs(symptoms, "seo-prerender-desc"));
            }
            return sb.ToString();
        }

        if (TryGetContentDetailSegment(cleanPath, out var contentType, out _))
        {
            var contentTitle = J(node, "Title") ?? ContentTypeCatalog.Label(contentType);
            var summary = J(node, "Summary") ?? "";
            var content = J(node, "Content") ?? "";
            var categoryTitle = J(node, "CategoryTitle");
            sb.Append($"<h1 class=\"seo-prerender\">{EscapeHtml(contentTitle)}</h1>\n");
            if (!string.IsNullOrEmpty(categoryTitle))
                sb.Append($"<p class=\"seo-prerender-desc\">دسته: {EscapeHtml(categoryTitle)}</p>\n");
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
            var videoUrl = J(node, "VideoUrl") ?? "";
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
            if (!string.IsNullOrEmpty(videoUrl))
                sb.Append($"<p class=\"seo-prerender-desc\">ویدیوی آموزشی: <a href=\"{EscapeHtml(videoUrl)}\">مشاهده ویدیو</a></p>\n");

            var faqs = node["Faqs"]?.AsArray();
            if (faqs is { Count: > 0 })
            {
                sb.Append("<h2 class=\"seo-prerender-h2\">پرسش‌های متداول</h2>\n");
                foreach (var faq in faqs)
                {
                    var question = faq?["Question"]?.GetValue<string>();
                    var answer = faq?["Answer"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer)) continue;

                    sb.Append($"<h3 class=\"seo-prerender-h3\">{EscapeHtml(question)}</h3>\n");
                    sb.Append(TextToParagraphs(answer, "seo-prerender-desc"));
                }
            }
            return sb.ToString();
        }

        if (cleanPath.StartsWith("technical/sensor-finder/"))
        {
            var sensorName = J(node, "SensorName");
            var sensorType = J(node, "SensorType") ?? "";
            var size = J(node, "SizeText") ?? "";
            var notes = J(node, "Notes") ?? "";
            var seo = SensorSeoText.Build(sensorName, sensorType, size, J(node, "DeviceName"), notes);
            sb.Append($"<h1 class=\"seo-prerender\">{EscapeHtml(seo.FullName)}</h1>\n");
            sb.Append("<ul class=\"seo-prerender-list\">");
            var deviceName = J(node, "DeviceName") ?? "";
            if (!string.IsNullOrEmpty(deviceName)) sb.Append($"<li>دستگاه: {EscapeHtml(deviceName)}</li>");
            if (!string.IsNullOrEmpty(sensorType)) sb.Append($"<li>نوع: {EscapeHtml(sensorType)}</li>");
            if (!string.IsNullOrEmpty(size)) sb.Append($"<li>مقدار / سایز: {EscapeHtml(size)}</li>");
            sb.Append("</ul>\n");
            if (!string.IsNullOrEmpty(notes))
                sb.Append(TextToParagraphs(notes, "seo-prerender-desc"));
            return sb.ToString();
        }

        if (cleanPath.StartsWith("workshops/"))
        {
            var wName = J(node, "WorkshopName") ?? "همکار ارورسرویس";
            var owner = J(node, "OwnerFullName") ?? "";
            var city = J(node, "CityName") ?? "";
            var province = J(node, "ProvinceName") ?? "";
            var address = J(node, "Address") ?? "";
            var phone = J(node, "MobileNumber") ?? "";
            var wImg = J(node, "ImageUrl") ?? "";

            var place = string.Join("، ", new[] { city, province }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .Distinct());

            sb.Append($"<h1 class=\"seo-prerender\">{EscapeHtml(wName)}</h1>\n");

            if (!string.IsNullOrEmpty(wImg))
                sb.Append($"<img class=\"seo-prerender-img\" src=\"{EscapeHtml(AbsUrl(wImg))}\" alt=\"{EscapeHtml(wName)}\" />\n");

            var intro = "«" + wName + "» یکی از همکاران ارورسرویس" +
                        (place.Length > 0 ? " در " + place : "") +
                        " است و در زمینه تعمیر و سرویس لوازم خانگی فعالیت می‌کند.";
            sb.Append($"<p class=\"seo-prerender-desc\">{EscapeHtml(intro)}</p>\n");

            sb.Append("<ul class=\"seo-prerender-list\">");
            if (!string.IsNullOrEmpty(owner)) sb.Append($"<li>مسئول مجموعه: {EscapeHtml(owner)}</li>");
            if (place.Length > 0) sb.Append($"<li>استان و شهر: {EscapeHtml(place)}</li>");
            if (!string.IsNullOrEmpty(address)) sb.Append($"<li>آدرس: {EscapeHtml(address)}</li>");
            if (!string.IsNullOrEmpty(phone)) sb.Append($"<li>شماره تماس: {EscapeHtml(phone)}</li>");
            sb.Append("</ul>\n");

            sb.Append($"<p class=\"seo-prerender-desc\"><a href=\"{EscapeHtml(_baseUrl + "/workshops")}\">مشاهده فهرست کامل همکاران ارورسرویس</a></p>\n");
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
        text = StripHtml(text).Replace("\r\n", "\n").Replace("\r", "\n").Trim();
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

    /// <summary>مسیرهای جزئیات مرکز محتوا (news/tips/announcements) را تشخیص می‌دهد.</summary>
    /// <summary>مسیرهای دسته/برچسب مرکز محتوا را تشخیص می‌دهد.</summary>
    private static bool TryGetContentListingSegment(string cleanPath, out ContentType type, out string kind, out string slug)
    {
        foreach (var candidate in ContentTypeCatalog.All)
        {
            var route = ContentTypeCatalog.Slug(candidate);
            if (cleanPath == route
                || (cleanPath.StartsWith(route + "/", StringComparison.Ordinal)
                    && (cleanPath.StartsWith(route + "/category/", StringComparison.Ordinal)
                        || cleanPath.StartsWith(route + "/tag/", StringComparison.Ordinal))))
            {
                var rest = cleanPath.Length == route.Length ? "" : cleanPath[(route.Length + 1)..];
                if (rest.StartsWith("category/", StringComparison.Ordinal))
                {
                    type = candidate;
                    kind = "category";
                    slug = Uri.UnescapeDataString(rest["category/".Length..].Split('/')[0]);
                    return true;
                }
                if (rest.StartsWith("tag/", StringComparison.Ordinal))
                {
                    type = candidate;
                    kind = "tag";
                    slug = Uri.UnescapeDataString(rest["tag/".Length..].Split('/')[0]);
                    return true;
                }
            }
        }

        type = ContentType.News;
        kind = "";
        slug = "";
        return false;
    }

    private static bool TryGetContentDetailSegment(string cleanPath, out ContentType type, out string segment)
    {
        foreach (var candidate in ContentTypeCatalog.All)
        {
            var prefix = ContentTypeCatalog.Slug(candidate) + "/";
            if (cleanPath.StartsWith(prefix, StringComparison.Ordinal))
            {
                type = candidate;
                segment = cleanPath[prefix.Length..].Split('/')[0];
                return true;
            }
        }

        type = ContentType.News;
        segment = "";
        return false;
    }

    private async Task<string?> TryResolveRedirect(string cleanPath, ErrorServiceDbContext db)
    {
        try
        {
            if (cleanPath == "help/faq") return "/faq";

            if (cleanPath.StartsWith("products/"))
            {
                var seg = cleanPath["products/".Length..].Split('/')[0];
                if (int.TryParse(seg, out var pid))
                {
                    var slug = await db.Products
                        .Where(p => p.Id == pid && p.IsAvailable && !p.IsDraft)
                        .Select(p => p.Slug)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrEmpty(slug)) return $"/products/{slug}";
                }
                return null;
            }

            if (TryGetContentDetailSegment(cleanPath, out var contentType, out var contentSegment))
            {
                if (int.TryParse(contentSegment, out var nid))
                {
                    var slug = await db.News
                        .Where(n => n.Id == nid && n.ContentType == contentType && n.IsPublished)
                        .Select(n => n.Slug)
                        .FirstOrDefaultAsync();
                    if (!string.IsNullOrEmpty(slug))
                        return $"{ContentTypeCatalog.Route(contentType)}/{slug}";
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
                    return await db.Products.Where(p => p.Id == pid && p.IsAvailable && !p.IsDraft).Select(p => new { p.Id, p.Name, p.Description, p.Price, p.DiscountPrice, p.MainImageUrl, p.CompatibilityInfo, p.FailureSymptoms, p.UpdatedAt, CategoryName = p.Category.Name }).FirstOrDefaultAsync();

                return await db.Products.Where(p => p.Slug == seg && p.IsAvailable && !p.IsDraft).Select(p => new { p.Id, p.Name, p.Description, p.Price, p.DiscountPrice, p.MainImageUrl, p.CompatibilityInfo, p.FailureSymptoms, p.UpdatedAt, CategoryName = p.Category.Name }).FirstOrDefaultAsync();
            }

            if (TryGetContentDetailSegment(cleanPath, out var contentType, out var contentSegment))
            {
                var now = DateTimeOffset.UtcNow;
                var visible = db.News.Where(n => n.ContentType == contentType
                    && n.IsPublished
                    && (n.PublishAt == null || n.PublishAt <= now)
                    && (n.ExpireAt == null || n.ExpireAt > now));

                if (int.TryParse(contentSegment, out var nid))
                    return await visible.Where(n => n.Id == nid)
                        .Select(n => new { n.Id, n.Title, n.Summary, n.Content, n.ImageUrl, n.CreatedAt, n.UpdatedAt, PublishedAt = n.PublishAt ?? n.CreatedAt, n.ContentType, CategoryTitle = n.Category != null ? n.Category.Title : null, CategorySlug = n.Category != null ? n.Category.Slug : null, n.SourceName, n.SourceUrl })
                        .FirstOrDefaultAsync();

                return await visible.Where(n => n.Slug == contentSegment)
                    .Select(n => new { n.Id, n.Title, n.Summary, n.Content, n.ImageUrl, n.CreatedAt, n.UpdatedAt, PublishedAt = n.PublishAt ?? n.CreatedAt, n.ContentType, CategoryTitle = n.Category != null ? n.Category.Title : null, CategorySlug = n.Category != null ? n.Category.Slug : null, n.SourceName, n.SourceUrl })
                    .FirstOrDefaultAsync();
            }

            if (cleanPath.StartsWith("academy/articles/"))
            {
                var seg = cleanPath["academy/articles/".Length..].Split('/')[0];
                if (int.TryParse(seg, out var aid))
                    return await db.TrainingArticles.Where(a => a.Id == aid && a.IsPublished).Select(a => new { a.Id, a.Title, a.Summary, a.CoverImageUrl, a.CreatedAt, a.UpdatedAt, Blocks = a.Blocks.OrderBy(b => b.SortOrder).Select(b => new { b.Title, b.Content, b.BlockType, b.ThumbnailUrl }).ToList() }).FirstOrDefaultAsync();

                return await db.TrainingArticles.Where(a => a.Slug == seg && a.IsPublished).Select(a => new { a.Id, a.Title, a.Summary, a.CoverImageUrl, a.CreatedAt, a.UpdatedAt, Blocks = a.Blocks.OrderBy(b => b.SortOrder).Select(b => new { b.Title, b.Content, b.BlockType, b.ThumbnailUrl }).ToList() }).FirstOrDefaultAsync();
            }

            if (cleanPath.StartsWith("academy/"))
            {
                var segments = cleanPath.Replace("academy/", "").Split('/');

                if (segments.Length == 1)
                {
                    if (int.TryParse(segments[0], out var courseId))
                        return await db.TrainingCourses.Where(c => c.Id == courseId && c.IsPublished).Select(c => new { c.Id, c.Title, c.Summary, c.CoverImageUrl, c.CreatedAt, c.UpdatedAt }).FirstOrDefaultAsync();

                    return await db.TrainingCourses.Where(c => c.Slug == segments[0] && c.IsPublished).Select(c => new { c.Id, c.Title, c.Summary, c.CoverImageUrl, c.CreatedAt, c.UpdatedAt }).FirstOrDefaultAsync();
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

            if (cleanPath.StartsWith("workshops/"))
            {
                var seg = cleanPath["workshops/".Length..].Split('/')[0];
                if (int.TryParse(seg, out var wid))
                    return await db.Workshops
                        .Where(w => w.Id == wid && !w.IsDeleted)
                        .Select(w => new
                        {
                            w.Id, w.WorkshopName, w.OwnerFullName, w.MobileNumber,
                            w.Address, w.ImageUrl, w.CreatedAt,
                            ProvinceName = w.Province.Name,
                            CityName = w.City.Name
                        })
                        .FirstOrDefaultAsync();
            }

            if (cleanPath.StartsWith("technical/error-codes/"))
            {
                var parts = cleanPath.Replace("technical/error-codes/", "").Split('/');
                if (parts.Length >= 3)
                {
                    var brand = Uri.UnescapeDataString(parts[0].Trim());
                    var device = Uri.UnescapeDataString(parts[1].Trim());
                    var code = Uri.UnescapeDataString(parts[2].Trim());
                    var brandKey = brand.Trim().ToLowerInvariant();
                    var deviceKey = device.Trim().ToLowerInvariant();
                    var codeKey = code.Trim().ToLowerInvariant();
                    return await db.ErrorCodes.Where(e => e.Brand.Trim().ToLower() == brandKey &&
                                                          e.DeviceType.Trim().ToLower() == deviceKey &&
                                                          e.Code.Trim().ToLower() == codeKey)
                        .Select(e => new
                        {
                            e.Id, e.Brand, e.DeviceType, e.Code, e.Description, e.Solution,
                            e.TechnicalNotes, e.ModelNames, e.ImageUrl, e.VideoUrl,
                            Faqs = e.Faqs.OrderBy(f => f.SortOrder)
                                .Select(f => new { f.Question, f.Answer })
                                .ToList()
                        }).FirstOrDefaultAsync();
                }
            }

            if (cleanPath.StartsWith("technical/sensor-finder/") && int.TryParse(cleanPath.Replace("technical/sensor-finder/", "").Split('/')[0], out var sid))
                return await db.SensorFinderRecords.Where(s => s.Id == sid).Select(s => new
                {
                    s.Id,
                    s.SensorName,
                    s.SensorType,
                    s.SizeText,
                    s.Notes,
                    DeviceName = s.Device.Name,
                    FirstImageUrl = s.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
                }).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SEO middleware: failed to fetch entity data for {Path}", cleanPath);
        }

        return null;
    }

    private bool TryGetDynamicMeta(string cleanPath, object? entity, out (string title, string description, string keywords, string jsonLd, string ogImage, string ogType) meta, out bool entityFound)
    {
        meta = default;
        entityFound = entity != null;

        if (entity == null) return false;

        if (cleanPath.StartsWith("products/"))
        {
            var p = JsonSerializer.SerializeToNode(entity)!;
            var pName = p["Name"]?.GetValue<string>() ?? "محصول";
            var priceToman = p["DiscountPrice"]?.GetValue<decimal?>() ?? p["Price"]?.GetValue<decimal>() ?? 0;
            // Display prices are Toman; schema.org requires ISO 4217 currency (IRR = Rial).
            var priceRial = (priceToman * 10).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var img = AbsUrl(p["MainImageUrl"]?.GetValue<string>() ?? "");
            var updated = p["UpdatedAt"]?.GetValue<DateTime>() ?? DateTime.UtcNow;
            var pTitle = SeoTitles.Product(pName);
            var pDescription = SeoDescriptions.Product(p["Description"]?.GetValue<string>(), pName, p["CategoryName"]?.GetValue<string>());
            var pKeywords = $"خرید {pName}, قطعات یدکی, {pName}";
            var pBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{{""@type"":""ListItem"",""position"":1,""name"":""فروشگاه"",""item"":""{_baseUrl}/products""}},{{""@type"":""ListItem"",""position"":2,""name"":""{EscapeJson(pName)}"",""item"":""{_baseUrl}/{cleanPath}""}}]}}";
            var pSku = p["Id"]?.GetValue<int>() ?? 0;
            var pJsonLd = $@"[{{""@context"":""https://schema.org"",""@type"":""Product"",""name"":""{EscapeJson(pName)}"",""description"":""{EscapeJson(pDescription)}"",""image"":[""{img}""],""sku"":""{pSku}"",""brand"":{{""@type"":""Brand"",""name"":""ErrorService""}},""offers"":{{""@type"":""Offer"",""price"":{priceRial},""priceCurrency"":""IRR"",""itemCondition"":""https://schema.org/NewCondition"",""availability"":""https://schema.org/InStock"",""url"":""{_baseUrl}/{cleanPath}""}},""url"":""{_baseUrl}/{cleanPath}""}},{pBreadcrumb}]";
            meta = (pTitle, pDescription, pKeywords, pJsonLd, img, "product");
            return true;
        }

        if (TryGetContentDetailSegment(cleanPath, out var contentType, out _))
        {
            var n = JsonSerializer.SerializeToNode(entity)!;
            var typeLabel = ContentTypeCatalog.Label(contentType);
            var typePlural = ContentTypeCatalog.PluralLabel(contentType);
            var contentRoute = _baseUrl + ContentTypeCatalog.Route(contentType);
            var nTitleText = n["Title"]?.GetValue<string>() ?? typeLabel;
            var img = AbsUrl(n["ImageUrl"]?.GetValue<string>() ?? "");
            var published = n["PublishedAt"]?.GetValue<DateTime>() ?? n["CreatedAt"]?.GetValue<DateTime>() ?? DateTime.UtcNow;
            var updated = n["UpdatedAt"]?.GetValue<DateTime>() ?? published;
            var nTitle = SeoTitles.News(nTitleText);
            var nDesc = SeoDescriptions.News(n["Summary"]?.GetValue<string>(), nTitleText);
            var nKeywords = $"{typeLabel}, {nTitleText}, {typePlural}, ارورسرویس";

            var breadcrumbItems = new List<string>
            {
                $@"{{""@type"":""ListItem"",""position"":1,""name"":""{EscapeJson(typePlural)}"",""item"":""{contentRoute}""}}"
            };
            var categoryTitle = J(n, "CategoryTitle");
            if (!string.IsNullOrWhiteSpace(categoryTitle))
            {
                var categorySlug = J(n, "CategorySlug") ?? "";
                var categoryUrl = contentRoute + "/category/" + Uri.EscapeDataString(categorySlug);
                breadcrumbItems.Add($@"{{""@type"":""ListItem"",""position"":2,""name"":""{EscapeJson(categoryTitle)}"",""item"":""{categoryUrl}""}}");
            }
            breadcrumbItems.Add($@"{{""@type"":""ListItem"",""position"":{breadcrumbItems.Count + 1},""name"":""{EscapeJson(nTitleText)}"",""item"":""{_baseUrl}/{cleanPath}""}}");

            var nBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{string.Join(",", breadcrumbItems)}]}}";
            var schemaType = ContentTypeCatalog.ArticleSchemaType(contentType);
            var nJsonLd = $@"[{{""@context"":""https://schema.org"",""@type"":""{schemaType}"",""mainEntityOfPage"":{{""@type"":""WebPage"",""@id"":""{_baseUrl}/{cleanPath}""}},""headline"":""{EscapeJson(nTitleText)}"",""description"":""{EscapeJson(nDesc)}"",""image"":[""{img}""],""datePublished"":""{published:yyyy-MM-dd}"",""dateModified"":""{updated:yyyy-MM-dd}"",""inLanguage"":""fa-IR"",""articleSection"":""{EscapeJson(typePlural)}"",""author"":{{""@type"":""Organization"",""name"":""ارورسرویس"",""url"":""{_baseUrl}""}},""publisher"":{{""@type"":""Organization"",""name"":""ارورسرویس"",""logo"":{{""@type"":""ImageObject"",""url"":""{_baseUrl}/icon-512.png"",""width"":512,""height"":512}}}},""url"":""{_baseUrl}/{cleanPath}""}},{nBreadcrumb}]";
            meta = (nTitle, nDesc, nKeywords, nJsonLd, img, "article");
            return true;
        }

        if (cleanPath.StartsWith("academy/articles/"))
        {
            var a = JsonSerializer.SerializeToNode(entity)!;
            var aTitleText = a["Title"]?.GetValue<string>() ?? "مقاله آموزشی";
            var img = AbsUrl(a["CoverImageUrl"]?.GetValue<string>() ?? "");
            var created = a["CreatedAt"]?.GetValue<DateTime>() ?? DateTime.UtcNow;
            var updated = a["UpdatedAt"]?.GetValue<DateTime>() ?? created;
            var aTitle = SeoTitles.Article(aTitleText);
            var aDesc = SeoDescriptions.Article(a["Summary"]?.GetValue<string>(), aTitleText);
            var aKeywords = $"مقاله آموزشی, {aTitleText}, آموزش تعمیرات";
            var aBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{{""@type"":""ListItem"",""position"":1,""name"":""آکادمی"",""item"":""{_baseUrl}/academy""}},{{""@type"":""ListItem"",""position"":2,""name"":""{EscapeJson(aTitleText)}"",""item"":""{_baseUrl}/{cleanPath}""}}]}}";
            var aJsonLd = $@"[{{""@context"":""https://schema.org"",""@type"":""Article"",""mainEntityOfPage"":{{""@type"":""WebPage"",""@id"":""{_baseUrl}/{cleanPath}""}},""headline"":""{EscapeJson(aTitleText)}"",""description"":""{EscapeJson(aDesc)}"",""image"":[""{img}""],""datePublished"":""{created:yyyy-MM-dd}"",""dateModified"":""{updated:yyyy-MM-dd}"",""inLanguage"":""fa-IR"",""articleSection"":""آکادمی"",""author"":{{""@type"":""Organization"",""name"":""ارورسرویس"",""url"":""{_baseUrl}""}},""publisher"":{{""@type"":""Organization"",""name"":""ارورسرویس"",""logo"":{{""@type"":""ImageObject"",""url"":""{_baseUrl}/icon-512.png"",""width"":512,""height"":512}}}},""url"":""{_baseUrl}/{cleanPath}""}},{aBreadcrumb}]";
            meta = (aTitle, aDesc, aKeywords, aJsonLd, img, "article");
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
                    var lTitle = SeoTitles.Lesson(titleText, courseTitle);
                    var lDesc = SeoDescriptions.Lesson(titleText, courseTitle);
                    var lKeywords = $"آموزش تعمیرات, {titleText}, {courseTitle}";
                    var lBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{{""@type"":""ListItem"",""position"":1,""name"":""آکادمی"",""item"":""{_baseUrl}/academy""}},{{""@type"":""ListItem"",""position"":2,""name"":""{EscapeJson(courseTitle)}"",""item"":""{_baseUrl}/academy/{parts[0]}""}},{{""@type"":""ListItem"",""position"":3,""name"":""{EscapeJson(titleText)}"",""item"":""{_baseUrl}/{cleanPath}""}}]}}";
                    var lJsonLd = $@"[{{""@context"":""https://schema.org"",""@type"":""LearningResource"",""name"":""{EscapeJson(titleText)}"",""description"":""{EscapeJson(lDesc)}"",""learningResourceType"":""Lesson"",""url"":""{_baseUrl}/{cleanPath}""}},{lBreadcrumb}]";
                    meta = (lTitle, lDesc, lKeywords, lJsonLd, "", "article");
                    return true;
                }
            }

            var c = JsonSerializer.SerializeToNode(entity)!;
            var courseName = c["Title"]?.GetValue<string>() ?? "دوره آموزشی";
            var courseImg = AbsUrl(c["CoverImageUrl"]?.GetValue<string>() ?? "");
            var cCreated = c["CreatedAt"]?.GetValue<DateTime>() ?? DateTime.UtcNow;
            var cUpdated = c["UpdatedAt"]?.GetValue<DateTime>() ?? cCreated;
            var cTitle = SeoTitles.Course(courseName);
            var cDesc = SeoDescriptions.Course(c["Summary"]?.GetValue<string>(), courseName);
            var cKeywords = $"دوره آموزشی, {courseName}, آموزش تعمیرات";
            var cBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{{""@type"":""ListItem"",""position"":1,""name"":""آکادمی"",""item"":""{_baseUrl}/academy""}},{{""@type"":""ListItem"",""position"":2,""name"":""{EscapeJson(courseName)}"",""item"":""{_baseUrl}/{cleanPath}""}}]}}";
            var cJsonLd = $@"[{{""@context"":""https://schema.org"",""@type"":""Course"",""name"":""{EscapeJson(courseName)}"",""description"":""{EscapeJson(cDesc)}"",""image"":""{courseImg}"",""datePublished"":""{cCreated:yyyy-MM-dd}"",""dateModified"":""{cUpdated:yyyy-MM-dd}"",""inLanguage"":""fa-IR"",""provider"":{{""@type"":""Organization"",""name"":""ارورسرویس"",""url"":""{_baseUrl}"",""logo"":{{""@type"":""ImageObject"",""url"":""{_baseUrl}/icon-512.png"",""width"":512,""height"":512}}}},""url"":""{_baseUrl}/{cleanPath}""}},{cBreadcrumb}]";
            meta = (cTitle, cDesc, cKeywords, cJsonLd, courseImg, "website");
            return true;
        }

        if (cleanPath.StartsWith("technical/error-codes/"))
        {
            var e = JsonSerializer.SerializeToNode(entity)!;
            var brand = e["Brand"]?.GetValue<string>() ?? "";
            var device = e["DeviceType"]?.GetValue<string>() ?? "";
            var code = e["Code"]?.GetValue<string>() ?? "";
            var img = AbsUrl(e["ImageUrl"]?.GetValue<string>() ?? "");
            var eTitle = SeoTitles.ErrorCode(code, brand, device);
            var eDescription = SeoDescriptions.ErrorCode(code, brand, device,
                e["Description"]?.GetValue<string>(), e["Solution"]?.GetValue<string>());
            var eKeywords = $"کد خطای {code}, {brand}, {device}, عیب‌یابی, تعمیرات";
            var eBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{{""@type"":""ListItem"",""position"":1,""name"":""کدهای خطا"",""item"":""{_baseUrl}/technical/error-codes""}},{{""@type"":""ListItem"",""position"":2,""name"":""کد خطای {EscapeJson(code)} {EscapeJson(brand)} {EscapeJson(device)}"",""item"":""{_baseUrl}/{cleanPath}""}}]}}";
            var eJsonLdParts = new List<string>
            {
                $@"{{""@context"":""https://schema.org"",""@type"":""TechArticle"",""headline"":""{EscapeJson(eTitle)}"",""description"":""{EscapeJson(eDescription)}"",""image"":""{img}"",""proficiencyLevel"":""Beginner"",""dependencies"":""{EscapeJson(brand)} {EscapeJson(device)}"",""url"":""{_baseUrl}/{cleanPath}""}}",
                eBreadcrumb
            };

            var faqs = e["Faqs"]?.AsArray();
            if (faqs is { Count: > 0 })
            {
                var items = faqs
                    .Where(f => !string.IsNullOrWhiteSpace(f?["Question"]?.GetValue<string>())
                             && !string.IsNullOrWhiteSpace(f?["Answer"]?.GetValue<string>()))
                    .Select(f => new Dictionary<string, object?>
                    {
                        ["@type"] = "Question",
                        ["name"] = f!["Question"]!.GetValue<string>(),
                        ["acceptedAnswer"] = new Dictionary<string, object?>
                        {
                            ["@type"] = "Answer",
                            ["text"] = f["Answer"]!.GetValue<string>()
                        }
                    })
                    .ToArray();

                if (items.Length > 0)
                {
                    var faqPage = new Dictionary<string, object?>
                    {
                        ["@context"] = "https://schema.org",
                        ["@type"] = "FAQPage",
                        ["mainEntity"] = items
                    };
                    eJsonLdParts.Add(JsonSerializer.Serialize(faqPage));
                }
            }

            var eJsonLd = $"[{string.Join(",", eJsonLdParts)}]";
            meta = (eTitle, eDescription, eKeywords, eJsonLd, img, "article");
            return true;
        }

        if (cleanPath.StartsWith("technical/sensor-finder/"))
        {
            var s = JsonSerializer.SerializeToNode(entity)!;
            var sType = s["SensorType"]?.GetValue<string>() ?? "";
            var sDevice = s["DeviceName"]?.GetValue<string>() ?? "";
            var seo = SensorSeoText.Build(s["SensorName"]?.GetValue<string>(), sType, s["SizeText"]?.GetValue<string>(), sDevice, s["Notes"]?.GetValue<string>());
            var sImg = AbsUrl(s["FirstImageUrl"]?.GetValue<string>() ?? "");
            var sBreadcrumb = $@"{{""@type"":""BreadcrumbList"",""itemListElement"":[{{""@type"":""ListItem"",""position"":1,""name"":""سنسور یاب"",""item"":""{_baseUrl}/technical/sensor-finder""}},{{""@type"":""ListItem"",""position"":2,""name"":""{EscapeJson(seo.FullName)}"",""item"":""{_baseUrl}/{cleanPath}""}}]}}";
            var sJsonLd = $@"[{{""@context"":""https://schema.org"",""@type"":""Product"",""name"":""{EscapeJson(seo.FullName)}"",""description"":""{EscapeJson(seo.Description)}"",""category"":""{EscapeJson(sType)}"",""brand"":{{""@type"":""Brand"",""name"":""{EscapeJson(sDevice)}""}},""url"":""{_baseUrl}/{cleanPath}""}},{sBreadcrumb}]";
            meta = (seo.Title, seo.Description, seo.Keywords, sJsonLd, sImg, "product");
            return true;
        }

        if (cleanPath.StartsWith("workshops/"))
        {
            var w = JsonSerializer.SerializeToNode(entity)!;
            var wName = J(w, "WorkshopName") ?? "همکار ارورسرویس";
            var owner = J(w, "OwnerFullName") ?? "";
            var city = J(w, "CityName") ?? "";
            var province = J(w, "ProvinceName") ?? "";
            var address = J(w, "Address") ?? "";
            var phone = J(w, "MobileNumber") ?? "";
            var wImg = AbsUrl(J(w, "ImageUrl") ?? "");

            var place = string.Join("، ", new[] { city, province }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .Distinct());

            var wTitle = place.Length == 0
                ? $"{wName} | ارورسرویس"
                : $"{wName} - تعمیر لوازم خانگی {place} | ارورسرویس";

            var descParts = new List<string> { $"پروفایل «{wName}»" };
            if (place.Length > 0) descParts.Add($"همکار ارورسرویس در {place}");
            if (address.Length > 0) descParts.Add(address);
            if (phone.Length > 0) descParts.Add($"تماس: {phone}");
            var wDescRaw = string.Join(" — ", descParts) + ". خدمات تعمیر و سرویس لوازم خانگی در محل.";
            var wDescription = wDescRaw.Length > 165 ? wDescRaw[..165].TrimEnd() + "..." : wDescRaw;

            var kwParts = new List<string> { wName };
            if (place.Length > 0)
            {
                kwParts.Add($"تعمیر لوازم خانگی {place}");
                kwParts.Add($"تعمیرگاه {place}");
                kwParts.Add($"سرویس در منزل {place}");
            }
            kwParts.Add("تعمیر ماشین لباسشویی");
            kwParts.Add("تعمیر یخچال");
            kwParts.Add("ارورسرویس");
            var wKeywords = string.Join(", ", kwParts);

            var localBusiness = new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "LocalBusiness",
                ["name"] = wName,
                ["url"] = $"{_baseUrl}/{cleanPath}",
                ["image"] = wImg,
                ["parentOrganization"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Organization",
                    ["name"] = "ارورسرویس",
                    ["url"] = _baseUrl
                }
            };
            if (phone.Length > 0) localBusiness["telephone"] = phone;

            var wAddress = new Dictionary<string, object?> { ["@type"] = "PostalAddress", ["addressCountry"] = "IR" };
            if (address.Length > 0) wAddress["streetAddress"] = address;
            if (city.Length > 0) wAddress["addressLocality"] = city;
            if (province.Length > 0) wAddress["addressRegion"] = province;
            localBusiness["address"] = wAddress;

            if (place.Length > 0)
                localBusiness["areaServed"] = new Dictionary<string, object?> { ["@type"] = "City", ["name"] = place };

            if (owner.Length > 0)
                localBusiness["founder"] = new Dictionary<string, object?> { ["@type"] = "Person", ["name"] = owner };

            var wBreadcrumb = new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "BreadcrumbList",
                ["itemListElement"] = new object[]
                {
                    new Dictionary<string, object?> { ["@type"] = "ListItem", ["position"] = 1, ["name"] = "خانه", ["item"] = _baseUrl },
                    new Dictionary<string, object?> { ["@type"] = "ListItem", ["position"] = 2, ["name"] = "همکاران ما", ["item"] = $"{_baseUrl}/workshops" },
                    new Dictionary<string, object?> { ["@type"] = "ListItem", ["position"] = 3, ["name"] = wName, ["item"] = $"{_baseUrl}/{cleanPath}" }
                }
            };

            var wJsonLd = $"[{JsonSerializer.Serialize(localBusiness)},{JsonSerializer.Serialize(wBreadcrumb)}]";
            meta = (wTitle, wDescription, wKeywords, wJsonLd, wImg, "website");
            return true;
        }

        return false;
    }

    private string BuildStaticJsonLd(string type, string canonical, string title, string description)
    {
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
            $@"""logo"":{{""@type"":""ImageObject"",""url"":""{_baseUrl}/icon-512.png"",""width"":512,""height"":512}}",
            $@"""image"":""{_baseUrl}/icon-512.png"""
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

        var webSite = $@"{{""@context"":""https://schema.org"",""@type"":""WebSite"",""@id"":""{_baseUrl}/#website"",""name"":""ارورسرویس"",""alternateName"":[""errorservice"",""ErrorService"",""ارور سرویس""],""url"":""{_baseUrl}"",""inLanguage"":""fa-IR"",""publisher"":{{""@id"":""{_baseUrl}/#organization""}},""potentialAction"":{{""@type"":""SearchAction"",""target"":""{_baseUrl}/search?q={{search_term_string}}"",""query-input"":""required name=search_term_string""}}}}";

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

    private string GetFrameworkPreloadLinks()
    {
        if (_frameworkPreloadHtml != null) return _frameworkPreloadHtml;
        lock (_frameworkPreloadLock)
        {
            if (_frameworkPreloadHtml != null) return _frameworkPreloadHtml;
            _frameworkPreloadHtml = "";
            try
            {
                var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
                    ? Path.Combine(_env.ContentRootPath, "wwwroot")
                    : _env.WebRootPath;
                var frameworkDir = Path.Combine(webRoot, "_framework");
                var bootPath = Path.Combine(frameworkDir, "blazor.boot.json");
                var provider = _env.WebRootFileProvider;
                var bootFile = provider?.GetFileInfo("_framework/blazor.boot.json");
                JsonDocument doc;
                if (bootFile != null && bootFile.Exists)
                {
                    using var stream = bootFile.CreateReadStream();
                    doc = JsonDocument.Parse(stream);
                }
                else if (File.Exists(bootPath))
                {
                    doc = JsonDocument.Parse(File.ReadAllText(bootPath));
                }
                else
                {
                    return _frameworkPreloadHtml;
                }

                using var _ = doc;
                bool FrameworkFileExists(string name) =>
                    (provider?.GetFileInfo($"_framework/{name}").Exists ?? false)
                    || File.Exists(Path.Combine(frameworkDir, name));
                if (!doc.RootElement.TryGetProperty("resources", out var resources)) return _frameworkPreloadHtml;

                var sb = new StringBuilder();
                void AddWasm(string key, Func<string, bool>? filter = null)
                {
                    if (!resources.TryGetProperty(key, out var map) || map.ValueKind != JsonValueKind.Object) return;
                    foreach (var item in map.EnumerateObject())
                    {
                        var name = item.Name;
                        if (filter != null && !filter(name)) continue;
                        if (!name.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!FrameworkFileExists(name)) continue;
                        sb.Append($"<link rel=\"preload\" href=\"/_framework/{EscapeHtml(name)}\" as=\"fetch\" type=\"application/wasm\" />");
                    }
                }

                AddWasm("wasmNative");
                AddWasm("coreAssembly", n => n.Contains("System.Private.CoreLib", StringComparison.OrdinalIgnoreCase));
                AddWasm("assembly", n => n.StartsWith("ErrorService.Client", StringComparison.OrdinalIgnoreCase));

                if (resources.TryGetProperty("jsModuleRuntime", out var runtimeMap) && runtimeMap.ValueKind == JsonValueKind.Object)
                {
                    foreach (var item in runtimeMap.EnumerateObject())
                    {
                        if (!item.Name.EndsWith(".js", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!FrameworkFileExists(item.Name)) continue;
                        sb.Append($"<link rel=\"modulepreload\" href=\"/_framework/{EscapeHtml(item.Name)}\" />");
                    }
                }

                _frameworkPreloadHtml = sb.ToString();
            }
            catch
            {
                _frameworkPreloadHtml = "";
            }
            return _frameworkPreloadHtml;
        }
    }

    private static string ApplyMetaTags(string template, string title, string description, string keywords, string canonical, string jsonLd, string ogImage = "", bool noindex = false, string? crawlerContent = null, string? preloadImage = null, string? preloadImageMobile = null, string ogType = "website", string? headExtra = null)
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

        // Keep the single Persian alternate aligned with the page canonical.
        result = Regex.Replace(result,
            @"(<link rel=""alternate"" hreflang=""(?:fa-IR|x-default)"" href="")[^""]*("" />)",
            $"$1{canonical}$2");

        result = Regex.Replace(result,
            @"<meta property=""og:title"" content=""[^""]*"" />",
            $"<meta property=\"og:title\" content=\"{EscapeHtml(title)}\" />");

        result = Regex.Replace(result,
            @"<meta property=""og:description"" content=""[^""]*"" />",
            $"<meta property=\"og:description\" content=\"{EscapeHtml(description)}\" />");

        result = Regex.Replace(result,
            @"<meta property=""og:url"" content=""[^""]*"" />",
            $"<meta property=\"og:url\" content=\"{canonical}\" />");

        result = Regex.Replace(result,
            @"<meta property=""og:type"" content=""[^""]*"" />",
            $"<meta property=\"og:type\" content=\"{EscapeHtml(ogType)}\" />");

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

        if (!string.IsNullOrEmpty(headExtra))
        {
            result = Regex.Replace(result,
                @"(</head>)",
                headExtra + "$1",
                RegexOptions.Singleline);
        }

        if (!string.IsNullOrEmpty(preloadImage))
        {
            var preloadLinks = new StringBuilder();
            if (!string.IsNullOrEmpty(preloadImageMobile))
            {
                preloadLinks.Append($"<link rel=\"preload\" as=\"image\" href=\"{EscapeHtml(preloadImage)}\" media=\"(min-width: 900px)\" fetchpriority=\"high\" />");
                preloadLinks.Append($"<link rel=\"preload\" as=\"image\" href=\"{EscapeHtml(preloadImageMobile)}\" media=\"(max-width: 899.98px)\" fetchpriority=\"high\" />");
            }
            else
            {
                preloadLinks.Append($"<link rel=\"preload\" as=\"image\" href=\"{EscapeHtml(preloadImage)}\" fetchpriority=\"high\" />");
            }

            result = Regex.Replace(result,
                @"(</head>)",
                preloadLinks + "$1",
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

        // فایل سروشده توسط static web assets — وقتی پوشه wwwroot در content root وجود ندارد
        // WebRootPath برابر null است و Path.Combine کرش می‌کند؛ بنابراین از file provider می‌خوانیم.
        var fileInfo = _env.WebRootFileProvider?.GetFileInfo("index.html");
        if (fileInfo != null && fileInfo.Exists)
        {
            using var stream = fileInfo.CreateReadStream();
            using var reader = new StreamReader(stream);
            _indexHtmlTemplate = await reader.ReadToEndAsync();
            return _indexHtmlTemplate;
        }

        foreach (var dir in new[] { _env.WebRootPath, Path.Combine(_env.ContentRootPath ?? "", "wwwroot"), AppContext.BaseDirectory })
        {
            if (string.IsNullOrEmpty(dir)) continue;
            var indexPath = Path.Combine(dir, "index.html");
            if (!File.Exists(indexPath)) continue;
            _indexHtmlTemplate = await File.ReadAllTextAsync(indexPath);
            return _indexHtmlTemplate;
        }

        return null;
    }

    private string AbsUrl(string? url) =>
        string.IsNullOrWhiteSpace(url)
            ? ""
            : (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? url
                : _baseUrl + (url.StartsWith('/') ? url : "/" + url));

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";

        var text = Regex.Replace(html, @"<(script|style)[\s\S]*?</\1>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</(p|div|li|h[1-6]|tr|blockquote|td|th)>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t\u00A0]+", " ");
        text = Regex.Replace(text, @"\n[ \t]+", "\n");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        return text.Trim();
    }

    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string EscapeJson(string text) =>
        text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
}
