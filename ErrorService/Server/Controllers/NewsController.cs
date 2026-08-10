using ErrorService.Server.Data;
using ErrorService.Server.Infrastructure;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsController : ControllerBase
{
    private readonly ErrorServiceDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public NewsController(ErrorServiceDbContext context, IWebHostEnvironment env, IConfiguration configuration)
    {
        _context = context;
        _env = env;
        _configuration = configuration;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(15_000_000)]
    public async Task<ActionResult<string>> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length <= 0)
                return BadRequest("فایل خالی است یا دریافت نشد");

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";

            var safeExt = ext.ToLowerInvariant();
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
            if (!allowed.Contains(safeExt))
                return BadRequest("فرمت فایل مجاز نیست. فقط jpg, jpeg, png, webp, svg");

            var relativeFolder = _configuration["Uploads:NewsRelativePath"];
            if (string.IsNullOrWhiteSpace(relativeFolder))
                relativeFolder = "uploads/news";

            relativeFolder = relativeFolder.Trim().TrimStart('~').TrimStart('/').TrimEnd('/');

            var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            if (!Directory.Exists(wwwroot)) Directory.CreateDirectory(wwwroot);

            var uploadsRoot = Path.Combine(wwwroot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(uploadsRoot)) Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{Guid.NewGuid():N}{safeExt}";
            var fullPath = Path.Combine(uploadsRoot, fileName);

            await using (var outStream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(outStream, cancellationToken);
            }

            return Ok($"/{relativeFolder}/{fileName}");
        }
        catch (Exception ex)
        {
            return BadRequest($"آپلود تصویر ناموفق بود: {ex.Message}");
        }
    }

    [HttpPost("upload-video")]
    [RequestSizeLimit(150_000_000)]
    public async Task<ActionResult<string>> UploadVideo([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length <= 0)
                return BadRequest("فایل خالی است یا دریافت نشد");

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";

            var safeExt = ext.ToLowerInvariant();
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm", ".ogg", ".m4v", ".mov" };
            if (!allowed.Contains(safeExt))
                return BadRequest("فرمت فایل مجاز نیست. فقط mp4, webm, ogg, m4v, mov");

            var relativeFolder = _configuration["Uploads:NewsVideosRelativePath"];
            if (string.IsNullOrWhiteSpace(relativeFolder))
                relativeFolder = "uploads/news-videos";

            relativeFolder = relativeFolder.Trim().TrimStart('~').TrimStart('/').TrimEnd('/');

            var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            if (!Directory.Exists(wwwroot)) Directory.CreateDirectory(wwwroot);

            var uploadsRoot = Path.Combine(wwwroot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(uploadsRoot)) Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{Guid.NewGuid():N}{safeExt}";
            var fullPath = Path.Combine(uploadsRoot, fileName);

            await using (var outStream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(outStream, cancellationToken);
            }

            return Ok($"/{relativeFolder}/{fileName}");
        }
        catch (Exception ex)
        {
            return BadRequest($"آپلود ویدیو ناموفق بود: {ex.Message}");
        }
    }

    [HttpGet]
    [OutputCache(Duration = 120, VaryByQueryKeys = new[] { "*" })]
    public async Task<ActionResult<IEnumerable<NewsDto>>> GetNews(bool onlyPublished = false, int skip = 0, int take = 10)
    {
        var query = _context.News.AsQueryable();

        if (onlyPublished)
            query = query.Where(n => n.IsPublished);

        var totalCount = await query.CountAsync();
        Response.Headers["X-Total-Count"] = totalCount.ToString();

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(n => new NewsDto
            {
                Id = n.Id,
                Title = n.Title,
                Slug = n.Slug,
                Summary = n.Summary,
                Content = n.Content,
                ImageUrl = n.ImageUrl,
                Tags = n.Tags,
                AuthorName = n.AuthorName,
                IsPublished = n.IsPublished,
                CreatedAt = n.CreatedAt
            }).ToListAsync();
    }

    [HttpGet("slug/{slug}")]
    [OutputCache(Duration = 300)]
    public async Task<ActionResult<NewsDto>> GetNewsItemBySlug(string slug)
    {
        var news = await _context.News
            .Select(n => new NewsDto
            {
                Id = n.Id,
                Title = n.Title,
                Slug = n.Slug,
                Summary = n.Summary,
                Content = n.Content,
                ImageUrl = n.ImageUrl,
                Tags = n.Tags,
                AuthorName = n.AuthorName,
                IsPublished = n.IsPublished,
                CreatedAt = n.CreatedAt
            })
            .FirstOrDefaultAsync(n => n.Slug == slug);

        if (news == null)
            return NotFound("خبر مورد نظر یافت نشد");

        return Ok(await BuildNewsItemPayload(news));
    }

    [HttpGet("{id:int}")]
    [OutputCache(Duration = 300)]
    public async Task<ActionResult<NewsDto>> GetNewsItem(int id)
    {
        var news = await _context.News
            .Select(n => new NewsDto
            {
                Id = n.Id,
                Title = n.Title,
                Slug = n.Slug,
                Summary = n.Summary,
                Content = n.Content,
                ImageUrl = n.ImageUrl,
                Tags = n.Tags,
                AuthorName = n.AuthorName,
                IsPublished = n.IsPublished,
                CreatedAt = n.CreatedAt
            })
            .FirstOrDefaultAsync(n => n.Id == id);

        if (news == null)
            return NotFound("خبر مورد نظر یافت نشد");

        return Ok(await BuildNewsItemPayload(news));
    }

    private async Task<NewsDto> BuildNewsItemPayload(NewsDto news)
    {
        var comments = await _context.NewsComments
            .Where(c => c.NewsId == news.Id && c.IsApproved)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommentDto
            {
                Id = c.Id,
                OwnerId = c.NewsId,
                ParentId = c.ParentId,
                FullName = c.FullName,
                Content = c.Content,
                Rating = c.Rating,
                CreatedAtFa = c.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            })
            .ToListAsync();

        var withRating = comments.Where(c => c.Rating.HasValue).ToList();
        news.AverageRating = withRating.Any() ? Math.Round(withRating.Average(c => c.Rating!.Value), 1) : 0;
        news.CommentCount = comments.Count;
        news.Comments = BuildTree(comments);

        var prev = await _context.News
            .Where(n => n.IsPublished && n.CreatedAt < news.CreatedAt)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NewsDto { Id = n.Id, Title = n.Title, Slug = n.Slug, ImageUrl = n.ImageUrl })
            .FirstOrDefaultAsync();

        var next = await _context.News
            .Where(n => n.IsPublished && n.CreatedAt > news.CreatedAt)
            .OrderBy(n => n.CreatedAt)
            .Select(n => new NewsDto { Id = n.Id, Title = n.Title, Slug = n.Slug, ImageUrl = n.ImageUrl })
            .FirstOrDefaultAsync();

        news.PreviousNews = prev;
        news.NextNews = next;

        return news;
    }

    [HttpPost]
    public async Task<ActionResult<NewsDto>> PostNews(NewsUpsertRequest request)
    {
        var news = new News
        {
            Title = request.Title,
            Slug = await SlugService.ResolveUniqueAsync(
                _context.News.Where(n => n.Slug != null).Select(n => n.Slug!),
                request.Title),
            Summary = request.Summary,
            Content = request.Content,
            ImageUrl = request.ImageUrl,
            Tags = request.Tags,
            AuthorName = request.AuthorName,
            IsPublished = request.IsPublished,
            CreatedAt = request.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.News.Add(news);
        await _context.SaveChangesAsync();

        return Ok(new NewsDto { Id = news.Id, Title = news.Title, Slug = news.Slug });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutNews(int id, NewsUpsertRequest request)
    {
        var news = await _context.News.FindAsync(id);
        if (news == null)
            return NotFound("خبر مورد نظر یافت نشد");

        news.Summary = request.Summary;
        news.Content = request.Content;
        FileCleanupHelper.DeleteOldFileIfChanged(news.ImageUrl, request.ImageUrl, _env);
        news.ImageUrl = request.ImageUrl;
        news.Tags = request.Tags;
        news.AuthorName = request.AuthorName;
        news.IsPublished = request.IsPublished;
        if (request.CreatedAt.HasValue)
            news.CreatedAt = request.CreatedAt.Value;
        news.UpdatedAt = DateTimeOffset.UtcNow;

        var oldName = news.Title;
        news.Title = request.Title;
        news.Slug = await SlugService.ResolveForUpdateAsync(
            _context.News.Where(n => n.Slug != null && n.Id != id).Select(n => n.Slug!),
            oldName, news.Slug, request.Title);
        news.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteNews(int id)
    {
        var news = await _context.News.FindAsync(id);
        if (news == null)
            return NotFound("خبر مورد نظر یافت نشد");

        _context.News.Remove(news);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/related")]
    [OutputCache(Duration = 300)]
    public async Task<ActionResult<IEnumerable<NewsDto>>> GetRelatedNews(int id, int take = 3)
    {
        var currentNews = await _context.News.FindAsync(id);
        if (currentNews == null) return NotFound();

        var query = _context.News
            .Where(n => n.Id != id && n.IsPublished);

        // Simple similarity: match by tags if any
        if (!string.IsNullOrEmpty(currentNews.Tags))
        {
            var tags = currentNews.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            // In a real app, you'd use a more complex EF query or full-text search. 
            // For now, let's just get the latest published news as fallback if no tags match nicely in simple LINQ.
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new NewsDto
            {
                Id = n.Id,
                Title = n.Title,
                Slug = n.Slug,
                Summary = n.Summary,
                ImageUrl = n.ImageUrl,
                CreatedAt = n.CreatedAt
            }).ToListAsync();
    }

    private static List<CommentDto> BuildTree(List<CommentDto> flat)
    {
        var lookup = flat.ToLookup(x => x.ParentId);
        var roots = lookup[null].ToList();
        foreach (var root in roots)
            AttachReplies(root, lookup);
        return roots;
    }

    private static void AttachReplies(CommentDto parent, ILookup<int?, CommentDto> lookup)
    {
        var children = lookup[parent.Id].ToList();
        foreach (var child in children)
            AttachReplies(child, lookup);
        parent.Replies = children;
    }
}
