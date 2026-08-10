using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrustController : ControllerBase
{
    private readonly ErrorServiceDbContext _context;

    public TrustController(ErrorServiceDbContext context)
    {
        _context = context;
    }

    [HttpGet("testimonials")]
    [OutputCache(Duration = 3600)]
    public async Task<ActionResult<IEnumerable<TestimonialDto>>> GetTestimonials()
    {
        return await _context.Testimonials
            .Where(t => t.IsApproved)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TestimonialDto
            {
                Id = t.Id,
                CustomerName = t.CustomerName,
                CustomerRole = t.CustomerRole,
                Content = t.Content,
                Rating = t.Rating,
                CreatedAt = t.CreatedAt
            }).ToListAsync();
    }

    [HttpGet("portfolio")]
    [OutputCache(Duration = 3600)]
    public async Task<ActionResult<IEnumerable<PortfolioProjectDto>>> GetPortfolio()
    {
        return await _context.PortfolioProjects
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.CompletionDate)
            .Select(p => new PortfolioProjectDto
            {
                Id = p.Id,
                Title = p.Title,
                Description = p.Description,
                BeforeImageUrl = p.BeforeImageUrl,
                AfterImageUrl = p.AfterImageUrl,
                DeviceType = p.DeviceType,
                Brand = p.Brand,
                CompletionDate = p.CompletionDate
            }).ToListAsync();
    }

    [HttpPost("testimonials")]
    public async Task<IActionResult> PostTestimonial(TestimonialDto dto)
    {
        var testimonial = new Testimonial
        {
            CustomerName = dto.CustomerName,
            CustomerRole = dto.CustomerRole,
            Content = dto.Content,
            Rating = dto.Rating,
            IsApproved = false, // Needs admin approval
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.Testimonials.Add(testimonial);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [Authorize(Policy = "perm:admin.trust.testimonials.manage")]
    [HttpGet("admin/testimonials")]
    public async Task<ActionResult<IEnumerable<object>>> GetAdminTestimonials()
    {
        var list = await _context.Testimonials
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.CustomerName,
                t.CustomerRole,
                t.Content,
                t.Rating,
                t.CreatedAt,
                t.IsApproved
            })
            .ToListAsync();

        return Ok(list);
    }

    [Authorize(Policy = "perm:admin.trust.testimonials.manage")]
    [HttpPut("admin/testimonials/{id:int}/approve")]
    public async Task<IActionResult> ToggleApproveTestimonial(int id)
    {
        var t = await _context.Testimonials.FindAsync(id);
        if (t == null) return NotFound();

        t.IsApproved = !t.IsApproved;
        await _context.SaveChangesAsync();
        return Ok(new { isApproved = t.IsApproved });
    }

    [Authorize(Policy = "perm:admin.trust.testimonials.manage")]
    [HttpPut("admin/testimonials/{id:int}")]
    public async Task<IActionResult> EditTestimonial(int id, [FromBody] CommentEditRequest request)
    {
        var t = await _context.Testimonials.FindAsync(id);
        if (t == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.FullName)) t.CustomerName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Content)) t.Content = request.Content.Trim();
        if (request.Rating.HasValue) t.Rating = request.Rating.Value;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.trust.testimonials.manage")]
    [HttpDelete("admin/testimonials/{id:int}")]
    public async Task<IActionResult> DeleteTestimonial(int id)
    {
        var t = await _context.Testimonials.FindAsync(id);
        if (t == null) return NotFound();

        _context.Testimonials.Remove(t);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.trust.portfolio.manage")]
    [HttpGet("admin/portfolio")]
    public async Task<ActionResult<PortfolioAdminResponse>> GetAdminPortfolio()
    {
        var items = await _context.PortfolioProjects
            .OrderByDescending(p => p.CompletionDate)
            .Select(p => new PortfolioProjectDto
            {
                Id = p.Id,
                Title = p.Title,
                Description = p.Description,
                BeforeImageUrl = p.BeforeImageUrl,
                AfterImageUrl = p.AfterImageUrl,
                DeviceType = p.DeviceType,
                Brand = p.Brand,
                CompletionDate = p.CompletionDate
            })
            .ToListAsync();

        var publishedIds = await _context.PortfolioProjects
            .Where(p => p.IsPublished)
            .Select(p => p.Id)
            .ToListAsync();

        return Ok(new PortfolioAdminResponse { Items = items, PublishedIds = publishedIds });
    }

    [Authorize(Policy = "perm:admin.trust.portfolio.manage")]
    [HttpPost("admin/portfolio")]
    public async Task<IActionResult> UpsertPortfolio([FromBody] PortfolioAdminUpsert request)
    {
        PortfolioProject entity;
        if (request.Id > 0)
        {
            entity = await _context.PortfolioProjects.FindAsync(request.Id) ?? new PortfolioProject();
            if (entity.Id == 0)
                _context.PortfolioProjects.Add(entity);
        }
        else
        {
            entity = new PortfolioProject();
            _context.PortfolioProjects.Add(entity);
        }

        entity.Title = request.Title ?? string.Empty;
        entity.Description = request.Description;
        entity.BeforeImageUrl = request.BeforeImageUrl;
        entity.AfterImageUrl = request.AfterImageUrl;
        entity.DeviceType = request.DeviceType;
        entity.Brand = request.Brand;
        entity.CompletionDate = request.CompletionDate;
        entity.IsPublished = request.IsPublished;

        await _context.SaveChangesAsync();
        return Ok(new { entity.Id });
    }

    [Authorize(Policy = "perm:admin.trust.portfolio.manage")]
    [HttpDelete("admin/portfolio/{id:int}")]
    public async Task<IActionResult> DeletePortfolio(int id)
    {
        var p = await _context.PortfolioProjects.FindAsync(id);
        if (p == null) return NotFound();

        _context.PortfolioProjects.Remove(p);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    public sealed class PortfolioAdminResponse
    {
        public List<PortfolioProjectDto> Items { get; set; } = new();
        public List<int> PublishedIds { get; set; } = new();
    }

    public sealed class PortfolioAdminUpsert
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? BeforeImageUrl { get; set; }
        public string? AfterImageUrl { get; set; }
        public string? DeviceType { get; set; }
        public string? Brand { get; set; }
        public DateTimeOffset CompletionDate { get; set; }
        public bool IsPublished { get; set; }
    }
}
