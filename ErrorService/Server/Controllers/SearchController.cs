using ErrorService.Server.Data;
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
    [OutputCache(Duration = 180, VaryByQueryKeys = new[] { "q", "take" })]
    public async Task<ActionResult<SearchResponseDto>> Get(
        [FromQuery] string q,
        [FromQuery] int take = 8,
        CancellationToken ct = default)
    {
        q = (q ?? string.Empty).Trim();
        if (q.Length < 2)
            return Ok(new SearchResponseDto());

        take = Math.Clamp(take, 1, 20);

        var products = await _context.Products
            .AsNoTracking()
            .Where(p =>
                p.Name.Contains(q) ||
                (p.Description != null && p.Description.Contains(q)) ||
                (p.Category != null && p.Category.Name.Contains(q)))
            .OrderByDescending(p => p.CreatedAt)
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
}
