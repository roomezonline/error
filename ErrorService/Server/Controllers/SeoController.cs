using ErrorService.Server.Data;
using ErrorService.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Xml.Linq;

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
    public async Task<IActionResult> GetRobotsTxt()
    {
        var baseUrl = Request.Scheme + "://" + Request.Host;
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
        sb.AppendLine("");
        sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");
        sb.AppendLine($"Sitemap: {baseUrl}/image-sitemap.xml");

        return Content(sb.ToString(), "text/plain", Encoding.UTF8);
    }

    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> GetSitemap()
    {
        var baseUrl = Request.Scheme + "://" + Request.Host;
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        
        var root = new XElement(ns + "urlset");

        // 1. Static Pages (no lastmod - no reliable change date available; omit rather than lie)
        var staticPages = new[] { "/", "/products", "/academy", "/technical/error-codes", "/contact", "/about", "/faq", "/privacy", "/terms", "/news", "/technical/sensor-finder", "/technical/calculators", "/trust/portfolio", "/admission/repair", "/admission/expertise", "/technical/consultation" };
        foreach (var page in staticPages)
        {
            root.Add(CreateUrlElement(ns, baseUrl + page, null, "daily", 0.9));
        }

        // 2. Products
        var products = await _db.Products.Where(p => p.IsAvailable).Select(p => new { p.Id, p.Slug, p.UpdatedAt }).ToListAsync();
        foreach (var p in products)
        {
            var lastMod = p.UpdatedAt.UtcDateTime;
            root.Add(CreateUrlElement(ns, $"{baseUrl}/products/{p.Slug ?? p.Id.ToString()}", lastMod, "weekly", 0.8));
        }

        // 3. News
        var news = await _db.News.Where(n => n.IsPublished).Select(n => new { n.Id, n.Slug, n.UpdatedAt }).ToListAsync();
        foreach (var n in news)
        {
            var lastMod = n.UpdatedAt.UtcDateTime;
            root.Add(CreateUrlElement(ns, $"{baseUrl}/news/{n.Slug ?? n.Id.ToString()}", lastMod, "monthly", 0.7));
        }

        // 4. Academy (Courses)
        var courses = await _db.TrainingCourses.Where(c => c.IsPublished).Select(c => new { c.Id, c.Slug, c.UpdatedAt }).ToListAsync();
        foreach (var c in courses)
        {
            var lastMod = c.UpdatedAt.UtcDateTime;
            root.Add(CreateUrlElement(ns, $"{baseUrl}/academy/{c.Slug ?? c.Id.ToString()}", lastMod, "weekly", 0.8));
        }

        // 5. Academy (Articles)
        var articles = await _db.TrainingArticles.Where(a => a.IsPublished).Select(a => new { a.Id, a.Slug, a.UpdatedAt }).ToListAsync();
        foreach (var a in articles)
        {
            var lastMod = a.UpdatedAt.UtcDateTime;
            root.Add(CreateUrlElement(ns, $"{baseUrl}/academy/articles/{a.Slug ?? a.Id.ToString()}", lastMod, "monthly", 0.7));
        }

        // 6. Academy (Lessons within courses)
        var lessons = await _db.TrainingLessons
            .Where(l => l.Course.IsPublished && l.IsPublished)
            .Select(l => new { l.Id, l.CourseId, l.UpdatedAt })
            .ToListAsync();
        foreach (var l in lessons)
        {
            var lastMod = l.UpdatedAt.UtcDateTime;
            root.Add(CreateUrlElement(ns, $"{baseUrl}/academy/{l.CourseId}/{l.Id}", lastMod, "weekly", 0.7));
        }

        // 7. Error Codes - individual pages (no lastmod - ErrorCode has no reliable change date)
        //    Code is lowercased here to exactly match the canonical URL produced by the SEO middleware,
        //    avoiding duplicate "/F1" vs "/f1" variants.
        var errorCodes = await _db.ErrorCodes
            .Select(e => new { e.Id, e.Brand, e.DeviceType, e.Code })
            .ToListAsync();
        foreach (var e in errorCodes)
        {
            var brand = Uri.EscapeDataString(e.Brand.Trim());
            var device = Uri.EscapeDataString(e.DeviceType.Trim());
            var code = Uri.EscapeDataString(e.Code.Trim().ToLowerInvariant());
            root.Add(CreateUrlElement(ns, $"{baseUrl}/technical/error-codes/{brand}/{device}/{code}", null, "monthly", 0.7));
        }

        // 8. Sensor Finder - individual pages
        var sensorRecords = await _db.SensorFinderRecords
            .Select(s => new { s.Id, s.CreatedAt })
            .ToListAsync();
        foreach (var s in sensorRecords)
        {
            root.Add(CreateUrlElement(ns, $"{baseUrl}/technical/sensor-finder/{s.Id}", s.CreatedAt, "monthly", 0.6));
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return Content(doc.ToString(), "application/xml", Encoding.UTF8);
    }

    [HttpGet("image-sitemap.xml")]
    public async Task<IActionResult> GetImageSitemap()
    {
        var baseUrl = Request.Scheme + "://" + Request.Host;
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace imgNs = "http://www.google.com/schemas/sitemap-image/1.1";

        var root = new XElement(ns + "urlset",
            new XAttribute(XNamespace.Xmlns + "image", imgNs));

        // Product images
        var products = await _db.Products
            .Where(p => p.IsAvailable && !string.IsNullOrEmpty(p.MainImageUrl))
            .Select(p => new { p.Id, p.Slug, p.MainImageUrl })
            .ToListAsync();
        foreach (var p in products)
        {
            var urlElem = new XElement(ns + "url",
                new XElement(ns + "loc", $"{baseUrl}/products/{p.Slug ?? p.Id.ToString()}"),
                new XElement(imgNs + "image",
                    new XElement(imgNs + "loc", p.MainImageUrl.StartsWith("http") ? p.MainImageUrl : $"{baseUrl}{p.MainImageUrl}")));
            root.Add(urlElem);
        }

        // News images
        var news = await _db.News
            .Where(n => n.IsPublished && !string.IsNullOrEmpty(n.ImageUrl))
            .Select(n => new { n.Id, n.Slug, n.ImageUrl })
            .ToListAsync();
        foreach (var n in news)
        {
            var urlElem = new XElement(ns + "url",
                new XElement(ns + "loc", $"{baseUrl}/news/{n.Slug ?? n.Id.ToString()}"),
                new XElement(imgNs + "image",
                    new XElement(imgNs + "loc", n.ImageUrl.StartsWith("http") ? n.ImageUrl : $"{baseUrl}{n.ImageUrl}")));
            root.Add(urlElem);
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return Content(doc.ToString(), "application/xml", Encoding.UTF8);
    }

    private XElement CreateUrlElement(XNamespace ns, string loc, DateTime? lastMod, string changefreq, double priority)
    {
        var url = new XElement(ns + "url",
            new XElement(ns + "loc", loc));

        if (lastMod.HasValue)
        {
            url.Add(new XElement(ns + "lastmod", lastMod.Value.ToString("yyyy-MM-dd")));
        }

        url.Add(
            new XElement(ns + "changefreq", changefreq),
            new XElement(ns + "priority", priority.ToString("F1"))
        );

        return url;
    }
}
