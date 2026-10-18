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

        return Ok(new SearchResponseDto
        {
            Products = products,
            News = news,
            ErrorCodes = errorCodes,
            Courses = courses
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
