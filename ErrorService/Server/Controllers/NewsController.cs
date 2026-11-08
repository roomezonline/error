using System.Linq.Expressions;
using System.Text.RegularExpressions;
using ErrorService.Server.Data;
using ErrorService.Server.Infrastructure;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NewsController : ControllerBase
{
    private const string CacheTag = "content";

    private readonly ErrorServiceDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly IAuthorizationService _authorization;
    private readonly IOutputCacheStore _outputCache;

    public NewsController(
        ErrorServiceDbContext context,
        IWebHostEnvironment env,
        IConfiguration configuration,
        IAuthorizationService authorization,
        IOutputCacheStore outputCache)
    {
        _context = context;
        _env = env;
        _configuration = configuration;
        _authorization = authorization;
        _outputCache = outputCache;
    }

    private async Task<bool> CanManageAsync()
        => User.Identity?.IsAuthenticated == true
           && (await _authorization.AuthorizeAsync(User, "perm:admin.news.manage")).Succeeded;

    private void DisableOutputCaching()
    {
        var cacheFeature = HttpContext.Features.Get<IOutputCacheFeature>();
        if (cacheFeature != null) cacheFeature.Context.EnableOutputCaching = false;
    }

    private async Task EvictCacheAsync()
    {
        try { await _outputCache.EvictByTagAsync(CacheTag, CancellationToken.None); } catch { }
    }

    private async Task<ContentSettingsDto> GetSettingsAsync()
    {
        var settings = await _context.ContentSettings.AsNoTracking().FirstOrDefaultAsync();
        return settings == null ? new ContentSettingsDto() : ContentSettingsMapper.ToDto(settings);
    }

    private static IQueryable<News> Visible(IQueryable<News> query, DateTimeOffset now)
        => query.Where(n => n.IsPublished
                            && (n.PublishAt == null || n.PublishAt <= now)
                            && (n.ExpireAt == null || n.ExpireAt > now));

    private static bool IsTagMatch(string tags, string tag)
        => tags == tag
           || tags.StartsWith(tag + ",", StringComparison.Ordinal)
           || tags.EndsWith("," + tag, StringComparison.Ordinal)
           || tags.Contains("," + tag + ",", StringComparison.Ordinal);

    private static string? BuildSummary(string? summary, string content)
    {
        if (!string.IsNullOrWhiteSpace(summary)) return summary.Trim();

        var text = Regex.Replace(content ?? string.Empty, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, "\\s+", " ").Trim();
        if (text.Length == 0) return null;
        return text.Length > 300 ? text[..300].TrimEnd() + "…" : text;
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength].TrimEnd() : trimmed;
    }

    [Authorize(Policy = "perm:admin.news.manage")]
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

    [Authorize(Policy = "perm:admin.news.manage")]
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
    [OutputCache(Duration = 60, VaryByQueryKeys = new[] { "*" }, Tags = new[] { CacheTag })]
    public async Task<ActionResult<IEnumerable<NewsDto>>> GetNews(
        bool onlyPublished = false,
        int skip = 0,
        int take = 10,
        string? type = null,
        string? types = null,
        int? categoryId = null,
        string? category = null,
        string? tag = null,
        string? q = null,
        string? sort = "newest",
        string? status = null,
        bool featuredOnly = false,
        bool pinnedFirst = true,
        int? page = null,
        int? pageSize = null)
    {
        var settings = await GetSettingsAsync();
        var canManage = await CanManageAsync();
        if (canManage) DisableOutputCaching();

        var now = DateTimeOffset.UtcNow;
        var query = _context.News.AsNoTracking().AsQueryable();

        if (!canManage)
        {
            query = Visible(query, now);
            if (!settings.TipsEnabled)
                query = query.Where(n => n.ContentType != ContentType.Tip);
            if (!settings.AnnouncementsEnabled)
                query = query.Where(n => n.ContentType != ContentType.Announcement);
        }
        else if (onlyPublished)
        {
            query = Visible(query, now);
        }

        var typeFilter = ContentTypeCatalog.TryParse(type);
        if (typeFilter.HasValue)
            query = query.Where(n => n.ContentType == typeFilter.Value);

        if (!string.IsNullOrWhiteSpace(types))
        {
            var typeList = ContentTags.Split(types)
                .Select(ContentTypeCatalog.TryParse)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();
            if (typeList.Count == 0)
                return Ok(Array.Empty<NewsDto>());
            query = query.Where(n => typeList.Contains(n.ContentType));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(n => n.CategoryId == categoryId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryRow = await _context.ContentCategories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Slug == category || c.Title == category);
            if (categoryRow == null)
            {
                Response.Headers["X-Total-Count"] = "0";
                return Ok(Array.Empty<NewsDto>());
            }
            query = query.Where(n => n.CategoryId == categoryRow.Id);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var requested = Uri.UnescapeDataString(tag).Trim();
            var requestedSlug = ContentTags.Slug(requested);
            var allTags = await _context.News.AsNoTracking()
                .Where(n => n.Tags != null && n.Tags != "")
                .Select(n => n.Tags!)
                .ToListAsync();
            var match = allTags
                .SelectMany(ContentTags.Split)
                .FirstOrDefault(t => string.Equals(t, requested, StringComparison.OrdinalIgnoreCase)
                                     || ContentTags.Slug(t) == requestedSlug);

            if (match == null)
            {
                Response.Headers["X-Total-Count"] = "0";
                return Ok(Array.Empty<NewsDto>());
            }

            query = query.Where(n => n.Tags != null &&
                (n.Tags == match
                 || n.Tags.StartsWith(match + ",")
                 || n.Tags.EndsWith("," + match)
                 || n.Tags.Contains("," + match + ",")));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(n => n.Title.Contains(term)
                                     || (n.Summary != null && n.Summary.Contains(term))
                                     || (n.Tags != null && n.Tags.Contains(term)));
        }

        if (featuredOnly)
            query = query.Where(n => n.IsFeatured);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = status.Trim().ToLowerInvariant() switch
            {
                "draft" => query.Where(n => !n.IsPublished),
                "scheduled" => query.Where(n => n.IsPublished
                                                && n.PublishAt != null && n.PublishAt > now
                                                && (n.ExpireAt == null || n.ExpireAt > now)),
                "archived" => query.Where(n => n.IsPublished && n.ExpireAt != null && n.ExpireAt <= now),
                "published" => query.Where(n => n.IsPublished
                                                && (n.PublishAt == null || n.PublishAt <= now)
                                                && (n.ExpireAt == null || n.ExpireAt > now)),
                _ => query
            };
        }

        var totalCount = await query.CountAsync();
        Response.Headers["X-Total-Count"] = totalCount.ToString();

        if (page.HasValue && page.Value > 0)
        {
            take = Math.Clamp(pageSize ?? settings.PageSize, 1, 100);
            skip = (page.Value - 1) * take;
        }
        else
        {
            take = Math.Clamp(take, 1, 100);
            skip = Math.Max(0, skip);
        }

        query = (sort ?? "newest").ToLowerInvariant() switch
        {
            "views" => query.OrderByDescending(n => n.ViewCount).ThenByDescending(n => n.CreatedAt),
            "oldest" => query.OrderBy(n => n.PublishAt ?? n.CreatedAt),
            _ => settings.PinnedEnabled && pinnedFirst
                ? query.OrderByDescending(n => n.PinnedUntil != null && n.PinnedUntil > now)
                       .ThenByDescending(n => n.PublishAt ?? n.CreatedAt)
                : query.OrderByDescending(n => n.PublishAt ?? n.CreatedAt)
        };

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(n => new NewsDto
            {
                Id = n.Id,
                Title = n.Title,
                Slug = n.Slug,
                Summary = n.Summary,
                ImageUrl = n.ImageUrl,
                Tags = n.Tags,
                AuthorName = n.AuthorName,
                IsPublished = n.IsPublished,
                ContentType = n.ContentType,
                CategoryId = n.CategoryId,
                CategoryTitle = n.Category != null ? n.Category.Title : null,
                CategorySlug = n.Category != null ? n.Category.Slug : null,
                PublishAt = n.PublishAt,
                ExpireAt = n.ExpireAt,
                IsFeatured = n.IsFeatured,
                PinnedUntil = n.PinnedUntil,
                ViewCount = n.ViewCount,
                SourceName = n.SourceName,
                SourceUrl = n.SourceUrl,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                PublishedAt = n.PublishAt ?? n.CreatedAt
            })
            .ToListAsync();

        foreach (var item in items)
        {
            item.Status = ContentStatusResolver.Resolve(item.IsPublished, item.PublishAt, item.ExpireAt, now);
            item.CommentsEnabled = settings.AreCommentsEnabled(item.ContentType);
        }

        return Ok(items);
    }

    [HttpGet("slug/{slug}")]
    [OutputCache(Duration = 120, Tags = new[] { CacheTag })]
    public async Task<ActionResult<NewsDto>> GetNewsItemBySlug(string slug, string? type = null)
    {
        var settings = await GetSettingsAsync();
        var canManage = await CanManageAsync();
        if (canManage) DisableOutputCaching();

        var query = _context.News.AsNoTracking().Where(n => n.Slug == slug);
        var typeFilter = ContentTypeCatalog.TryParse(type);
        if (typeFilter.HasValue)
            query = query.Where(n => n.ContentType == typeFilter.Value);

        var news = await ProjectFull(query).FirstOrDefaultAsync();
        if (news == null) return NotFound("محتوا یافت نشد");

        var now = DateTimeOffset.UtcNow;
        var visible = ContentStatusResolver.IsVisible(news.IsPublished, news.PublishAt, news.ExpireAt, now);
        if (!visible && !canManage) return NotFound("محتوا یافت نشد");
        if (!settings.IsTypeEnabled(news.ContentType) && !canManage) return NotFound("محتوا یافت نشد");
        if (!visible) DisableOutputCaching();

        return Ok(await BuildNewsItemPayload(news, settings, now));
    }

    [HttpGet("{id:int}")]
    [OutputCache(Duration = 120, Tags = new[] { CacheTag })]
    public async Task<ActionResult<NewsDto>> GetNewsItem(int id, string? type = null)
    {
        var settings = await GetSettingsAsync();
        var canManage = await CanManageAsync();
        if (canManage) DisableOutputCaching();

        var query = _context.News.AsNoTracking().Where(n => n.Id == id);
        var typeFilter = ContentTypeCatalog.TryParse(type);
        if (typeFilter.HasValue)
            query = query.Where(n => n.ContentType == typeFilter.Value);

        var news = await ProjectFull(query).FirstOrDefaultAsync();
        if (news == null) return NotFound("محتوا یافت نشد");

        var now = DateTimeOffset.UtcNow;
        var visible = ContentStatusResolver.IsVisible(news.IsPublished, news.PublishAt, news.ExpireAt, now);
        if (!visible && !canManage) return NotFound("محتوا یافت نشد");
        if (!settings.IsTypeEnabled(news.ContentType) && !canManage) return NotFound("محتوا یافت نشد");
        if (!visible) DisableOutputCaching();

        return Ok(await BuildNewsItemPayload(news, settings, now));
    }

    [HttpPost("{id:int}/view")]
    public async Task<IActionResult> IncrementView(int id)
    {
        await _context.News
            .Where(n => n.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ViewCount, n => n.ViewCount + 1));
        return NoContent();
    }

    private static IQueryable<NewsDto> ProjectFull(IQueryable<News> query)
        => query.Select(n => new NewsDto
        {
            Id = n.Id,
            Title = n.Title,
            Slug = n.Slug,
            Content = n.Content,
            Summary = n.Summary,
            ImageUrl = n.ImageUrl,
            Tags = n.Tags,
            AuthorName = n.AuthorName,
            IsPublished = n.IsPublished,
            ContentType = n.ContentType,
            CategoryId = n.CategoryId,
            CategoryTitle = n.Category != null ? n.Category.Title : null,
            CategorySlug = n.Category != null ? n.Category.Slug : null,
            PublishAt = n.PublishAt,
            ExpireAt = n.ExpireAt,
            IsFeatured = n.IsFeatured,
            PinnedUntil = n.PinnedUntil,
            ViewCount = n.ViewCount,
            SeoTitle = n.SeoTitle,
            SeoDescription = n.SeoDescription,
            CanonicalUrl = n.CanonicalUrl,
            SourceName = n.SourceName,
            SourceUrl = n.SourceUrl,
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt,
            PublishedAt = n.PublishAt ?? n.CreatedAt
        });

    private async Task<NewsDto> BuildNewsItemPayload(NewsDto news, ContentSettingsDto settings, DateTimeOffset now)
    {
        news.Status = ContentStatusResolver.Resolve(news.IsPublished, news.PublishAt, news.ExpireAt, now);
        news.CommentsEnabled = settings.AreCommentsEnabled(news.ContentType);

        if (news.CommentsEnabled)
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
        }

        var visible = Visible(_context.News.AsNoTracking()
            .Where(n => n.ContentType == news.ContentType), now);

        news.PreviousNews = await visible
            .Where(n => n.Id != news.Id && (n.PublishAt ?? n.CreatedAt) < news.PublishedAt)
            .OrderByDescending(n => n.PublishAt ?? n.CreatedAt)
            .Select(n => new NewsDto { Id = n.Id, Title = n.Title, Slug = n.Slug, ImageUrl = n.ImageUrl, ContentType = n.ContentType })
            .FirstOrDefaultAsync();

        news.NextNews = await visible
            .Where(n => n.Id != news.Id && (n.PublishAt ?? n.CreatedAt) > news.PublishedAt)
            .OrderBy(n => n.PublishAt ?? n.CreatedAt)
            .Select(n => new NewsDto { Id = n.Id, Title = n.Title, Slug = n.Slug, ImageUrl = n.ImageUrl, ContentType = n.ContentType })
            .FirstOrDefaultAsync();

        return news;
    }

    [HttpGet("{id:int}/related")]
    [OutputCache(Duration = 120, Tags = new[] { CacheTag })]
    public async Task<ActionResult<IEnumerable<NewsDto>>> GetRelatedNews(int id, int take = 0)
    {
        var settings = await GetSettingsAsync();
        take = Math.Clamp(take <= 0 ? settings.RelatedCount : take, 1, 12);

        var current = await _context.News.AsNoTracking()
            .Where(n => n.Id == id)
            .Select(n => new { n.Id, n.ContentType, n.CategoryId })
            .FirstOrDefaultAsync();
        if (current == null) return NotFound();

        var now = DateTimeOffset.UtcNow;
        var query = Visible(_context.News.AsNoTracking()
            .Where(n => n.Id != id && n.ContentType == current.ContentType), now);

        var related = new List<NewsDto>();
        if (current.CategoryId.HasValue)
        {
            related = await ProjectList(query.Where(n => n.CategoryId == current.CategoryId))
                .OrderByDescending(n => n.PublishedAt)
                .Take(take)
                .ToListAsync();
        }

        if (related.Count < take)
        {
            var excludeIds = related.Select(x => x.Id).Append(id).ToList();
            var more = await ProjectList(query.Where(n => !excludeIds.Contains(n.Id)))
                .OrderByDescending(n => n.PublishedAt)
                .Take(take - related.Count)
                .ToListAsync();
            related.AddRange(more);
        }

        foreach (var item in related)
            item.Status = ContentStatusResolver.Resolve(item.IsPublished, item.PublishAt, item.ExpireAt, now);

        return Ok(related);
    }

    private static IQueryable<NewsDto> ProjectList(IQueryable<News> query)
        => query.Select(n => new NewsDto
        {
            Id = n.Id,
            Title = n.Title,
            Slug = n.Slug,
            Summary = n.Summary,
            ImageUrl = n.ImageUrl,
            Tags = n.Tags,
            ContentType = n.ContentType,
            CategoryId = n.CategoryId,
            CategoryTitle = n.Category != null ? n.Category.Title : null,
            CategorySlug = n.Category != null ? n.Category.Slug : null,
            IsPublished = n.IsPublished,
            PublishAt = n.PublishAt,
            ExpireAt = n.ExpireAt,
            IsFeatured = n.IsFeatured,
            ViewCount = n.ViewCount,
            CreatedAt = n.CreatedAt,
            UpdatedAt = n.UpdatedAt,
            PublishedAt = n.PublishAt ?? n.CreatedAt
        });

    [Authorize(Policy = "perm:admin.news.manage")]
    [HttpPost]
    public async Task<ActionResult<NewsDto>> PostNews(NewsUpsertRequest request)
    {
        var validationError = await ValidateRequestAsync(request);
        if (validationError != null) return BadRequest(validationError);

        var settings = await GetSettingsAsync();
        var now = DateTimeOffset.UtcNow;
        var news = new News
        {
            Title = request.Title.Trim(),
            Slug = await SlugService.ResolveUniqueAsync(
                _context.News.Where(n => n.Slug != null).Select(n => n.Slug!),
                request.Title),
            Summary = BuildSummary(request.Summary, request.Content),
            Content = request.Content,
            ImageUrl = Clean(request.ImageUrl, 500),
            Tags = ContentTags.Normalize(request.Tags),
            AuthorName = Clean(request.AuthorName, 100) ?? settings.DefaultAuthorName,
            IsPublished = request.IsPublished,
            ContentType = request.ContentType,
            CategoryId = request.CategoryId,
            PublishAt = request.PublishAt,
            ExpireAt = request.ExpireAt,
            IsFeatured = request.IsFeatured,
            PinnedUntil = request.PinnedUntil,
            SeoTitle = Clean(request.SeoTitle, 200),
            SeoDescription = Clean(request.SeoDescription, 500),
            CanonicalUrl = Clean(request.CanonicalUrl, 500),
            SourceName = Clean(request.SourceName, 200),
            SourceUrl = Clean(request.SourceUrl, 500),
            CreatedAt = request.CreatedAt ?? now,
            UpdatedAt = now
        };

        _context.News.Add(news);
        await _context.SaveChangesAsync();
        SeoFallbackMiddleware.InvalidateHtmlCache();
        await EvictCacheAsync();

        return Ok(new NewsDto { Id = news.Id, Title = news.Title, Slug = news.Slug, ContentType = news.ContentType });
    }

    [Authorize(Policy = "perm:admin.news.manage")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutNews(int id, NewsUpsertRequest request)
    {
        var news = await _context.News.FindAsync(id);
        if (news == null)
            return NotFound("محتوا یافت نشد");

        var validationError = await ValidateRequestAsync(request);
        if (validationError != null) return BadRequest(validationError);

        var oldName = news.Title;
        news.Title = request.Title.Trim();
        news.Summary = BuildSummary(request.Summary, request.Content);
        news.Content = request.Content;
        FileCleanupHelper.DeleteOldFileIfChanged(news.ImageUrl, request.ImageUrl, _env);
        news.ImageUrl = Clean(request.ImageUrl, 500);
        news.Tags = ContentTags.Normalize(request.Tags);
        news.AuthorName = Clean(request.AuthorName, 100);
        news.IsPublished = request.IsPublished;
        news.ContentType = request.ContentType;
        news.CategoryId = request.CategoryId;
        news.PublishAt = request.PublishAt;
        news.ExpireAt = request.ExpireAt;
        news.IsFeatured = request.IsFeatured;
        news.PinnedUntil = request.PinnedUntil;
        news.SeoTitle = Clean(request.SeoTitle, 200);
        news.SeoDescription = Clean(request.SeoDescription, 500);
        news.CanonicalUrl = Clean(request.CanonicalUrl, 500);
        news.SourceName = Clean(request.SourceName, 200);
        news.SourceUrl = Clean(request.SourceUrl, 500);
        if (request.CreatedAt.HasValue)
            news.CreatedAt = request.CreatedAt.Value;
        news.UpdatedAt = DateTimeOffset.UtcNow;

        news.Slug = await SlugService.ResolveForUpdateAsync(
            _context.News.Where(n => n.Slug != null && n.Id != id).Select(n => n.Slug!),
            oldName, news.Slug, request.Title);

        await _context.SaveChangesAsync();
        SeoFallbackMiddleware.InvalidateHtmlCache();
        await EvictCacheAsync();
        return NoContent();
    }

    private async Task<string?> ValidateRequestAsync(NewsUpsertRequest request)
    {
        if (!Enum.IsDefined(typeof(ContentType), request.ContentType))
            return "نوع محتوا معتبر نیست.";

        if (request.CategoryId.HasValue)
        {
            var category = await _context.ContentCategories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId.Value);
            if (category == null)
                return "دسته انتخاب‌شده یافت نشد.";
            if (category.ContentType.HasValue && category.ContentType.Value != request.ContentType)
                return "دسته انتخاب‌شده با نوع محتوا هم‌خوانی ندارد.";
        }

        if (request.PublishAt.HasValue && request.ExpireAt.HasValue && request.ExpireAt <= request.PublishAt)
            return "زمان انقضا باید بعد از زمان انتشار باشد.";

        if (request.PublishAt.HasValue && request.PublishAt > DateTimeOffset.UtcNow)
        {
            var settings = await GetSettingsAsync();
            if (!settings.ScheduledPublishingEnabled)
                return "انتشار زمان‌بندی‌شده توسط مدیر غیرفعال شده است.";
        }

        return null;
    }

    [Authorize(Policy = "perm:admin.news.manage")]
    [HttpPost("bulk")]
    public async Task<IActionResult> Bulk([FromBody] ContentBulkRequest request)
    {
        if (request == null || request.Ids.Count == 0) return BadRequest("موردی انتخاب نشده است.");

        var ids = request.Ids.Distinct().Take(200).ToList();
        var rows = await _context.News.Where(n => ids.Contains(n.Id)).ToListAsync();
        if (rows.Count == 0) return NotFound();

        var action = request.Action.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        switch (action)
        {
            case "publish":
                foreach (var row in rows)
                {
                    row.IsPublished = true;
                    if (row.PublishAt.HasValue && row.PublishAt > now) row.PublishAt = null;
                    row.UpdatedAt = now;
                }
                break;
            case "unpublish":
                foreach (var row in rows)
                {
                    row.IsPublished = false;
                    row.UpdatedAt = now;
                }
                break;
            case "archive":
                foreach (var row in rows)
                {
                    row.IsPublished = true;
                    row.ExpireAt = now;
                    row.UpdatedAt = now;
                }
                break;
            case "delete":
                _context.News.RemoveRange(rows);
                break;
            default:
                return BadRequest("عملیات نامعتبر است.");
        }

        await _context.SaveChangesAsync();
        SeoFallbackMiddleware.InvalidateHtmlCache();
        await EvictCacheAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.news.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteNews(int id)
    {
        var news = await _context.News.FindAsync(id);
        if (news == null)
            return NotFound("محتوا یافت نشد");

        _context.News.Remove(news);
        await _context.SaveChangesAsync();
        SeoFallbackMiddleware.InvalidateHtmlCache();
        await EvictCacheAsync();
        return NoContent();
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
