using ErrorService.Server.Data;
using ErrorService.Shared;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<ActionResult<SearchResponseDto>> Get([FromQuery] string q, [FromQuery] int take = 8)
    {
        q = (q ?? string.Empty).Trim();
        if (q.Length < 2)
            return Ok(new SearchResponseDto());

        take = Math.Clamp(take, 1, 20);

        var productsQuery = _context.Products
            .Include(p => p.Category)
            .AsNoTracking()
            .Where(p =>
                p.Name.Contains(q) ||
                (p.Description != null && p.Description.Contains(q)) ||
                (p.Category != null && p.Category.Name.Contains(q)));

        var newsQuery = _context.News
            .AsNoTracking()
            .Where(n =>
                n.IsPublished &&
                (n.Title.Contains(q) ||
                 (n.Summary != null && n.Summary.Contains(q)) ||
                 (n.Content != null && n.Content.Contains(q))));

        var products = await productsQuery
            .OrderByDescending(p => p.CreatedAt)
            .Take(take)
            .Select(p => new SearchResultItemDto
            {
                Type = "product",
                Id = p.Id,
                Title = p.Name,
                Subtitle = p.Category != null ? p.Category.Name : null,
                Url = $"/products/{p.Id}"
            })
            .ToListAsync();

        var news = await newsQuery
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new SearchResultItemDto
            {
                Type = "news",
                Id = n.Id,
                Title = n.Title,
                Subtitle = n.Summary,
                Url = $"/news/{n.Id}"
            })
            .ToListAsync();

        var errorCodesQuery = _context.ErrorCodes
            .AsNoTracking()
            .Where(e =>
                e.Code.Contains(q) ||
                e.Brand.Contains(q) ||
                e.DeviceType.Contains(q) ||
                e.Description.Contains(q) ||
                (e.TechnicalNotes != null && e.TechnicalNotes.Contains(q)));

        var coursesQuery = _context.TrainingCourses
            .AsNoTracking()
            .Where(c =>
                c.IsPublished &&
                (c.Title.Contains(q) ||
                 (c.Summary != null && c.Summary.Contains(q))));

        var errorCodes = await errorCodesQuery
            .Take(take)
            .Select(e => new SearchResultItemDto
            {
                Type = "error-code",
                Id = e.Id,
                Title = $"{e.Brand} - {e.Code}",
                Subtitle = e.Description,
                Url = "/technical/error-codes"
            })
            .ToListAsync();

        var courses = await coursesQuery
            .OrderByDescending(c => c.CreatedAt)
            .Take(take)
            .Select(c => new SearchResultItemDto
            {
                Type = "course",
                Id = c.Id,
                Title = c.Title,
                Subtitle = c.Summary,
                Url = $"/academy/{c.Id}"
            })
            .ToListAsync();

        return Ok(new SearchResponseDto
        {
            Products = products,
            News = news,
            ErrorCodes = errorCodes,
            Courses = courses
        });
    }
}
