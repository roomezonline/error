using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services.ChatAi;
using ErrorService.Server.Services.ContentStudio;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/admin/content-studio")]
[Authorize(Policy = "perm:admin.contentstudio.view")]
public sealed class ContentStudioController : ControllerBase
{
    private static readonly HashSet<string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        "News", "Tutorial", "Tip"
    };

    private static readonly HashSet<string> AutoPublishModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Inherit", "Never"
    };

    private static readonly HashSet<string> ImageModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "None", "OwnFallback", "SourceHotlink"
    };

    private readonly ErrorServiceDbContext _db;
    private readonly ContentStudioService _service;
    private readonly ContentStudioRunner _runner;
    private readonly IWebHostEnvironment _env;

    public ContentStudioController(
        ErrorServiceDbContext db,
        ContentStudioService service,
        ContentStudioRunner runner,
        IWebHostEnvironment env)
    {
        _db = db;
        _service = service;
        _runner = runner;
        _env = env;
    }

    // ===== نمای کلی =====

    [HttpGet("overview")]
    public async Task<ActionResult<ContentStudioOverviewDto>> GetOverview(CancellationToken ct)
    {
        var settings = await _service.GetOrCreateSettingsAsync(ct);
        var counts = await _db.ContentStudioItems.AsNoTracking()
            .GroupBy(i => i.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var sources = await _db.ContentStudioSources.AsNoTracking()
            .Select(s => new { s.Enabled })
            .ToListAsync(ct);

        var dayStart = ContentStudioClock.DayStartUtc();
        var todayPublished = await _db.ContentStudioItems.AsNoTracking()
            .CountAsync(i => i.NewsIsPublished && i.PublishedAt != null && i.PublishedAt >= dayStart, ct);

        var runs = await _db.ContentStudioRuns.AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Take(10)
            .ToListAsync(ct);

        return Ok(new ContentStudioOverviewDto
        {
            Settings = MapSettings(settings),
            Stats = new ContentStudioStatsDto
            {
                Pending = counts.GetValueOrDefault("Pending"),
                Ready = counts.GetValueOrDefault("Ready"),
                Published = counts.GetValueOrDefault("Published"),
                Duplicate = counts.GetValueOrDefault("Duplicate"),
                Skipped = counts.GetValueOrDefault("Skipped"),
                Rejected = counts.GetValueOrDefault("Rejected"),
                Failed = counts.GetValueOrDefault("Failed"),
                Sources = sources.Count,
                EnabledSources = sources.Count(s => s.Enabled),
                TodayPublished = todayPublished
            },
            Ai = await GetAiStatusAsync(ct),
            Progress = _runner.GetProgress(),
            RecentRuns = runs.Select(MapRun).ToList()
        });
    }

    [HttpPut("settings")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<ActionResult<ContentStudioSettingsDto>> UpdateSettings(
        [FromBody] ContentStudioSettingsDto dto, CancellationToken ct)
    {
        var settings = await _service.UpdateSettingsAsync(dto, ct);
        return Ok(MapSettings(settings));
    }

    // ===== منابع =====

    [HttpGet("sources")]
    public async Task<ActionResult<List<ContentStudioSourceDto>>> GetSources(CancellationToken ct)
    {
        var sources = await _db.ContentStudioSources.AsNoTracking()
            .OrderBy(s => s.Priority).ThenBy(s => s.Id)
            .ToListAsync(ct);

        var group = await _db.ContentStudioItems.AsNoTracking()
            .GroupBy(i => new { i.SourceId, i.Status })
            .Select(g => new { g.Key.SourceId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var pending = group.Where(g => g.Status is "Pending" or "Processing" or "Ready" or "Failed")
            .GroupBy(g => g.SourceId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
        var published = group.Where(g => g.Status == "Published")
            .GroupBy(g => g.SourceId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        return Ok(sources.Select(s => MapSource(
            s,
            pending.GetValueOrDefault(s.Id),
            published.GetValueOrDefault(s.Id))).ToList());
    }

    [HttpPost("sources")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<ActionResult<ContentStudioSourceDto>> CreateSource(
        [FromBody] ContentStudioSourceRequest request, CancellationToken ct)
    {
        if (ValidateSource(request) is { } error) return BadRequest(error);
        if (await ResolveFeedUrlIfMissingAsync(request, ct) is { } resolveError) return BadRequest(resolveError);

        var now = DateTimeOffset.UtcNow;
        var source = new ContentStudioSource
        {
            CreatedAt = now,
            UpdatedAt = now
        };
        ApplySource(source, request);

        _db.ContentStudioSources.Add(source);
        await _db.SaveChangesAsync(ct);
        return Ok(MapSource(source, 0, 0));
    }

    [HttpPut("sources/{id:int}")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<ActionResult<ContentStudioSourceDto>> UpdateSource(
        int id, [FromBody] ContentStudioSourceRequest request, CancellationToken ct)
    {
        if (ValidateSource(request) is { } error) return BadRequest(error);

        var source = await _db.ContentStudioSources.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (source is null) return NotFound("منبع یافت نشد.");

        if (await ResolveFeedUrlIfMissingAsync(request, ct) is { } resolveError) return BadRequest(resolveError);

        ApplySource(source, request);
        source.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(MapSource(source, 0, 0));
    }

    /// <summary>
    /// اگر ادمین فقط «آدرس سایت» را داده باشد، بهترین فید همان دامنه پیدا و در FeedUrl قرار می‌گیرد.
    /// </summary>
    private async Task<string?> ResolveFeedUrlIfMissingAsync(ContentStudioSourceRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.FeedUrl)) return null;
        if (string.IsNullOrWhiteSpace(request.SiteUrl)) return "آدرس سایت یا آدرس فید را وارد کنید.";

        var site = request.SiteUrl.Trim();
        if (!ContentText.IsSafeHttpUrl(site)) return "آدرس سایت معتبر نیست (فقط http/https عمومی).";

        var (feedUrl, error) = await _service.ResolveFeedUrlAsync(site, ct);
        if (feedUrl is null) return error;

        request.FeedUrl = feedUrl;
        return null;
    }

    [HttpDelete("sources/{id:int}")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<IActionResult> DeleteSource(int id, CancellationToken ct)
    {
        var source = await _db.ContentStudioSources.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (source is null) return NotFound("منبع یافت نشد.");

        _db.ContentStudioSources.Remove(source);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("sources/test")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<ActionResult<ContentStudioTestFeedResultDto>> TestSource(
        [FromBody] ContentStudioUrlRequest request, CancellationToken ct)
    {
        if (!ContentText.IsSafeHttpUrl(request.Url))
            return BadRequest("آدرس فید معتبر نیست (فقط http/https عمومی).");

        return Ok(await _service.TestFeedAsync(request.Url.Trim(), ct));
    }

    [HttpPost("sources/discover")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<ActionResult<ContentStudioFeedDiscoverResultDto>> DiscoverFeeds(
        [FromBody] ContentStudioUrlRequest request, CancellationToken ct)
    {
        if (!ContentText.IsSafeHttpUrl(request.Url))
            return BadRequest("آدرس سایت معتبر نیست (فقط http/https عمومی).");

        return Ok(await _service.DiscoverFeedsAsync(request.Url.Trim(), ct));
    }

    [HttpPost("upload")]
    [RequestSizeLimit(15_000_000)]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<ActionResult<string>> UploadImage([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length <= 0) return BadRequest("فایل خالی است یا دریافت نشد.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return BadRequest("فرمت تصویر مجاز نیست؛ فقط jpg، png یا webp.");

        var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var relativeFolder = "uploads/content-studio";
        var uploadsRoot = Path.Combine(wwwroot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        await using (var output = System.IO.File.Create(Path.Combine(uploadsRoot, fileName)))
        {
            await file.CopyToAsync(output, ct);
        }

        return Ok($"/{relativeFolder}/{fileName}");
    }

    [HttpPost("sources/{id:int}/fetch")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<ActionResult<ContentStudioRunStartDto>> FetchSourceNow(int id, CancellationToken ct)
    {
        var exists = await _db.ContentStudioSources.AsNoTracking().AnyAsync(s => s.Id == id, ct);
        if (!exists) return NotFound("منبع یافت نشد.");

        if (!_runner.TryStart("source", id))
            return Conflict(new ContentStudioRunStartDto { Started = false, Message = "یک اجرا در حال انجام است؛ کمی بعد تلاش کنید." });

        return Accepted(new ContentStudioRunStartDto { Started = true, Message = "دریافت این منبع در پس‌زمینه شروع شد." });
    }

    // ===== اجرا و AI =====

    [HttpPost("run")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public ActionResult<ContentStudioRunStartDto> RunNow()
    {
        if (!_runner.TryStart("manual"))
            return Conflict(new ContentStudioRunStartDto { Started = false, Message = "یک اجرا در حال انجام است؛ کمی بعد تلاش کنید." });

        return Accepted(new ContentStudioRunStartDto { Started = true, Message = "اجرای دستی در پس‌زمینه شروع شد." });
    }

    [HttpPost("ai-test")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<ActionResult<ContentStudioAiTestResultDto>> TestAi(CancellationToken ct)
        => Ok(await _service.TestAiAsync(ct));

    [HttpGet("runs")]
    public async Task<ActionResult<List<ContentStudioRunDto>>> GetRuns(int take = 30, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);
        var runs = await _db.ContentStudioRuns.AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Take(take)
            .ToListAsync(ct);
        return Ok(runs.Select(MapRun).ToList());
    }

    // ===== صف آیتم‌ها =====

    [HttpGet("items")]
    public async Task<ActionResult<ContentStudioItemListDto>> GetItems(
        string? status = null,
        int? sourceId = null,
        string? q = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.ContentStudioItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.Status == status);
        }
        if (sourceId.HasValue)
            query = query.Where(i => i.SourceId == sourceId.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(i =>
                EF.Functions.Like(i.OriginalTitle, $"%{term}%") ||
                (i.GeneratedTitle != null && EF.Functions.Like(i.GeneratedTitle, $"%{term}%")));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new ContentStudioItemDto
            {
                Id = i.Id,
                SourceId = i.SourceId,
                SourceName = i.Source != null ? i.Source.Name : "",
                Category = i.Source != null ? i.Source.Category : "News",
                SourceUrl = i.SourceUrl,
                OriginalTitle = i.OriginalTitle,
                OriginalSummary = i.OriginalSummary,
                OriginalContent = null,
                OriginalImageUrl = i.OriginalImageUrl,
                SourcePublishedAt = i.SourcePublishedAt,
                GeneratedTitle = i.GeneratedTitle,
                GeneratedSummary = i.GeneratedSummary,
                GeneratedContent = i.GeneratedContent,
                GeneratedTags = i.GeneratedTags,
                ImageUrl = i.ImageUrl,
                Status = i.Status,
                IsManualDraft = i.IsManualDraft,
                StatusDetail = i.StatusDetail,
                CopyOverlapPercent = i.CopyOverlapPercent,
                TitleSimilarity = i.TitleSimilarity,
                NewsId = i.NewsId,
                NewsIsPublished = i.NewsIsPublished,
                CreatedAt = i.CreatedAt,
                PublishedAt = i.PublishedAt
            })
            .ToListAsync(ct);

        var counts = await _db.ContentStudioItems.AsNoTracking()
            .GroupBy(i => i.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return Ok(new ContentStudioItemListDto
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
            Counts = counts
        });
    }

    [HttpGet("items/{id:int}")]
    public async Task<ActionResult<ContentStudioItemDto>> GetItem(int id, CancellationToken ct)
    {
        var item = await _db.ContentStudioItems.AsNoTracking()
            .Include(i => i.Source)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return NotFound("آیتم یافت نشد.");

        var dto = MapItem(item);
        dto.OriginalContent = item.OriginalContent;
        return Ok(dto);
    }

    [HttpPost("items/{id:int}/rewrite")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<ActionResult<ContentStudioRunStartDto>> RewriteItem(int id, CancellationToken ct)
    {
        var exists = await _db.ContentStudioItems.AsNoTracking().AnyAsync(i => i.Id == id, ct);
        if (!exists) return NotFound("آیتم یافت نشد.");

        if (!_runner.TryStartRewrite(id))
            return Conflict(new ContentStudioRunStartDto { Started = false, Message = "یک کار در حال انجام است؛ کمی بعد تلاش کنید." });

        return Accepted(new ContentStudioRunStartDto { Started = true, Message = "بازنویسی در پس‌زمینه شروع شد." });
    }

    [HttpPost("items/{id:int}/draft/prepare")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<ActionResult<ContentStudioItemDto>> PrepareManualDraft(
        int id, bool overwrite = false, CancellationToken ct = default)
    {
        var (item, error) = await _service.PrepareManualDraftAsync(id, overwrite, ct);
        if (item is null) return NotFound(error ?? "آیتم یافت نشد.");
        if (error is not null) return Conflict(new { message = error });
        return Ok(MapItem(item));
    }

    [HttpPut("items/{id:int}/draft")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<ActionResult<ContentStudioItemDto>> SaveManualDraft(
        int id, [FromBody] ContentStudioDraftRequest request, CancellationToken ct)
    {
        var (item, error) = await _service.SaveManualDraftAsync(id, request, ct);
        if (item is null) return NotFound(error ?? "آیتم یافت نشد.");
        if (error is not null) return Conflict(new { message = error });
        return Ok(MapItem(item));
    }

    [HttpPost("items/{id:int}/publish")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<ActionResult<ContentStudioItemDto>> PublishItem(
        int id, [FromBody] ContentStudioPublishRequest? request, CancellationToken ct)
    {
        var item = await _service.PublishItemAsync(id, request?.AsDraft ?? false, ct);
        if (item is null) return NotFound("آیتم یافت نشد.");

        if (!item.NewsId.HasValue)
            return Conflict(new ContentStudioItemDto
            {
                Id = item.Id,
                Status = item.Status,
                StatusDetail = item.StatusDetail
            });

        return Ok(MapItem(item));
    }

    [HttpPost("items/{id:int}/reject")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<ActionResult<ContentStudioItemDto>> RejectItem(int id, CancellationToken ct)
    {
        var item = await _service.RejectItemAsync(id, ct);
        if (item is null) return NotFound("آیتم یافت نشد.");
        return Ok(MapItem(item));
    }

    [HttpDelete("items/{id:int}")]
    [Authorize(Policy = "perm:admin.contentstudio.manage")]
    public async Task<IActionResult> DeleteItem(int id, CancellationToken ct)
    {
        var deleted = await _service.DeleteItemAsync(id, ct);
        return deleted ? NoContent() : NotFound("آیتم یافت نشد.");
    }

    // ===== کمکی =====

    private async Task<ContentStudioAiStatusDto> GetAiStatusAsync(CancellationToken ct)
    {
        var providers = await _db.ChatAiProviders.AsNoTracking()
            .Where(p => p.Enabled && p.UseForContentStudio)
            .Select(p => new
            {
                Keys = p.ApiKeys.Count(k => k.Enabled),
                Models = p.Models.Count(m => m.Enabled)
            })
            .ToListAsync(ct);

        var keys = providers.Sum(p => p.Keys);
        var models = providers.Sum(p => p.Models);
        var configured = providers.Any(p => p.Keys > 0 && p.Models > 0);

        if (!configured && providers.Count == 0)
        {
            var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            configured = settings is not null
                && !string.IsNullOrWhiteSpace(settings.ChatAiApiUrl)
                && !string.IsNullOrWhiteSpace(settings.ChatAiModel);
            if (configured)
            {
                keys = 1;
                models = 1;
            }
        }

        return new ContentStudioAiStatusDto
        {
            Configured = configured,
            Providers = providers.Count(p => p.Keys > 0 && p.Models > 0),
            Models = models,
            Keys = keys,
            Message = configured
                ? $"زنجیره AI آماده است ({providers.Count(p => p.Keys > 0 && p.Models > 0)} ارائه‌دهنده، {models} مدل، {keys} کلید فعال)."
                : "هوش مصنوعی تنظیم نشده است؛ استودیو در حالت «پیش‌نویس دستی» کار می‌کند (دریافت فید و آماده‌سازی متن برای ویرایش ادمین). برای بازنویسی خودکار، در تنظیمات هوش مصنوعی کاربرد «استودیوی محتوا» را برای حداقل یک سرویس‌دهنده فعال کنید."
        };
    }

    private static string? ValidateSource(ContentStudioSourceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "نام منبع الزامی است.";
        if (request.Name.Trim().Length > 150) return "نام منبع حداکثر ۱۵۰ کاراکتر است.";

        var hasFeed = !string.IsNullOrWhiteSpace(request.FeedUrl);
        var hasSite = !string.IsNullOrWhiteSpace(request.SiteUrl);
        if (!hasFeed && !hasSite)
            return "آدرس سایت را وارد کنید؛ فید RSS/Atom به‌صورت خودکار از همان سایت پیدا می‌شود (یا آدرس فید را دستی بدهید).";

        if (hasFeed && !ContentText.IsSafeHttpUrl(request.FeedUrl))
            return "آدرس فید معتبر نیست (فقط http/https عمومی).";

        if (!Categories.Contains(request.Category)) return "دسته محتوا معتبر نیست.";
        if (!AutoPublishModes.Contains(request.AutoPublishMode)) return "حالت انتشار خودکار معتبر نیست.";
        if (!ImageModes.Contains(request.ImageMode)) return "حالت تصویر معتبر نیست.";

        if (!string.IsNullOrWhiteSpace(request.SiteUrl) && !ContentText.IsSafeHttpUrl(request.SiteUrl))
            return "آدرس سایت معتبر نیست.";

        if (string.Equals(request.ImageMode, "OwnFallback", StringComparison.OrdinalIgnoreCase))
        {
            var image = request.FallbackImageUrl?.Trim() ?? "";
            var isLocal = image.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase);
            if (image.Length == 0 || (!isLocal && !ContentText.IsSafeHttpUrl(image)))
                return "برای حالت «تصویر اختصاصی»، یک تصویر آپلود کنید یا آدرس معتبر بدهید.";
        }

        return null;
    }

    private static void ApplySource(ContentStudioSource source, ContentStudioSourceRequest request)
    {
        source.Name = request.Name.Trim();
        source.FeedUrl = ContentText.CanonicalizeUrl(request.FeedUrl) ?? (request.FeedUrl ?? "").Trim();
        source.SiteUrl = string.IsNullOrWhiteSpace(request.SiteUrl)
            ? DeriveOrigin(source.FeedUrl)
            : (ContentText.CanonicalizeUrl(request.SiteUrl) ?? request.SiteUrl.Trim());
        source.Category = NormalizeCategory(request.Category);
        source.DefaultTags = TrimOrNull(request.DefaultTags, 500);
        source.IncludeKeywords = TrimOrNull(request.IncludeKeywords, 1000);
        source.ExcludeKeywords = TrimOrNull(request.ExcludeKeywords, 1000);
        source.Enabled = request.Enabled;
        source.AutoPublishMode = request.AutoPublishMode.Equals("Never", StringComparison.OrdinalIgnoreCase) ? "Never" : "Inherit";
        source.MaxItemsPerDay = Math.Clamp(request.MaxItemsPerDay, 1, 10);
        source.ImageMode = NormalizeImageMode(request.ImageMode);
        source.FallbackImageUrl = string.IsNullOrWhiteSpace(request.FallbackImageUrl) ? null : request.FallbackImageUrl.Trim();
        source.Priority = Math.Clamp(request.Priority, 1, 1000);
    }

    private static string? DeriveOrigin(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;

    private static string NormalizeCategory(string value) => value.ToLowerInvariant() switch
    {
        "tutorial" => "Tutorial",
        "tip" => "Tip",
        _ => "News"
    };

    private static string NormalizeImageMode(string value) => value.ToLowerInvariant() switch
    {
        "ownfallback" => "OwnFallback",
        "sourcehotlink" => "SourceHotlink",
        _ => "None"
    };

    private static string? TrimOrNull(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static ContentStudioSettingsDto MapSettings(ContentStudioSettings s) => new()
    {
        Enabled = s.Enabled,
        AutoPublish = s.AutoPublish,
        DailyRunHour = s.DailyRunHour,
        MaxItemsPerRun = s.MaxItemsPerRun,
        DailyPublishLimit = s.DailyPublishLimit,
        DuplicateThreshold = s.DuplicateThreshold,
        MaxCopyOverlapPercent = s.MaxCopyOverlapPercent,
        MinContentChars = s.MinContentChars,
        AiTemperature = s.AiTemperature,
        AiMaxTokens = s.AiMaxTokens,
        FetchFullArticle = s.FetchFullArticle,
        AutoKeywordTags = s.AutoKeywordTags,
        MaxTagsPerItem = s.MaxTagsPerItem,
        AuthorName = s.AuthorName,
        IncludeSourceCredit = s.IncludeSourceCredit,
        UseAiRewrite = s.UseAiRewrite,
        AutoDraftOnAiFailure = s.AutoDraftOnAiFailure,
        LastRunAt = s.LastRunAt,
        LastRunStatus = s.LastRunStatus,
        LastRunSummary = s.LastRunSummary,
        NextScheduledRunAt = s.Enabled ? ContentStudioClock.NextRunAt(s.DailyRunHour) : null
    };

    private static ContentStudioSourceDto MapSource(ContentStudioSource s, int pending, int published) => new()
    {
        Id = s.Id,
        Name = s.Name,
        FeedUrl = s.FeedUrl,
        SiteUrl = s.SiteUrl,
        Category = s.Category,
        DefaultTags = s.DefaultTags,
        IncludeKeywords = s.IncludeKeywords,
        ExcludeKeywords = s.ExcludeKeywords,
        Enabled = s.Enabled,
        AutoPublishMode = s.AutoPublishMode,
        MaxItemsPerDay = s.MaxItemsPerDay,
        ImageMode = s.ImageMode,
        FallbackImageUrl = s.FallbackImageUrl,
        Priority = s.Priority,
        LastFetchedAt = s.LastFetchedAt,
        LastStatus = s.LastStatus,
        LastError = s.LastError,
        PendingCount = pending,
        PublishedCount = published,
        CreatedAt = s.CreatedAt
    };

    private static ContentStudioItemDto MapItem(ContentStudioItem i) => new()
    {
        Id = i.Id,
        SourceId = i.SourceId,
        SourceName = i.Source?.Name ?? "",
        Category = i.Source?.Category ?? "News",
        SourceUrl = i.SourceUrl,
        OriginalTitle = i.OriginalTitle,
        OriginalSummary = i.OriginalSummary,
        OriginalContent = i.OriginalContent,
        OriginalImageUrl = i.OriginalImageUrl,
        SourcePublishedAt = i.SourcePublishedAt,
        GeneratedTitle = i.GeneratedTitle,
        GeneratedSummary = i.GeneratedSummary,
        GeneratedContent = i.GeneratedContent,
        GeneratedContentText = ContentText.ToEditableText(i.GeneratedContent),
        GeneratedTags = i.GeneratedTags,
        ImageUrl = i.ImageUrl,
        Status = i.Status,
        IsManualDraft = i.IsManualDraft,
        StatusDetail = i.StatusDetail,
        CopyOverlapPercent = i.CopyOverlapPercent,
        TitleSimilarity = i.TitleSimilarity,
        NewsId = i.NewsId,
        NewsIsPublished = i.NewsIsPublished,
        CreatedAt = i.CreatedAt,
        PublishedAt = i.PublishedAt
    };

    private static ContentStudioRunDto MapRun(ContentStudioRun r) => new()
    {
        Id = r.Id,
        StartedAt = r.StartedAt,
        FinishedAt = r.FinishedAt,
        Trigger = r.Trigger,
        Status = r.Status,
        Message = r.Message,
        SourcesChecked = r.SourcesChecked,
        Fetched = r.Fetched,
        Generated = r.Generated,
        Duplicates = r.Duplicates,
        Skipped = r.Skipped,
        Published = r.Published,
        Failed = r.Failed
    };
}
