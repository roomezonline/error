using ErrorService.Server.Data;
using ErrorService.Server.Infrastructure;
using ErrorService.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ErrorServiceDbContext _context;

    public SearchController(ErrorServiceDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [OutputCache(Duration = 180, VaryByQueryKeys = new[] { "q", "take", "skip" })]
    public async Task<ActionResult<SearchResponseDto>> Get(
        [FromQuery] string q,
        [FromQuery] int take = 8,
        [FromQuery] int skip = 0,
        CancellationToken ct = default)
    {
        q = (q ?? string.Empty).Trim();
        if (q.Length < 2)
            return Ok(new SearchResponseDto());

        take = Math.Clamp(take, 1, 20);
        skip = Math.Clamp(skip, 0, 500);

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDraft)
            .ApplyTextSearch(q)
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(p => new SearchResultItemDto
            {
                Type = "product",
                Id = p.Id,
                Title = p.Name,
                Subtitle = p.Category != null ? p.Category.Name : null,
                Url = $"/products/{p.Id}",
                ImageUrl = p.MainImageUrl
            })
            .ToListAsync(ct);

        var news = await _context.News
            .AsNoTracking()
            .Where(n =>
                n.IsPublished &&
                (n.Title.Contains(q) ||
                 (n.Summary != null && n.Summary.Contains(q)) ||
                 (n.Content != null && n.Content.Contains(q))))
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(n => new SearchResultItemDto
            {
                Type = "news",
                Id = n.Id,
                Title = n.Title,
                Subtitle = n.Summary,
                Url = $"/news/{n.Id}",
                ImageUrl = n.ImageUrl
            })
            .ToListAsync(ct);

        var errorCodes = await _context.ErrorCodes
            .AsNoTracking()
            .Where(e =>
                e.Code.Contains(q) ||
                e.Brand.Contains(q) ||
                e.DeviceType.Contains(q) ||
                e.Description.Contains(q) ||
                (e.TechnicalNotes != null && e.TechnicalNotes.Contains(q)))
            .OrderBy(e => e.Id)
            .Skip(skip)
            .Take(take)
            .Select(e => new SearchResultItemDto
            {
                Type = "error-code",
                Id = e.Id,
                Title = $"{e.Brand} - {e.Code}",
                Subtitle = e.Description,
                Url = $"/technical/error-codes/{Uri.EscapeDataString(e.Brand.Trim().ToLowerInvariant())}/{Uri.EscapeDataString(e.DeviceType.Trim().ToLowerInvariant())}/{Uri.EscapeDataString(e.Code.Trim().ToLowerInvariant())}",
                ImageUrl = e.ImageUrl
            })
            .ToListAsync(ct);

        var courses = await _context.TrainingCourses
            .AsNoTracking()
            .Where(c =>
                c.IsPublished &&
                (c.Title.Contains(q) ||
                 (c.Summary != null && c.Summary.Contains(q))))
            .OrderByDescending(c => c.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(c => new SearchResultItemDto
            {
                Type = "course",
                Id = c.Id,
                Title = c.Title,
                Subtitle = c.Summary,
                Url = $"/academy/{c.Id}",
                ImageUrl = c.CoverImageUrl
            })
            .ToListAsync(ct);

        var articles = await _context.TrainingArticles
            .AsNoTracking()
            .Where(a =>
                a.IsPublished &&
                (a.Title.Contains(q) ||
                 (a.Summary != null && a.Summary.Contains(q))))
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(a => new { a.Id, a.Title, a.Summary, a.Slug, a.CoverImageUrl })
            .ToListAsync(ct);

        var articleItems = articles.Select(a => new SearchResultItemDto
        {
            Type = "article",
            Id = a.Id,
            Title = a.Title,
            Subtitle = a.Summary,
            Url = "/academy/articles/" + (!string.IsNullOrWhiteSpace(a.Slug) ? a.Slug : a.Id.ToString()),
            ImageUrl = a.CoverImageUrl
        }).ToList();

        var workshops = await _context.Workshops
            .AsNoTracking()
            .Where(w =>
                w.IsActive && !w.IsDeleted && w.ShowInCustomersRow &&
                (w.WorkshopName.Contains(q) ||
                 w.OwnerFullName.Contains(q) ||
                 w.Address.Contains(q) ||
                 w.City.Name.Contains(q) ||
                 w.Province.Name.Contains(q)))
            .OrderBy(w => w.WorkshopName)
            .Skip(skip)
            .Take(take)
            .Select(w => new SearchResultItemDto
            {
                Type = "workshop",
                Id = w.Id,
                Title = w.WorkshopName,
                Subtitle = w.City.Name + "، " + w.Province.Name,
                Url = $"/workshops/{w.Id}",
                ImageUrl = w.ImageUrl
            })
            .ToListAsync(ct);

        var sensors = await _context.SensorFinderRecords
            .AsNoTracking()
            .Where(s =>
                s.SensorName.Contains(q) ||
                (s.SensorType != null && s.SensorType.Contains(q)) ||
                (s.SizeText != null && s.SizeText.Contains(q)) ||
                (s.Notes != null && s.Notes.Contains(q)) ||
                s.Device.Name.Contains(q))
            .OrderBy(s => s.SensorName)
            .Skip(skip)
            .Take(take)
            .Select(s => new SearchResultItemDto
            {
                Type = "sensor",
                Id = s.Id,
                Title = s.SensorName,
                Subtitle = s.SensorType == null ? s.Device.Name : s.Device.Name + " • " + s.SensorType,
                Url = $"/technical/sensor-finder?deviceId={s.DeviceId}",
                ImageUrl = s.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
            })
            .ToListAsync(ct);

        var disabledCalculators = CalculatorCatalog.ParseDisabled(
            await _context.SiteSettings.AsNoTracking()
                .Select(s => s.CalculatorsDisabled)
                .FirstOrDefaultAsync(ct));

        var normalizedQuery = SearchNormalizer.Normalize(q);
        var calculators = CalculatorCatalog.Tools
            .Where(t => !disabledCalculators.Contains(t.Key))
            .Where(t => normalizedQuery.Length == 0
                        || SearchNormalizer.Normalize(t.Title).Contains(normalizedQuery)
                        || SearchNormalizer.Normalize(t.ShortDescription).Contains(normalizedQuery)
                        || SearchNormalizer.Normalize(t.Keywords).Contains(normalizedQuery))
            .Skip(skip)
            .Take(take)
            .Select(t => new SearchResultItemDto
            {
                Type = "calculator",
                Id = 0,
                Title = t.Title,
                Subtitle = t.ShortDescription,
                Url = $"/technical/calculators?tool={t.Key}"
            })
            .ToList();

        var faq = await _context.SiteFaqEntries
            .AsNoTracking()
            .Where(f =>
                f.IsEnabled &&
                (f.Question.Contains(q) ||
                 f.Answer.Contains(q) ||
                 (f.Keywords != null && f.Keywords.Contains(q))))
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .Skip(skip)
            .Take(take)
            .Select(f => new SearchResultItemDto
            {
                Type = "faq",
                Id = f.Id,
                Title = f.Question,
                Subtitle = f.Answer,
                Url = $"/faq#faq-item-{f.Id}"
            })
            .ToListAsync(ct);

        return Ok(new SearchResponseDto
        {
            Products = products,
            News = news,
            ErrorCodes = errorCodes,
            Courses = courses,
            Articles = articleItems,
            Workshops = workshops,
            Sensors = sensors,
            Calculators = calculators,
            Faq = faq
        });
    }

    [HttpGet("suggest")]
    [OutputCache(Duration = 120, VaryByQueryKeys = new[] { "q" })]
    public async Task<ActionResult<List<ProductSuggestDto>>> Suggest(
        [FromQuery] string q,
        CancellationToken ct = default)
    {
        q = (q ?? string.Empty).Trim();
        if (q.Length < 2)
            return Ok(new List<ProductSuggestDto>());

        var variants = SearchNormalizer.QueryVariants(q);
        while (variants.Count < 4)
            variants.Add("\u0001");
        var a = variants[0];
        var b = variants[1];
        var c = variants[2];
        var d = variants[3];

        var items = await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDraft &&
                (p.Name.Contains(a) || p.Name.Contains(b) || p.Name.Contains(c) || p.Name.Contains(d) ||
                (p.Sku != null && (p.Sku.Contains(a) || p.Sku.Contains(b) || p.Sku.Contains(c) || p.Sku.Contains(d))) ||
                (p.Slug != null && (p.Slug.Contains(a) || p.Slug.Contains(b) || p.Slug.Contains(c) || p.Slug.Contains(d)))))
            .OrderByDescending(p => p.Name.StartsWith(a) || (p.Sku != null && p.Sku.StartsWith(a)))
            .ThenByDescending(p => p.IsAvailable)
            .ThenByDescending(p => p.CreatedAt)
            .Take(8)
            .Select(p => new ProductSuggestDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                ImageUrl = p.MainImageUrl,
                IsAvailable = p.IsAvailable
            })
            .ToListAsync(ct);

        return Ok(items);
    }
}
