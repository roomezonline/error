using ErrorService.Server.Data;
using ErrorService.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

/// <summary>
/// رفع نوع و عنوان مقصد لینک برای کارت «بیشتر مطالعه کنید».
/// </summary>
[ApiController]
[Route("api/linkmeta")]
public sealed class LinkMetaController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public LinkMetaController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    public sealed record LinkMetaDto(string Type, string TypeLabel, string Title, string Url);

    private static readonly Dictionary<string, string> StaticPages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/"] = "صفحه اصلی",
        ["/products"] = "فروشگاه",
        ["/academy"] = "آکادمی",
        ["/news"] = "اخبار",
        ["/technical/error-codes"] = "بانک کدهای خطا",
        ["/technical/calculators"] = "ابزارهای محاسباتی",
        ["/technical/consultation"] = "مشاوره فنی",
        ["/technical/sensor-finder"] = "سنسوریاب",
        ["/about"] = "درباره ما",
        ["/contact"] = "تماس با ما",
        ["/faq"] = "پرسش‌های متداول",
        ["/help/faq"] = "پرسش‌های متداول",
        ["/workshops"] = "کارگاه‌های تعمیر",
        ["/commitment"] = "تعهدنامه",
        ["/privacy"] = "حریم خصوصی",
        ["/terms"] = "قوانین و مقررات",
        ["/trust/portfolio"] = "نمونه کارها",
        ["/admission/repair"] = "پذیرش تعمیر",
        ["/admission/expertise"] = "درخواست کارشناسی",
        ["/admission/track"] = "پیگیری درخواست",
        ["/order-tracking"] = "پیگیری سفارش",
        ["/compare"] = "مقایسه محصولات",
        ["/cart"] = "سبد خرید",
        ["/checkout"] = "تسویه حساب",
    };

    [HttpGet]
    [OutputCache(Duration = 600, VaryByQueryKeys = new[] { "*" })]
    public async Task<ActionResult<LinkMetaDto>> Resolve([FromQuery] string? url, CancellationToken cancellationToken)
    {
        var input = (url ?? string.Empty).Trim();
        if (input.Length == 0 || input.Length > 1000)
            return BadRequest();

        if (input.StartsWith('#'))
            return Ok(new LinkMetaDto("anchor", string.Empty, string.Empty, input));

        var baseUri = new Uri($"{Request.Scheme}://{Request.Host}");
        if (!Uri.TryCreate(baseUri, input, out var uri))
            return Ok(new LinkMetaDto("unknown", string.Empty, string.Empty, input));

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return Ok(new LinkMetaDto("contact", string.Empty, string.Empty, uri.ToString()));

        if (!string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
            return Ok(new LinkMetaDto("external", "لینک خارجی", host, uri.ToString()));
        }

        var path = uri.AbsolutePath;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
            return Ok(new LinkMetaDto("page", "صفحه سایت", "صفحه اصلی", "/"));

        var key = segments.Select(x => x.ToLowerInvariant()).ToArray();
        var title = await ResolveAsync(key, segments, cancellationToken);

        if (title != null)
            return Ok(title);

        var cleanPath = "/" + string.Join('/', segments);
        if (StaticPages.TryGetValue(cleanPath, out var staticTitle))
            return Ok(new LinkMetaDto("page", "صفحه سایت", staticTitle, cleanPath));

        return Ok(new LinkMetaDto("unknown", string.Empty, string.Empty, cleanPath));
    }

    private async Task<LinkMetaDto?> ResolveAsync(string[] key, string[] original, CancellationToken cancellationToken)
    {
        if (key.Length == 2 && key[0] == "products")
        {
            var product = await FindProductAsync(key[1], cancellationToken);
            return product == null ? null : new LinkMetaDto("product", "محصول", product, "/products/" + original[1]);
        }

        if (key.Length == 2 && key[0] == "news")
        {
            var news = await FindNewsAsync(key[1], cancellationToken);
            return news == null ? null : new LinkMetaDto("news", "خبر مرتبط", news, "/news/" + original[1]);
        }

        if (key.Length == 2 && key[0] == "academy")
        {
            var course = await FindCourseAsync(key[1], cancellationToken);
            return course == null ? null : new LinkMetaDto("course", "دوره آموزشی", course, "/academy/" + original[1]);
        }

        if (key.Length == 3 && key[0] == "academy" && key[1] == "articles")
        {
            var article = await FindArticleAsync(key[2], cancellationToken);
            return article == null ? null : new LinkMetaDto("article", "مقاله آموزشی", article, "/academy/articles/" + original[2]);
        }

        if (key.Length == 3 && key[0] == "academy")
        {
            var course = await FindCourseAsync(key[1], cancellationToken);
            return course == null ? null : new LinkMetaDto("course", "دوره آموزشی", course, "/academy/" + original[1] + "/" + original[2]);
        }

        if (key.Length == 5 && key[0] == "technical" && key[1] == "error-codes")
        {
            var brand = original[2];
            var device = original[3];
            var code = original[4];
            var exists = await _db.ErrorCodes
                .AsNoTracking()
                .Where(e => e.Brand.ToLower() == brand.ToLower()
                            && e.DeviceType.ToLower() == device.ToLower()
                            && e.Code.ToLower() == code.ToLower())
                .Select(e => e.Description)
                .FirstOrDefaultAsync(cancellationToken);

            if (exists == null)
                return null;

            var title = string.IsNullOrWhiteSpace(exists) ? $"کد خطای {code}" : $"کد خطای {code} – {exists}";
            if (title.Length > 120) title = title[..120];
            return new LinkMetaDto("errorcode", "کد خطا", title, "/technical/error-codes/" + original[2] + "/" + original[3] + "/" + original[4]);
        }

        return null;
    }

    private static bool TryInt(string value, out int id) => int.TryParse(value, out id);

    private async Task<string?> FindProductAsync(string key, CancellationToken cancellationToken)
    {
        var query = _db.Products.AsNoTracking().Where(p => !p.IsDraft);
        var name = TryInt(key, out var id)
            ? await query.Where(p => p.Id == id).Select(p => p.Name).FirstOrDefaultAsync(cancellationToken)
            : await query.Where(p => p.Slug != null && p.Slug.ToLower() == key)
                        .Select(p => p.Name)
                        .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    private async Task<string?> FindNewsAsync(string key, CancellationToken cancellationToken)
    {
        var query = _db.News.AsNoTracking().Where(n => n.IsPublished);
        var title = TryInt(key, out var id)
            ? await query.Where(n => n.Id == id).Select(n => n.Title).FirstOrDefaultAsync(cancellationToken)
            : await query.Where(n => n.Slug != null && n.Slug.ToLower() == key)
                        .Select(n => n.Title)
                        .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }

    private async Task<string?> FindArticleAsync(string key, CancellationToken cancellationToken)
    {
        var query = _db.TrainingArticles.AsNoTracking().Where(a => a.IsPublished);
        var title = TryInt(key, out var id)
            ? await query.Where(a => a.Id == id).Select(a => a.Title).FirstOrDefaultAsync(cancellationToken)
            : await query.Where(a => a.Slug != null && a.Slug.ToLower() == key)
                        .Select(a => a.Title)
                        .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }

    private async Task<string?> FindCourseAsync(string key, CancellationToken cancellationToken)
    {
        var query = _db.TrainingCourses.AsNoTracking().Where(c => c.IsPublished);
        var title = TryInt(key, out var id)
            ? await query.Where(c => c.Id == id).Select(c => c.Title).FirstOrDefaultAsync(cancellationToken)
            : await query.Where(c => c.Slug != null && c.Slug.ToLower() == key)
                        .Select(c => c.Title)
                        .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }
}
