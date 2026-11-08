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
public class ContentController : ControllerBase
{
    private const string CacheTag = "content";

    private readonly ErrorServiceDbContext _context;
    private readonly IOutputCacheStore _outputCache;

    public ContentController(ErrorServiceDbContext context, IOutputCacheStore outputCache)
    {
        _context = context;
        _outputCache = outputCache;
    }

    private void DisableOutputCaching()
    {
        var cacheFeature = HttpContext.Features.Get<IOutputCacheFeature>();
        if (cacheFeature != null) cacheFeature.Context.EnableOutputCaching = false;
    }

    private async Task EvictCacheAsync()
    {
        try { await _outputCache.EvictByTagAsync(CacheTag, CancellationToken.None); } catch { }
    }

    // ───────────────────────── دسته‌بندی‌ها ─────────────────────────

    /// <summary>دسته‌های فعال برای نمایش عمومی؛ در صورت تعیین نوع، دسته‌های عمومی و همان نوع برگردانده می‌شوند.</summary>
    [HttpGet("categories")]
    [OutputCache(Duration = 120, VaryByQueryKeys = new[] { "*" }, Tags = new[] { CacheTag })]
    public async Task<ActionResult<IEnumerable<ContentCategoryDto>>> GetCategories(string? type = null, bool includeInactive = false)
    {
        var typeFilter = ContentTypeCatalog.TryParse(type);
        if (User.Identity?.IsAuthenticated == true) DisableOutputCaching();

        var query = _context.ContentCategories.AsNoTracking().AsQueryable();

        if (!includeInactive || User.Identity?.IsAuthenticated != true)
            query = query.Where(c => c.IsActive);

        if (typeFilter.HasValue)
            query = query.Where(c => c.ContentType == null || c.ContentType == typeFilter.Value);

        var items = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Title)
            .Select(c => new ContentCategoryDto
            {
                Id = c.Id,
                Title = c.Title,
                Slug = c.Slug,
                ContentType = c.ContentType,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                ContentCount = c.Contents.Count()
            })
            .ToListAsync();

