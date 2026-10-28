using ErrorService.Server.Data;
using ErrorService.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("")]
public sealed class SeoController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IConfiguration _config;

    public SeoController(ErrorServiceDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpGet("robots.txt")]
    [HttpHead("robots.txt")]
    public async Task<IActionResult> GetRobotsTxt()
    {
        var baseUrl = GetBaseUrl();
        // In Blazor WASM Hosted, the template is usually in the Client's wwwroot or served as static file.
        // We generate it dynamically to be safe and accurate.
        
        var sb = new StringBuilder();
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Allow: /");
        sb.AppendLine("Disallow: /admin/");
        sb.AppendLine("Disallow: /api/");
        sb.AppendLine("Disallow: /auth/");
        sb.AppendLine("Disallow: /login");
        sb.AppendLine("Disallow: /register");
        sb.AppendLine("Disallow: /profile");
        sb.AppendLine("Disallow: /my-orders");
        sb.AppendLine("Disallow: /wishlist");
        sb.AppendLine("Disallow: /cart");
        sb.AppendLine("Disallow: /checkout");
        sb.AppendLine("Disallow: /order-tracking");
        sb.AppendLine("Disallow: /search");
        sb.AppendLine("Disallow: /monitoring/");
        sb.AppendLine("Disallow: /payment/");
        sb.AppendLine("");
        sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");
        sb.AppendLine($"Sitemap: {baseUrl}/image-sitemap.xml");
        sb.AppendLine($"Sitemap: {baseUrl}/news-sitemap.xml");

        return PlainTextResult(sb.ToString(), "text/plain; charset=utf-8");
    }

    [HttpGet("sitemap.xml")]
    [HttpHead("sitemap.xml")]
    public async Task<IActionResult> GetSitemap()
    {
        var baseUrl = GetBaseUrl();
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        
        var root = new XElement(ns + "urlset");

        // 1. Static Pages (no lastmod - no reliable change date available; omit rather than lie)
        var staticPages = new[] { "/", "/products", "/academy", "/technical/error-codes", "/workshops", "/contact", "/about", "/faq", "/privacy", "/terms", "/news", "/technical/sensor-finder", "/technical/calculators", "/trust/portfolio", "/admission/repair", "/admission/expertise", "/technical/consultation", "/commitment" };
        foreach (var page in staticPages)
        {
            root.Add(CreateUrlElement(ns, baseUrl + page, null, "weekly", page is "/" or "/products" or "/academy" or "/technical/error-codes" ? 0.9 : 0.6));
        }

        // 2. Products
        var products = await _db.Products.Where(p => p.IsAvailable && !p.IsDraft).Select(p => new { p.Id, p.Slug, p.UpdatedAt }).ToListAsync();
        foreach (var p in products)
        {
            var lastMod = p.UpdatedAt.UtcDateTime;
            root.Add(CreateUrlElement(ns, $"{baseUrl}/products/{EncodeSegment(p.Slug ?? p.Id.ToString())}", lastMod, "weekly", 0.8));
        }

        // 3. News
        var news = await _db.News.Where(n => n.IsPublished).Select(n => new { n.Id, n.Slug, n.UpdatedAt }).ToListAsync();
        foreach (var n in news)
        {
            var lastMod = n.UpdatedAt.UtcDateTime;
            root.Add(CreateUrlElement(ns, $"{baseUrl}/news/{EncodeSegment(n.Slug ?? n.Id.ToString())}", lastMod, "monthly", 0.7));
        }

        // 4. Academy (Courses)
        var courses = await _db.TrainingCourses.Where(c => c.IsPublished).Select(c => new { c.Id, c.Slug, c.UpdatedAt }).ToListAsync();
        foreach (var c in courses)
        {
            var lastMod = c.UpdatedAt.UtcDateTime;
            root.Add(CreateUrlElement(ns, $"{baseUrl}/academy/{EncodeSegment(c.Slug ?? c.Id.ToString())}", lastMod, "weekly", 0.8));
        }

        // 5. Academy (Articles)
        var articles = await _db.TrainingArticles.Where(a => a.IsPublished).Select(a => new { a.Id, a.Slug, a.UpdatedAt }).ToListAsync();
        foreach (var a in articles)
        {
            var lastMod = a.UpdatedAt.UtcDateTime;
            root.Add(CreateUrlElement(ns, $"{baseUrl}/academy/articles/{EncodeSegment(a.Slug ?? a.Id.ToString())}", lastMod, "monthly", 0.7));
        }

        // Lessons are intentionally not included: premium/lesson URLs are low-level
        // content pages and can be gated or duplicate the canonical course page.

        // 6. Error Codes - individual pages (no lastmod - ErrorCode has no reliable change date)
        //    All segments are lowercased to exactly match the canonical URL produced by the SEO middleware,
        //    avoiding duplicate "/F1" vs "/f1" or "/Bosch" vs "/bosch" variants.
        var errorCodes = await _db.ErrorCodes
            .Select(e => new { e.Brand, e.DeviceType, e.Code })
            .Distinct()
            .ToListAsync();
        foreach (var e in errorCodes)
        {
            var brand = EncodeSegment(e.Brand.Trim().ToLowerInvariant());
            var device = EncodeSegment(e.DeviceType.Trim().ToLowerInvariant());
            var code = EncodeSegment(e.Code.Trim().ToLowerInvariant());
            root.Add(CreateUrlElement(ns, $"{baseUrl}/technical/error-codes/{brand}/{device}/{code}", null, "monthly", 0.7));
        }

        // 7. Sensor Finder - individual pages
        var sensorRecords = await _db.SensorFinderRecords
            .Select(s => new { s.Id, s.CreatedAt })
            .ToListAsync();
        foreach (var s in sensorRecords)
        {
            root.Add(CreateUrlElement(ns, $"{baseUrl}/technical/sensor-finder/{s.Id}", s.CreatedAt, "monthly", 0.6));
        }

        // 8) Workshops - all partner profiles (active and inactive), excluding soft-deleted
        var workshops = await _db.Workshops.AsNoTracking()
            .Where(w => !w.IsDeleted)
            .Select(w => new { w.Id, w.CreatedAt })
            .ToListAsync();
        foreach (var w in workshops)
        {
            root.Add(CreateUrlElement(ns, $"{baseUrl}/workshops/{w.Id}", w.CreatedAt, "monthly", 0.6));
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return XmlResult(doc);
    }

    [HttpGet("news-sitemap.xml")]
    [HttpHead("news-sitemap.xml")]
    public async Task<IActionResult> GetNewsSitemap()
    {
        var baseUrl = GetBaseUrl();
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace newsNs = "http://www.google.com/schemas/sitemap-news/0.9";

        var root = new XElement(ns + "urlset", new XAttribute(XNamespace.Xmlns + "news", newsNs));

        // Google only accepts news entries from the last 48 hours, and it rejects
        // an empty <urlset>. Fall back to the latest articles so the sitemap
        // stays parseable while no fresh article has been published.
        var since = DateTimeOffset.UtcNow.AddDays(-2);
        var published = _db.News.AsNoTracking().Where(n => n.IsPublished);
        var items = await published
            .Where(n => n.CreatedAt >= since)
            .OrderByDescending(n => n.CreatedAt)
            .Take(1000)
            .Select(n => new { n.Id, n.Slug, n.Title, n.Tags, n.CreatedAt })
            .ToListAsync();

        if (items.Count == 0)
        {
            items = await published
                .OrderByDescending(n => n.CreatedAt)
                .Take(20)
                .Select(n => new { n.Id, n.Slug, n.Title, n.Tags, n.CreatedAt })
                .ToListAsync();
        }

        if (items.Count == 0)
            return NotFound();

        foreach (var n in items)
        {
            var loc = $"{baseUrl}/news/{EncodeSegment(n.Slug ?? n.Id.ToString())}";
            var news = new XElement(newsNs + "news",
                new XElement(newsNs + "publication",
                    new XElement(newsNs + "name", "ارورسرویس"),
                    new XElement(newsNs + "language", "fa")),
                new XElement(newsNs + "publication_date", n.CreatedAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
                new XElement(newsNs + "title", n.Title));

            if (!string.IsNullOrWhiteSpace(n.Tags))
                news.Add(new XElement(newsNs + "keywords", n.Tags));

            root.Add(new XElement(ns + "url", new XElement(ns + "loc", loc), news));
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return XmlResult(doc);
    }

    [HttpGet("image-sitemap.xml")]
    [HttpHead("image-sitemap.xml")]
    public async Task<IActionResult> GetImageSitemap()
    {
        var baseUrl = GetBaseUrl();
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace imgNs = "http://www.google.com/schemas/sitemap-image/1.1";

        var root = new XElement(ns + "urlset",
            new XAttribute(XNamespace.Xmlns + "image", imgNs));

        // فقط تصاویری وارد می‌شوند که واقعاً در صفحهٔ مقصد رندر می‌شوند؛
        // گوگل تصویرِ خارج از صفحه را نادیده می‌گیرد و از دقت سایت‌مپ می‌کاهد.
        void AddEntry(string loc, DateTimeOffset? lastMod, string changefreq, double priority, string pageTitle, params string?[] imageUrls)
        {
            var images = imageUrls
                .Select(u => AbsoluteUrl(baseUrl, u))
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (images.Count == 0) return;

            var urlElem = new XElement(ns + "url", new XElement(ns + "loc", loc));
            if (lastMod.HasValue)
                urlElem.Add(new XElement(ns + "lastmod", lastMod.Value.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

            var title = Truncate(pageTitle, 150);
            foreach (var image in images)
            {
                var imageElem = new XElement(imgNs + "image", new XElement(imgNs + "loc", image));
                if (title.Length > 0) imageElem.Add(new XElement(imgNs + "title", title));
                urlElem.Add(imageElem);
            }

            urlElem.Add(
                new XElement(ns + "changefreq", changefreq),
                new XElement(ns + "priority", priority.ToString("F1", CultureInfo.InvariantCulture)));
            root.Add(urlElem);
        }

        // 1) محصولات: تصویر اصلی + گالری (هر چهارتا در گالری صفحهٔ محصول نمایش داده می‌شوند)
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsAvailable && !p.IsDraft)
            .Select(p => new { p.Id, p.Slug, p.Name, p.MainImageUrl, p.ImageUrl2, p.ImageUrl3, p.ImageUrl4, p.UpdatedAt })
            .ToListAsync();
        foreach (var p in products)
            AddEntry($"{baseUrl}/products/{EncodeSegment(p.Slug ?? p.Id.ToString())}", p.UpdatedAt, "weekly", 0.8, p.Name,
                p.MainImageUrl, p.ImageUrl2, p.ImageUrl3, p.ImageUrl4);

        // 2) اخبار
        var news = await _db.News.AsNoTracking()
            .Where(n => n.IsPublished)
            .Select(n => new { n.Id, n.Slug, n.Title, n.ImageUrl, n.UpdatedAt })
            .ToListAsync();
        foreach (var n in news)
            AddEntry($"{baseUrl}/news/{EncodeSegment(n.Slug ?? n.Id.ToString())}", n.UpdatedAt, "monthly", 0.7, n.Title, n.ImageUrl);

        // 3) آکادمی — دوره‌ها (کاور در صفحهٔ دوره رندر می‌شود)
        var courses = await _db.TrainingCourses.AsNoTracking()
            .Where(c => c.IsPublished)
            .Select(c => new { c.Id, c.Slug, c.Title, c.CoverImageUrl, c.UpdatedAt })
            .ToListAsync();
        foreach (var c in courses)
            AddEntry($"{baseUrl}/academy/{EncodeSegment(c.Slug ?? c.Id.ToString())}", c.UpdatedAt, "weekly", 0.8, c.Title, c.CoverImageUrl);

        // 4) آکادمی — مقالات
        var articles = await _db.TrainingArticles.AsNoTracking()
            .Where(a => a.IsPublished)
            .Select(a => new { a.Id, a.Slug, a.Title, a.CoverImageUrl, a.UpdatedAt })
            .ToListAsync();
        foreach (var a in articles)
            AddEntry($"{baseUrl}/academy/articles/{EncodeSegment(a.Slug ?? a.Id.ToString())}", a.UpdatedAt, "monthly", 0.7, a.Title, a.CoverImageUrl);

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return XmlResult(doc);
    }

    /// <summary>مسیر تصویر را به URL مطلق تبدیل می‌کند (شامل حالت scheme-relative).</summary>
    private static string? AbsoluteUrl(string baseUrl, string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var value = url.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return value;
        if (value.StartsWith("//", StringComparison.Ordinal))
            return "https:" + value;
        return baseUrl + (value.StartsWith('/') ? value : "/" + value);
    }

    private static string Truncate(string? value, int max)
    {
        var s = (value ?? string.Empty).Trim();
        return s.Length <= max ? s : s[..max];
    }

    /// <summary>
    /// خروجی XML با declaration و هدر کش؛ برای HEAD فقط هدرها برمی‌گردند (بدون بدنه)
    /// تا ابزارهایی که با HEAD تست می‌کنند به‌جای ۴۰۴، ۲۰۰ با Content-Length درست ببینند.
    /// </summary>
    private IActionResult XmlResult(XDocument doc)
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            using (var xw = XmlWriter.Create(ms, new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            }))
            {
                doc.Save(xw);
            }
            bytes = ms.ToArray();
        }

        Response.Headers["Cache-Control"] = "public, max-age=900";

        if (HttpMethods.IsHead(Request.Method))
        {
            Response.ContentType = "application/xml; charset=utf-8";
            Response.ContentLength = bytes.Length;
            return new EmptyResult();
        }

        return File(bytes, "application/xml; charset=utf-8");
    }

    private IActionResult PlainTextResult(string text, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        Response.Headers["Cache-Control"] = "public, max-age=900";

        if (HttpMethods.IsHead(Request.Method))
        {
            Response.ContentType = contentType;
            Response.ContentLength = bytes.Length;
            return new EmptyResult();
        }

        return File(bytes, contentType);
    }

    private XElement CreateUrlElement(XNamespace ns, string loc, DateTime? lastMod, string changefreq, double priority)
    {
        var url = new XElement(ns + "url",
            new XElement(ns + "loc", loc));

        if (lastMod.HasValue)
        {
            url.Add(new XElement(ns + "lastmod", lastMod.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }

        url.Add(
            new XElement(ns + "changefreq", changefreq),
            new XElement(ns + "priority", priority.ToString("F1", CultureInfo.InvariantCulture))
        );

        return url;
    }

    private string GetBaseUrl()
    {
        var configured = _config["Site:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');

        return $"{Request.Scheme}://{Request.Host}".TrimEnd('/');
    }

    private static string EncodeSegment(string value) => Uri.EscapeDataString(value.Trim());
}