        return Ok(items);
    }

    [Authorize(Policy = "perm:admin.news.manage")]
    [HttpGet("categories/admin")]
    public async Task<ActionResult<IEnumerable<ContentCategoryDto>>> GetCategoriesAdmin(string? type = null, string? q = null)
    {
        var query = _context.ContentCategories.AsNoTracking().AsQueryable();

        var typeFilter = ContentTypeCatalog.TryParse(type);
        if (typeFilter.HasValue)
            query = query.Where(c => c.ContentType == null || c.ContentType == typeFilter.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Title.Contains(term));
        }

        var items = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Title)
            .Select(c => new ContentCategoryDto
            {
                Id = c.Id,
                Title = c.Title,
                Slug = c.Slug,
                ContentType = c.ContentType,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                ContentCount = c.Contents.Count()
            })
            .ToListAsync();

        return Ok(items);
    }

    [Authorize(Policy = "perm:admin.news.manage")]
    [HttpPost("categories")]
    public async Task<ActionResult<ContentCategoryDto>> CreateCategory(ContentCategoryUpsertRequest request)
    {
        if (request.ContentType.HasValue && !Enum.IsDefined(typeof(ContentType), request.ContentType.Value))
            return BadRequest("نوع محتوا معتبر نیست.");

        var title = request.Title.Trim();
        var slug = await ResolveCategorySlugAsync(title, request.Slug, null);

        var category = new ContentCategory
        {
            Title = title,
            Slug = slug,
            ContentType = request.ContentType,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.ContentCategories.Add(category);
        await _context.SaveChangesAsync();
        await EvictCacheAsync();

        return Ok(ToDto(category, 0));
    }

    [Authorize(Policy = "perm:admin.news.manage")]
    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, ContentCategoryUpsertRequest request)
    {
        var category = await _context.ContentCategories.FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound("دسته یافت نشد.");

        if (request.ContentType.HasValue && !Enum.IsDefined(typeof(ContentType), request.ContentType.Value))
            return BadRequest("نوع محتوا معتبر نیست.");

        var title = request.Title.Trim();
        category.Title = title;
        category.Slug = await ResolveCategorySlugAsync(title, request.Slug, id);
        category.ContentType = request.ContentType;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        await EvictCacheAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.news.manage")]
    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.ContentCategories.FirstOrDefaultAsync(c => c.Id == id);
        if (category == null) return NotFound("دسته یافت نشد.");

        var hasContent = await _context.News.AnyAsync(n => n.CategoryId == id);
        if (hasContent)
            return BadRequest("این دسته دارای محتوا است؛ ابتدا محتواها را جابه‌جا یا حذف کنید.");

        _context.ContentCategories.Remove(category);
        await _context.SaveChangesAsync();
        await EvictCacheAsync();
        return NoContent();
    }

    private async Task<string> ResolveCategorySlugAsync(string title, string? requestedSlug, int? excludeId)
    {
        var baseSlug = SlugUtil.Slugify(string.IsNullOrWhiteSpace(requestedSlug) ? title : requestedSlug, 120);
        if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = "category";

        var candidate = baseSlug;
        var suffix = 1;
        while (await _context.ContentCategories.AnyAsync(c =>
                   c.Slug == candidate && (!excludeId.HasValue || c.Id != excludeId.Value)))
        {
            suffix++;
            candidate = $"{baseSlug}-{suffix}";
        }

        return candidate;
    }

    // ───────────────────────── برچسب‌ها ─────────────────────────

    [Authorize(Policy = "perm:admin.news.manage")]
    [HttpGet("tags")]
    public async Task<ActionResult<IEnumerable<ContentTagDto>>> GetTags(string? type = null, string? q = null)
    {
        var query = _context.News.AsNoTracking().Where(n => n.Tags != null && n.Tags != "");

        var typeFilter = ContentTypeCatalog.TryParse(type);
        if (typeFilter.HasValue)
            query = query.Where(n => n.ContentType == typeFilter.Value);

        var rawTags = await query.Select(n => n.Tags!).ToListAsync();

        var map = new Dictionary<string, (string Title, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rawTags)
        {
            foreach (var tag in ContentTags.Split(row))
            {
                if (map.TryGetValue(tag, out var existing))
                    map[tag] = (existing.Title, existing.Count + 1);
                else
                    map[tag] = (tag, 1);
            }
        }

        var items = map.Values
            .Select(v => new ContentTagDto { Title = v.Title, Slug = ContentTags.Slug(v.Title), Count = v.Count })
            .OrderByDescending(v => v.Count)
            .ThenBy(v => v.Title)
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            items = items.Where(v => v.Title.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(items.ToList());
    }

    [Authorize(Policy = "perm:admin.news.manage")]
    [HttpPost("tags/rename")]
    public async Task<IActionResult> RenameTag(ContentTagRenameRequest request)
    {
        var oldTitle = request.OldTitle.Trim().TrimStart('#');
        var newTitle = ContentTags.Normalize(request.NewTitle);
        if (string.IsNullOrWhiteSpace(oldTitle) || string.IsNullOrWhiteSpace(newTitle))
            return BadRequest("نام برچسب معتبر نیست.");

        if (newTitle.Contains(','))
            return BadRequest("نام برچسب نمی‌تواند شامل ویرگول باشد.");

        var rows = await _context.News
            .Where(n => n.Tags != null && n.Tags != "")
            .ToListAsync();

        var changed = 0;
        foreach (var row in rows)
        {
            var tags = ContentTags.Split(row.Tags);
            if (!tags.Any(t => string.Equals(t, oldTitle, StringComparison.OrdinalIgnoreCase))) continue;

            var updated = tags
                .Select(t => string.Equals(t, oldTitle, StringComparison.OrdinalIgnoreCase)
                    ? string.Equals(t, newTitle, StringComparison.OrdinalIgnoreCase) ? t : newTitle
                    : t)
                .ToList();

            var normalized = ContentTags.Normalize(string.Join(",", updated));
            if (!string.Equals(normalized, row.Tags, StringComparison.Ordinal))
            {
                row.Tags = normalized;
                row.UpdatedAt = DateTimeOffset.UtcNow;
                changed++;
            }
        }

        if (changed == 0) return NotFound("برچسب یافت نشد.");

        await _context.SaveChangesAsync();
        SeoFallbackMiddleware.InvalidateHtmlCache();
        await EvictCacheAsync();
        return Ok(new { updated = changed });
    }

    [Authorize(Policy = "perm:admin.news.manage")]
    [HttpDelete("tags")]
    public async Task<IActionResult> DeleteTag(string title)
    {
        var target = title?.Trim().TrimStart('#');
        if (string.IsNullOrWhiteSpace(target)) return BadRequest("نام برچسب معتبر نیست.");

        var rows = await _context.News
            .Where(n => n.Tags != null && n.Tags != "")
            .ToListAsync();

        var changed = 0;
        foreach (var row in rows)
        {
            var tags = ContentTags.Split(row.Tags);
            if (!tags.Any(t => string.Equals(t, target, StringComparison.OrdinalIgnoreCase))) continue;

            row.Tags = ContentTags.Normalize(string.Join(",", tags.Where(t => !string.Equals(t, target, StringComparison.OrdinalIgnoreCase))));
            row.UpdatedAt = DateTimeOffset.UtcNow;
            changed++;
        }

        if (changed == 0) return NotFound("برچسب یافت نشد.");

        await _context.SaveChangesAsync();
        SeoFallbackMiddleware.InvalidateHtmlCache();
        await EvictCacheAsync();
        return Ok(new { updated = changed });
    }

    // ───────────────────────── تنظیمات ─────────────────────────

    [HttpGet("settings")]
    [OutputCache(Duration = 60, Tags = new[] { CacheTag })]
    public async Task<ActionResult<ContentSettingsDto>> GetSettings()
    {
        var settings = await _context.ContentSettings.AsNoTracking().FirstOrDefaultAsync();
        return Ok(settings == null ? new ContentSettingsDto() : ContentSettingsMapper.ToDto(settings));
    }

    [Authorize(Policy = "perm:admin.news.settings.manage")]
    [HttpPut("settings")]
    public async Task<ActionResult<ContentSettingsDto>> UpdateSettings(ContentSettingsDto request)
    {
        if (request.PageSize < 1 || request.PageSize > 60) return BadRequest("تعداد محتوای هر صفحه باید بین ۱ تا ۶۰ باشد.");
        if (request.RelatedCount < 0 || request.RelatedCount > 12) return BadRequest("تعداد محتوای مرتبط باید بین ۰ تا ۱۲ باشد.");
        if (request.HomeCardCount < 0 || request.HomeCardCount > 12) return BadRequest("تعداد کارت صفحه اصلی باید بین ۰ تا ۱۲ باشد.");
        if (request.HomeListCount < 0 || request.HomeListCount > 20) return BadRequest("تعداد فهرست صفحه اصلی باید بین ۰ تا ۲۰ باشد.");

        var settings = await _context.ContentSettings.FirstOrDefaultAsync();
        var isNew = settings == null;
        settings ??= new ContentSettings();

        settings.TipsEnabled = request.TipsEnabled;
        settings.AnnouncementsEnabled = request.AnnouncementsEnabled;
        settings.NewsCommentsEnabled = request.NewsCommentsEnabled;
        settings.TipsCommentsEnabled = request.TipsCommentsEnabled;
        settings.AnnouncementsCommentsEnabled = request.AnnouncementsCommentsEnabled;
        settings.ShowViewCount = request.ShowViewCount;
        settings.ShowTableOfContents = request.ShowTableOfContents;
        settings.ShowAuthorBox = request.ShowAuthorBox;
        settings.ShowRelatedContent = request.ShowRelatedContent;
        settings.ShowReadingTime = request.ShowReadingTime;
        settings.ShowReadingProgress = request.ShowReadingProgress;
        settings.FeaturedEnabled = request.FeaturedEnabled;
        settings.PinnedEnabled = request.PinnedEnabled;
        settings.ScheduledPublishingEnabled = request.ScheduledPublishingEnabled;
        settings.PageSize = request.PageSize;
        settings.RelatedCount = request.RelatedCount;
        settings.HomeCardCount = request.HomeCardCount;
        settings.HomeListCount = request.HomeListCount;
        settings.HomeTitle = string.IsNullOrWhiteSpace(request.HomeTitle) ? "آخرین اخبار" : request.HomeTitle.Trim();
        settings.HomeTypes = NormalizeTypeList(request.HomeTypes);
        settings.DefaultAuthorName = string.IsNullOrWhiteSpace(request.DefaultAuthorName) ? string.Empty : request.DefaultAuthorName.Trim();
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        if (isNew) _context.ContentSettings.Add(settings);
        await _context.SaveChangesAsync();
        await EvictCacheAsync();

        return Ok(ContentSettingsMapper.ToDto(settings));
    }

    private static string NormalizeTypeList(string? raw)
    {
        var parsed = ContentTags.Split(raw)
            .Select(ContentTypeCatalog.TryParse)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        return parsed.Count == 0 ? "News" : string.Join(",", parsed);
    }

    private static ContentCategoryDto ToDto(ContentCategory category, int count) => new()
    {
        Id = category.Id,
        Title = category.Title,
        Slug = category.Slug,
        ContentType = category.ContentType,
        SortOrder = category.SortOrder,
        IsActive = category.IsActive,
        ContentCount = count
    };
}
