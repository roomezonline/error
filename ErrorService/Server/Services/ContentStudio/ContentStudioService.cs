using ErrorService.Server.Data;
using ErrorService.Server.Infrastructure;
using ErrorService.Server.Models;
using ErrorService.Server.Services.ChatAi;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services.ContentStudio;

public static class ContentStudioClock
{
    private static readonly TimeZoneInfo Tehran = ResolveTehran();

    public static DateTimeOffset Now() => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Tehran);

    public static DateTimeOffset DayStartUtc()
    {
        var local = Now();
        return new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, local.Offset).ToUniversalTime();
    }

    public static string TodayKey() => Now().ToString("yyyy-MM-dd");

    public static DateTimeOffset? NextRunAt(int hour)
    {
        var local = Now();
        var target = new DateTimeOffset(local.Year, local.Month, local.Day, Math.Clamp(hour, 0, 23), 0, 0, local.Offset);
        if (target <= local) target = target.AddDays(1);
        return target.ToUniversalTime();
    }

    private static TimeZoneInfo ResolveTehran()
    {
        foreach (var id in new[] { "Iran Standard Time", "Asia/Tehran" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* try next */ }
        }
        return TimeZoneInfo.CreateCustomTimeZone("Iran", TimeSpan.FromHours(3.5), "Iran", "Iran");
    }
}

public sealed class ContentStudioService
{
    private readonly ErrorServiceDbContext _db;
    private readonly FeedFetcher _feeds;
    private readonly OpenAiCompatibleChatService _ai;
    private readonly ILogger<ContentStudioService> _logger;

    public ContentStudioService(
        ErrorServiceDbContext db,
        FeedFetcher feeds,
        OpenAiCompatibleChatService ai,
        ILogger<ContentStudioService> logger)
    {
        _db = db;
        _feeds = feeds;
        _ai = ai;
        _logger = logger;
    }

    public static string CategoryLabel(string? category) => category switch
    {
        "Tutorial" => "آموزش",
        "Tip" => "ترفند",
        _ => "خبر"
    };

    public static bool ShouldAutoPublish(ContentStudioSettings settings, ContentStudioSource source)
        => settings.AutoPublish && !string.Equals(source.AutoPublishMode, "Never", StringComparison.OrdinalIgnoreCase);

    // ===== تنظیمات =====

    public async Task<ContentStudioSettings> GetOrCreateSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _db.ContentStudioSettings.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (settings is not null) return settings;

        var now = DateTimeOffset.UtcNow;
        settings = new ContentStudioSettings { CreatedAt = now, UpdatedAt = now };
        _db.ContentStudioSettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    public async Task<ContentStudioSettings> UpdateSettingsAsync(ContentStudioSettingsDto dto, CancellationToken ct = default)
    {
        var settings = await GetOrCreateSettingsAsync(ct);
        settings.Enabled = dto.Enabled;
        settings.AutoPublish = dto.AutoPublish;
        settings.DailyRunHour = Math.Clamp(dto.DailyRunHour, 0, 23);
        settings.MaxItemsPerRun = Math.Clamp(dto.MaxItemsPerRun, 1, 10);
        settings.DailyPublishLimit = Math.Clamp(dto.DailyPublishLimit, 1, 20);
        settings.DuplicateThreshold = Math.Clamp(dto.DuplicateThreshold, 0.5, 1);
        settings.MaxCopyOverlapPercent = Math.Clamp(dto.MaxCopyOverlapPercent, 0, 100);
        settings.MinContentChars = Math.Clamp(dto.MinContentChars, 200, 5000);
        settings.AiTemperature = dto.AiTemperature is >= 0 and <= 2 ? dto.AiTemperature : 0.5;
        settings.AiMaxTokens = Math.Clamp(dto.AiMaxTokens, 600, 6000);
        settings.FetchFullArticle = dto.FetchFullArticle;
        settings.AutoKeywordTags = dto.AutoKeywordTags;
        settings.MaxTagsPerItem = Math.Clamp(dto.MaxTagsPerItem, 3, 20);
        settings.UseAiRewrite = dto.UseAiRewrite;
        settings.AutoDraftOnAiFailure = dto.AutoDraftOnAiFailure;

        var author = (dto.AuthorName ?? "").Trim();
        if (author.Length == 0) author = "تیم تحریریه ارورسرویس";
        if (author.Length > 100) author = author[..100];
        settings.AuthorName = author;
        settings.IncludeSourceCredit = dto.IncludeSourceCredit;
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return settings;
    }

    public async Task<bool> IsAiConfiguredAsync(CancellationToken ct = default)
    {
        var scopedProviders = await _db.ChatAiProviders.AsNoTracking()
            .Where(p => p.Enabled && p.UseForContentStudio)
            .Select(p => new
            {
                HasKey = p.ApiKeys.Any(k => k.Enabled),
                HasModel = p.Models.Any(m => m.Enabled)
            })
            .ToListAsync(ct);

        if (scopedProviders.Any(p => p.HasKey && p.HasModel)) return true;
        if (scopedProviders.Count > 0) return false;

        var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return settings is not null
            && !string.IsNullOrWhiteSpace(settings.ChatAiApiUrl)
            && !string.IsNullOrWhiteSpace(settings.ChatAiModel);
    }

    public async Task<ContentStudioAiTestResultDto> TestAiAsync(CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var result = await _ai.GenerateAsync(
            "تو یک دستیار فنی هستی.",
            "فقط و فقط این عبارت را بنویس: آماده",
            0.1,
            24,
            ct,
            budget: TimeSpan.FromSeconds(60),
            maxAttempts: 2);

        return new ContentStudioAiTestResultDto
        {
            Ok = !string.IsNullOrWhiteSpace(result.Content),
            Reply = string.IsNullOrWhiteSpace(result.Content) ? null : Shorten(result.Content, 200),
            Error = string.IsNullOrWhiteSpace(result.Content)
                ? result.Error ?? "پاسخی از مدل دریافت نشد (اتصال، کلید یا سهمیه را بررسی کنید)."
                : null,
            ElapsedMs = (long)(DateTime.UtcNow - started).TotalMilliseconds
        };
    }

    // ===== تست و کشف فید =====

    public async Task<ContentStudioTestFeedResultDto> TestFeedAsync(string feedUrl, CancellationToken ct = default)
    {
        try
        {
            var feed = await _feeds.FetchAsync(feedUrl, ct);
            return new ContentStudioTestFeedResultDto
            {
                Ok = true,
                FeedTitle = feed.Title,
                ItemCount = feed.Entries.Count,
                Samples = feed.Entries.Take(5).Select(e => e.Title).ToList(),
                LatestItemTitle = feed.Entries.FirstOrDefault()?.Title,
                LatestItemAt = feed.Entries.FirstOrDefault()?.PublishedAt
            };
        }
        catch (Exception ex)
        {
            // آدرس ممکن است صفحه سایت باشد؛ یک‌بار کشف خودکار انجام می‌دهیم تا ادمین دست‌خالی نماند.
            var result = new ContentStudioTestFeedResultDto { Ok = false, Error = Shorten(ex.Message, 300) };
            if (!ContentText.IsSafeHttpUrl(feedUrl)) return result;

            try
            {
                var discovery = await _feeds.DiscoverAsync(feedUrl, ct);
                var best = discovery.Best;
                if (best is not null)
                {
                    result.SuggestedFeedUrl = best.Url;
                    result.FeedTitle = best.Title;
                    result.ItemCount = best.ItemCount;
                    result.Error = $"{result.Error} — اما فید «{best.Url}» روی همین سایت پیدا شد ({best.ItemCount} آیتم).";
                }
                else
                {
                    result.Error = $"{result.Error} — روی همین دامنه هم فید معتبری پیدا نشد ({discovery.Probed} آدرس بررسی شد).";
                }
            }
            catch (Exception discoveryEx)
            {
                result.Error = $"{result.Error} — کشف خودکار هم ناموفق بود: {Shorten(discoveryEx.Message, 160)}";
            }

            return result;
        }
    }

    public async Task<ContentStudioFeedDiscoverResultDto> DiscoverFeedsAsync(string siteUrl, CancellationToken ct = default)
    {
        try
        {
            var discovery = await _feeds.DiscoverAsync(siteUrl, ct);
            var best = discovery.Best;

            var dto = new ContentStudioFeedDiscoverResultDto
            {
                Ok = best is not null,
                SiteUrl = siteUrl,
                SiteTitle = discovery.SiteTitle,
                SiteError = discovery.SiteError,
                Probed = discovery.Probed,
                Best = best?.Url,
                Feeds = discovery.Feeds.Select(f => f.Url).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Candidates = discovery.Results
                    .OrderByDescending(r => r.Ok)
                    .ThenByDescending(r => r.ItemCount)
                    .Select(r => new ContentStudioFeedCandidateDto
                    {
                        Method = r.Method,
                        Url = r.Url,
                        Ok = r.Ok,
                        Title = r.Title,
                        ItemCount = r.ItemCount,
                        LatestItemTitle = r.LatestTitle,
                        LatestItemAt = r.LatestPublishedAt,
                        HttpStatus = r.HttpStatus,
                        ContentType = r.ContentType,
                        Error = r.Error
                    })
                    .ToList()
            };

            dto.Error = best is not null
                ? null
                : discovery.SiteError is not null
                    ? $"صفحه سایت خوانده نشد ({discovery.SiteError}) و در {discovery.Probed} مسیر رایج هم فیدی پیدا نشد. آدرس فید را دستی وارد کنید."
                    : $"فید RSS/Atom روی این دامنه پیدا نشد ({discovery.Probed} آدرس بررسی شد). بعضی سایت‌ها فید ندارند؛ آدرس فید را دستی وارد کنید.";

            return dto;
        }
        catch (Exception ex)
        {
            return new ContentStudioFeedDiscoverResultDto { Ok = false, Error = Shorten(ex.Message, 300) };
        }
    }

    /// <summary>
    /// اگر ادمین فقط آدرس سایت را داده باشد، بهترین فید همان سایت را پیدا می‌کند.
    /// </summary>
    public async Task<(string? FeedUrl, string? Error)> ResolveFeedUrlAsync(string siteUrl, CancellationToken ct = default)
    {
        try
        {
            var discovery = await _feeds.DiscoverAsync(siteUrl, ct);
            var best = discovery.Best;
            return best is not null
                ? (best.Url, null)
                : (null, $"فید RSS/Atom روی «{siteUrl}» پیدا نشد ({discovery.Probed} آدرس بررسی شد). آدرس فید را دستی وارد کنید.");
        }
        catch (Exception ex)
        {
            return (null, "کشف فید انجام نشد: " + Shorten(ex.Message, 240));
        }
    }

    // ===== اجرای پایپ‌لاین =====

    private sealed class Counters
    {
        public int Fetched;
        public int Generated;
        public int ManualDrafts;
        public int Duplicates;
        public int Skipped;
        public int Published;
        public int Failed;
        public int SourcesChecked;
    }

    public async Task<ContentStudioRun> RunAsync(
        string trigger,
        int? sourceId,
        Action<ContentStudioProgressDto>? report,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(ct);
        var run = new ContentStudioRun
        {
            StartedAt = DateTimeOffset.UtcNow,
            Trigger = trigger,
            Status = "running"
        };
        _db.ContentStudioRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        var counters = new Counters();

        try
        {
            var sources = await _db.ContentStudioSources
                .Where(s => s.Enabled && (!sourceId.HasValue || s.Id == sourceId.Value))
                .OrderBy(s => s.Priority).ThenBy(s => s.Id)
                .ToListAsync(ct);

            if (sources.Count == 0)
            {
                run.Status = "failed";
                run.Message = "هیچ منبع فعالی برای بررسی وجود ندارد.";
            }
            else
            {
                await ProcessSourcesAsync(run, settings, sources, counters, report, ct);
                run.Status = counters.Failed > 0 ? "partial" : "success";
            }
        }
        catch (OperationCanceledException)
        {
            run.Status = "failed";
            run.Message = "اجرا لغو شد.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ContentStudio run failed");
            run.Status = "failed";
            run.Message = "خطای غیرمنتظره: " + Shorten(ex.Message, 600);
        }
        finally
        {
            var finished = DateTimeOffset.UtcNow;
            run.FinishedAt = finished;
            run.SourcesChecked = counters.SourcesChecked;
            run.Fetched = counters.Fetched;
            run.Generated = counters.Generated;
            run.Duplicates = counters.Duplicates;
            run.Skipped = counters.Skipped;
            run.Published = counters.Published;
            run.Failed = counters.Failed;
            run.Message ??= BuildSummary(run, counters.ManualDrafts);

            settings.LastRunAt = run.StartedAt;
            settings.LastRunStatus = run.Status;
            settings.LastRunSummary = run.Message;
            settings.UpdatedAt = finished;

            if (trigger == "schedule")
            {
                settings.LastScheduledRunDate = ContentStudioClock.TodayKey();
                // partial یعنی ممکن است بخشی از منابع خطا داده باشند؛ برای کنترل هزینه،
                // اجرای زمان‌بندی‌شده همان روز تکرار نمی‌شود (اجرای دستی همیشه ممکن است).
                if (run.Status != "failed") settings.ScheduledRunAttempts = 2;
            }

            try
            {
                await _db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ContentStudio: failed to persist run summary");
            }

            report?.Invoke(new ContentStudioProgressDto
            {
                IsRunning = false,
                Stage = "پایان",
                Processed = counters.SourcesChecked,
                Total = counters.SourcesChecked
            });
        }

        return run;
    }

    private async Task ProcessSourcesAsync(
        ContentStudioRun run,
        ContentStudioSettings settings,
        List<ContentStudioSource> sources,
        Counters counters,
        Action<ContentStudioProgressDto>? report,
        CancellationToken ct)
    {
        var dayStart = ContentStudioClock.DayStartUtc();
        var perSourceToday = await _db.ContentStudioItems.AsNoTracking()
            .Where(i => i.CreatedAt >= dayStart)
            .GroupBy(i => i.SourceId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var publishedToday = await _db.ContentStudioItems.AsNoTracking()
            .CountAsync(i => i.NewsIsPublished && i.PublishedAt != null && i.PublishedAt >= dayStart, ct);

        var publishSlots = Math.Max(0, settings.DailyPublishLimit - publishedToday);
        var itemSlots = Math.Clamp(settings.MaxItemsPerRun, 1, 10);
        var knownTitles = await LoadKnownTitlesAsync(ct);
        var seenHashes = new HashSet<string>(StringComparer.Ordinal);
        var processedSources = 0;

        report?.Invoke(Progress("شروع بررسی منابع", null, 0, sources.Count));

        foreach (var source in sources)
        {
            if (ct.IsCancellationRequested || itemSlots <= 0) break;

            processedSources++;
            report?.Invoke(Progress($"دریافت فید: {source.Name}", source.Name, processedSources - 1, sources.Count));

            var sourceQuota = Math.Max(0, Math.Clamp(source.MaxItemsPerDay, 1, 10) - perSourceToday.GetValueOrDefault(source.Id));
            if (sourceQuota <= 0)
            {
                counters.Skipped++;
                counters.SourcesChecked++;
                continue;
            }

            FeedDocument feed;
            try
            {
                feed = await _feeds.FetchAsync(source.FeedUrl, ct);
                source.LastFetchedAt = DateTimeOffset.UtcNow;
                source.LastStatus = "ok";
                source.LastError = null;
                if (string.IsNullOrWhiteSpace(source.SiteUrl) && !string.IsNullOrWhiteSpace(feed.SiteUrl))
                    source.SiteUrl = feed.SiteUrl;
            }
            catch (Exception ex)
            {
                source.LastFetchedAt = DateTimeOffset.UtcNow;
                source.LastStatus = "error";
                source.LastError = Shorten(ex.Message, 900);
                counters.Failed++;
                counters.SourcesChecked++;
                _logger.LogWarning(ex, "ContentStudio: feed failed for source {SourceId}", source.Id);
                await _db.SaveChangesAsync(ct);
                continue;
            }

            counters.SourcesChecked++;

            foreach (var entry in feed.Entries)
            {
                if (ct.IsCancellationRequested) break;
                if (itemSlots <= 0 || sourceQuota <= 0) break;

                if (entry.PublishedAt.HasValue && entry.PublishedAt.Value < DateTimeOffset.UtcNow.AddDays(-14))
                {
                    counters.Skipped++;
                    continue;
                }

                var plainLength = (entry.Content ?? entry.Summary ?? "").Length;
                if (plainLength < 120)
                {
                    counters.Skipped++;
                    continue;
                }

                if (!PassesKeywords(source, entry))
                {
                    counters.Skipped++;
                    continue;
                }

                var hash = ContentText.HashUrl(entry.Link);
                if (!seenHashes.Add(hash) || await _db.ContentStudioItems.AsNoTracking().AnyAsync(i => i.SourceUrlHash == hash, ct))
                {
                    counters.Duplicates++;
                    continue;
                }

                var similarity = MaxTitleSimilarity(entry.Title, knownTitles);
                if (similarity >= settings.DuplicateThreshold)
                {
                    var duplicate = CreateItem(source, entry, hash);
                    duplicate.Status = "Duplicate";
                    duplicate.StatusDetail = $"تکراری؛ تشابه عنوان {Math.Round(similarity * 100)}٪";
                    duplicate.TitleSimilarity = similarity;
                    _db.ContentStudioItems.Add(duplicate);
                    await TrySaveIgnoreConflictAsync(ct);
                    counters.Duplicates++;
                    knownTitles.Add(entry.Title);
                    continue;
                }

                var item = CreateItem(source, entry, hash);
                _db.ContentStudioItems.Add(item);
                if (!await TrySaveIgnoreConflictAsync(ct))
                {
                    counters.Duplicates++;
                    continue;
                }

                counters.Fetched++;

                report?.Invoke(Progress($"بازنویسی: {Shorten(entry.Title, 60)}", source.Name, processedSources - 1, sources.Count));

                var rewritten = await RewriteCoreAsync(item, source, settings, knownTitles, ct);

                // عنوان اصلی و تولیدشده برای ضدتکرارِ آیتم‌های بعدی ثبت می‌شود
                // (بعد از بازنویسی، تا عنوان خودِ آیتم با خودش مقایسه نشود).
                knownTitles.Add(entry.Title);
                if (!string.IsNullOrWhiteSpace(item.GeneratedTitle)) knownTitles.Add(item.GeneratedTitle);

                if (!rewritten)
                {
                    counters.Failed++;
                    continue;
                }

                counters.Generated++;
                if (item.IsManualDraft) counters.ManualDrafts++;

                if (publishSlots > 0 && ShouldAutoPublish(settings, source) && !item.IsManualDraft)
                {
                    if (await PublishAsync(item, asDraft: false, settings, ct))
                    {
                        counters.Published++;
                        publishSlots--;
                    }
                }

                itemSlots--;
                sourceQuota--;
                perSourceToday[source.Id] = perSourceToday.GetValueOrDefault(source.Id) + 1;
            }

            await _db.SaveChangesAsync(ct);
        }
    }

    private static ContentStudioItem CreateItem(ContentStudioSource source, FeedEntry entry, string hash)
    {
        var now = DateTimeOffset.UtcNow;
        return new ContentStudioItem
        {
            SourceId = source.Id,
            ExternalId = entry.ExternalId,
            SourceUrl = entry.Link.Length > 1000 ? entry.Link[..1000] : entry.Link,
            SourceUrlHash = hash,
            OriginalTitle = ContentText.CleanTitle(entry.Title, 400),
            OriginalSummary = entry.Summary,
            OriginalContent = entry.Content ?? entry.Summary,
            OriginalImageUrl = entry.ImageUrl,
            SourcePublishedAt = entry.PublishedAt,
            Status = "Pending",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private async Task<bool> TrySaveIgnoreConflictAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // ایندکس یکتای SourceUrlHash: آیتم در اجرای موازی/هم‌زمان ثبت شده است.
            foreach (var entry in _db.ChangeTracker.Entries<ContentStudioItem>()
                         .Where(e => e.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }
            return false;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is Microsoft.Data.SqlClient.SqlException sql
           && (sql.Number == 2601 || sql.Number == 2627);

    // ===== بازنویسی و بررسی کیفیت =====

    public async Task<ContentStudioItem?> RewriteItemAsync(int itemId, CancellationToken ct = default)
    {
        var item = await _db.ContentStudioItems.Include(i => i.Source).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item?.Source is null) return null;

        var settings = await GetOrCreateSettingsAsync(ct);
        var knownTitles = await LoadKnownTitlesAsync(ct, item.Id);
        await RewriteCoreAsync(item, item.Source, settings, knownTitles, ct);
        return item;
    }

    private async Task<bool> RewriteCoreAsync(
        ContentStudioItem item,
        ContentStudioSource source,
        ContentStudioSettings settings,
        List<string> knownTitles,
        CancellationToken ct)
    {
        item.Status = "Processing";
        item.StatusDetail = null;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (!settings.UseAiRewrite)
        {
            return await BuildManualDraftCoreAsync(item, source, settings,
                "حالت «بدون هوش مصنوعی» فعال است؛ متن منبع برای ویرایش و انتشار دستی آماده شد.", ct);
        }

        if (!await IsAiConfiguredAsync(ct))
        {
            return settings.AutoDraftOnAiFailure
                ? await BuildManualDraftCoreAsync(item, source, settings,
                    "هوش مصنوعی تنظیم نشده است؛ متن منبع به‌صورت پیش‌نویس دستی آماده شد تا خودتان ویرایش و منتشر کنید.", ct)
                : await FailAsync(item, "هوش مصنوعی تنظیم نشده است؛ در تنظیمات، ارائه‌دهنده/کلید/مدل را ثبت کنید یا حالت بدون AI را فعال کنید.", ct);
        }

        if (settings.FetchFullArticle)
            await TryEnsureFullArticleAsync(item, ct);

        var userPrompt = ContentStudioAiWriter.BuildUserPrompt(
            source.Name,
            CategoryLabel(source.Category),
            item.OriginalTitle,
            item.SourcePublishedAt,
            item.OriginalContent ?? item.OriginalSummary ?? "");

        var (article, aiError) = await GenerateArticleAsync(settings, userPrompt, strict: false, ct);
        if (article is null)
        {
            var detail = aiError ?? "پاسخ معتبر از AI دریافت نشد؛ اتصال/مدل/سهمیه را بررسی کنید.";
            return settings.AutoDraftOnAiFailure
                ? await BuildManualDraftCoreAsync(item, source, settings,
                    $"AI پاسخ نداد ({Shorten(detail, 240)})؛ پیش‌نویس از متن منبع ساخته شد تا دستی کامل کنید.", ct)
                : await FailAsync(item, detail, ct);
        }

        var bodyText = string.Join("\n", article.Sections.SelectMany(s => s.Paragraphs));
        var sourceText = (item.OriginalContent ?? "") + "\n" + (item.OriginalSummary ?? "");
        var overlap = ContentText.CopyOverlapPercent(bodyText, sourceText);

        if (overlap > settings.MaxCopyOverlapPercent)
        {
            var retryPrompt = ContentStudioAiWriter.BuildStrictRetryPrompt(userPrompt);
            var (retry, retryError) = await GenerateArticleAsync(settings, retryPrompt, strict: true, ct);
            if (retry is null)
            {
                var detail = retryError ?? "بازنویسی دوم ناموفق بود؛ متن اولیه بیش از حد به منبع نزدیک بود.";
                return settings.AutoDraftOnAiFailure
                    ? await BuildManualDraftCoreAsync(item, source, settings,
                        $"بازنویسی AI نیمه‌کاره ماند ({Shorten(detail, 220)})؛ پیش‌نویس دستی از متن منبع آماده شد.", ct)
                    : await FailAsync(item, detail, ct);
            }

            article = retry;
            bodyText = string.Join("\n", article.Sections.SelectMany(s => s.Paragraphs));
            overlap = ContentText.CopyOverlapPercent(bodyText, sourceText);
            if (overlap > settings.MaxCopyOverlapPercent)
            {
                item.CopyOverlapPercent = overlap;
                return settings.AutoDraftOnAiFailure
                    ? await BuildManualDraftCoreAsync(item, source, settings,
                        $"متن AI {overlap:0.#}٪ با منبع هم‌پوشانی داشت و کنار گذاشته شد؛ پیش‌نویس دستی از متن منبع آماده شد تا بازنویسی کنید.", ct)
                    : await FailAsync(item,
                        $"متن تولیدشده {overlap:0.#}٪ با منبع هم‌پوشانی دارد؛ برای جلوگیری از مشکل کپی‌رایت منتشر نشد.", ct);
            }
        }

        var contentLength = bodyText.Replace(" ", "").Replace("\n", "").Length;
        if (contentLength < settings.MinContentChars)
        {
            return settings.AutoDraftOnAiFailure
                ? await BuildManualDraftCoreAsync(item, source, settings,
                    $"متن AI ({contentLength} کاراکتر) کوتاه‌تر از حداقل {settings.MinContentChars} بود؛ پیش‌نویس دستی از متن منبع آماده شد.", ct)
                : await FailAsync(item,
                    $"متن تولیدشده ({contentLength} کاراکتر) کوتاه‌تر از حداقل تنظیم‌شده ({settings.MinContentChars}) است.", ct);
        }

        var title = ContentText.CleanTitle(article.Title, 200);
        if (title.Length < 10)
        {
            return settings.AutoDraftOnAiFailure
                ? await BuildManualDraftCoreAsync(item, source, settings,
                    "عنوان تولیدشده معتبر نبود؛ پیش‌نویس دستی از متن منبع آماده شد.", ct)
                : await FailAsync(item, "عنوان تولیدشده معتبر نیست.", ct);
        }

        var titleSimilarity = MaxTitleSimilarity(title, knownTitles);
        if (titleSimilarity >= settings.DuplicateThreshold)
        {
            item.Status = "Duplicate";
            item.StatusDetail = $"تکراری؛ عنوان تولیدشده {Math.Round(titleSimilarity * 100)}٪ به محتوای موجود شبیه است.";
            item.TitleSimilarity = titleSimilarity;
            item.CopyOverlapPercent = overlap;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return false;
        }

        item.GeneratedTitle = title;
        item.GeneratedSummary = ContentText.CleanTitle(article.Summary, 450);
        item.GeneratedTags = BuildTags(source, article.Tags, settings, title, bodyText);
        item.IsManualDraft = false;
        item.GeneratedContent = ContentText.BuildNewsHtml(
            article.Sections.Select(s => (s.Heading, s.Paragraphs)),
            article.KeyPoints,
            source.Name,
            source.SiteUrl ?? source.FeedUrl,
            settings.IncludeSourceCredit,
            imageCredit: string.Equals(source.ImageMode, "SourceHotlink", StringComparison.OrdinalIgnoreCase));
        item.ImageUrl = ResolveImage(source, item);
        item.CopyOverlapPercent = overlap;
        item.TitleSimilarity = titleSimilarity;
        item.Status = "Ready";
        item.StatusDetail = null;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<(GeneratedArticle? Article, string? Error)> GenerateArticleAsync(
        ContentStudioSettings settings, string userPrompt, bool strict, CancellationToken ct)
    {
        var temperature = strict ? Math.Min(settings.AiTemperature, 0.35) : settings.AiTemperature;
        var result = await _ai.GenerateAsync(
            ContentStudioAiWriter.SystemPrompt,
            userPrompt,
            temperature,
            settings.AiMaxTokens,
            ct);

        if (string.IsNullOrWhiteSpace(result.Content))
            return (null, result.Error ?? "پاسخی از مدل دریافت نشد.");

        var article = ContentStudioAiWriter.Parse(result.Content);
        if (article is null)
        {
            return (null,
                "پاسخ مدل JSON معتبر نبود. یک مدل دیگر (مثلاً Gemma 4 یا Nemotron) انتخاب کنید و سقف توکن خروجی را بالا ببرید.");
        }

        return (article, null);
    }

    private async Task<bool> FailAsync(ContentStudioItem item, string detail, CancellationToken ct)
    {
        item.Status = "Failed";
        item.StatusDetail = Shorten(detail, 900);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return false;
    }

    private static string? ResolveImage(ContentStudioSource source, ContentStudioItem item)
    {
        var mode = source.ImageMode ?? "None";
        string? url = mode switch
        {
            "OwnFallback" => source.FallbackImageUrl,
            "SourceHotlink" => item.OriginalImageUrl,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!ContentText.IsSafeHttpUrl(url) && !url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)) return null;
        return url.Length <= 500 ? url : null;
    }

    private static string BuildTags(
        ContentStudioSource source,
        IReadOnlyList<string> aiTags,
        ContentStudioSettings settings,
        string? keywordTitle,
        string? keywordBody)
    {
        var max = Math.Clamp(settings.MaxTagsPerItem, 3, 20);
        var tags = new List<string> { CategoryLabel(source.Category) };
        tags.AddRange(SplitKeywords(source.DefaultTags));

        if (settings.AutoKeywordTags)
            tags.AddRange(ContentText.ExtractKeywords(keywordTitle, keywordBody, Math.Clamp(max / 2, 3, 8)));

        tags.AddRange(aiTags);

        var cleaned = tags
            .Select(t => t.Trim().Trim('#'))
            .Where(t => t.Length is > 1 and <= 40)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();

        var joined = string.Join(", ", cleaned);
        return joined.Length > 450 ? joined[..450] : joined;
    }

    // ===== متن کامل مقاله =====

    private const int FullArticleMinChars = 1200;

    /// <summary>
    /// وقتی فید فقط خلاصه داده باشد، متن کامل را از صفحه منبع می‌گیرد و جایگزین متن ناقص می‌کند.
    /// در صورت شکست، متن فعلی حفظ می‌شود.
    /// </summary>
    private async Task<bool> TryEnsureFullArticleAsync(ContentStudioItem item, CancellationToken ct)
    {
        var existing = (item.OriginalContent ?? "").Trim();
        if (existing.Length >= FullArticleMinChars) return false;
        if (!ContentText.IsSafeHttpUrl(item.SourceUrl)) return false;

        try
        {
            var full = await _feeds.FetchArticleTextAsync(item.SourceUrl, 20000, ct);
            if (string.IsNullOrWhiteSpace(full) || full.Length < existing.Length + 200) return false;

            item.OriginalContent = full;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "دریافت متن کامل مقاله ناموفق بود: {Url}", item.SourceUrl);
            return false;
        }
    }

    // ===== پیش‌نویس دستی =====

    /// <summary>پرکردن فیلدهای خالی آیتم از متن منبع، بدون AI، تا ادمین بتواند ویرایش و انتشار کند.</summary>
    public async Task<(ContentStudioItem? Item, string? Error)> PrepareManualDraftAsync(
        int itemId, bool overwrite = false, CancellationToken ct = default)
    {
        var item = await _db.ContentStudioItems.Include(i => i.Source).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return (null, "آیتم یافت نشد.");
        if (item.NewsId.HasValue) return (item, "این آیتم قبلاً به خبر تبدیل شده و قابل ویرایش نیست.");

        var settings = await GetOrCreateSettingsAsync(ct);
        await FillManualDraftAsync(item, item.Source, settings, overwrite,
            "پیش‌نویس دستی آماده ویرایش است؛ متن را بررسی کنید و ذخیره بزنید.", ct);

        return (item, null);
    }

    /// <summary>
    /// ساخت پیش‌نویس دستی از متن منبع (بدون AI). در پایپ‌لاین هم وقتی AI خاموش است یا خطا می‌دهد استفاده می‌شود.
    /// </summary>
    private async Task<bool> BuildManualDraftCoreAsync(
        ContentStudioItem item,
        ContentStudioSource? source,
        ContentStudioSettings settings,
        string detail,
        CancellationToken ct)
    {
        try
        {
            await FillManualDraftAsync(item, source, settings, overwrite: false, detail, ct);

            // آیتم از حالت Processing خارج می‌شود حتی اگر متن قبلی داشت (بازنویسی دستی روی آیتم آماده).
            if (!string.Equals(item.Status, "Ready", StringComparison.Ordinal))
            {
                item.Status = "Ready";
                item.StatusDetail = detail;
                item.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ContentStudio: ساخت پیش‌نویس دستی ناموفق بود (item {ItemId})", item.Id);
            return await FailAsync(item, "ساخت پیش‌نویس دستی ناموفق بود: " + Shorten(ex.Message, 400), ct);
        }
    }

    private async Task<bool> FillManualDraftAsync(
        ContentStudioItem item,
        ContentStudioSource? source,
        ContentStudioSettings settings,
        bool overwrite,
        string? readyDetail,
        CancellationToken ct)
    {
        var contentWasEmpty = string.IsNullOrWhiteSpace(item.GeneratedContent);
        var changed = false;

        if ((overwrite || contentWasEmpty) && settings.FetchFullArticle)
            changed = await TryEnsureFullArticleAsync(item, ct);

        var plain = ContentText.HtmlToText(item.OriginalContent ?? item.OriginalSummary ?? item.OriginalTitle, 12000);
        if (string.IsNullOrWhiteSpace(plain)) plain = item.OriginalTitle;

        if (overwrite || string.IsNullOrWhiteSpace(item.GeneratedTitle))
        {
            item.GeneratedTitle = ContentText.CleanTitle(item.OriginalTitle, 200);
            changed = true;
        }

        if (overwrite || string.IsNullOrWhiteSpace(item.GeneratedSummary))
        {
            item.GeneratedSummary = !string.IsNullOrWhiteSpace(item.OriginalSummary)
                ? ContentText.CleanTitle(item.OriginalSummary, 450)
                : ContentText.CleanTitle(plain, 450);
            changed = true;
        }

        if (overwrite || contentWasEmpty)
        {
            item.GeneratedContent = ContentText.BuildManualHtml(
                plain,
                settings.IncludeSourceCredit && source is not null,
                source?.Name,
                source?.SiteUrl ?? source?.FeedUrl);
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(item.ImageUrl) && source is not null)
        {
            item.ImageUrl = ResolveImage(source, item);
            changed = true;
        }

        if ((overwrite || string.IsNullOrWhiteSpace(item.GeneratedTags)) && source is not null)
        {
            item.GeneratedTags = BuildTags(source, Array.Empty<string>(), settings, item.GeneratedTitle, plain);
            changed = true;
        }

        if (overwrite || contentWasEmpty)
        {
            item.IsManualDraft = true;
            item.Status = "Ready";
            item.StatusDetail = readyDetail;
            changed = true;
        }

        if (changed)
        {
            item.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return true;
    }

    /// <summary>ذخیره ویرایش دستی ادمین روی فیلدهای نهایی آیتم.</summary>
    public async Task<(ContentStudioItem? Item, string? Error)> SaveManualDraftAsync(
        int itemId, ContentStudioDraftRequest request, CancellationToken ct = default)
    {
        var item = await _db.ContentStudioItems.Include(i => i.Source).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return (null, "آیتم یافت نشد.");
        if (item.NewsId.HasValue) return (item, "این آیتم قبلاً به خبر تبدیل شده و قابل ویرایش نیست.");

        var text = (request.Content ?? "").Replace("\r\n", "\n");
        if (string.IsNullOrWhiteSpace(text))
            return (item, "متن خبر خالی است.");

        var title = ContentText.CleanTitle(request.Title, 200);
        if (title.Length < 5)
            return (item, "عنوان باید حداقل ۵ کاراکتر باشد.");

        var settings = await GetOrCreateSettingsAsync(ct);
        item.GeneratedTitle = title;
        item.GeneratedSummary = string.IsNullOrWhiteSpace(request.Summary)
            ? null
            : ContentText.CleanTitle(request.Summary, 450);
        item.GeneratedContent = ContentText.BuildManualHtml(text);
        item.GeneratedTags = string.IsNullOrWhiteSpace(request.Tags)
            ? (item.Source is not null
                ? BuildTags(item.Source, Array.Empty<string>(), settings, title, text)
                : string.Join(", ", ContentText.ExtractKeywords(title, text, settings.MaxTagsPerItem)))
            : CleanManualTags(request.Tags, settings.MaxTagsPerItem);
        item.IsManualDraft = true;
        item.Status = "Ready";
        item.StatusDetail = "پیش‌نویس دستی توسط ادمین ذخیره شد.";
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return (item, null);
    }

    private static string CleanManualTags(string? raw, int maxTags)
    {
        var tags = SplitKeywords(raw)
            .Select(t => t.Trim().Trim('#'))
            .Where(t => t.Length is > 1 and <= 40)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maxTags, 3, 20))
            .ToList();

        var joined = string.Join(", ", tags);
        return joined.Length > 450 ? joined[..450] : joined;
    }

    private static bool PassesKeywords(ContentStudioSource source, FeedEntry entry)
    {
        var haystack = ContentText.Normalize($"{entry.Title} {entry.Summary} {entry.Content}");
        if (haystack.Length == 0) return false;

        var includes = SplitKeywords(source.IncludeKeywords);
        if (includes.Count > 0)
        {
            var ok = includes.Any(k =>
            {
                var n = ContentText.Normalize(k);
                return n.Length > 0 && haystack.Contains(n, StringComparison.Ordinal);
            });
            if (!ok) return false;
        }

        var excludes = SplitKeywords(source.ExcludeKeywords);
        foreach (var keyword in excludes)
        {
            var n = ContentText.Normalize(keyword);
            if (n.Length > 0 && haystack.Contains(n, StringComparison.Ordinal)) return false;
        }

        return true;
    }

    private static List<string> SplitKeywords(string? raw)
        => string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split(new[] { ',', '،', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(k => k.Length > 0)
                .ToList();

    private static double MaxTitleSimilarity(string title, IReadOnlyCollection<string> knownTitles)
    {
        var max = 0d;
        foreach (var known in knownTitles)
        {
            var score = ContentText.TitleSimilarity(title, known);
            if (score > max) max = score;
            if (max >= 0.99) break;
        }
        return max;
    }

    private async Task<List<string>> LoadKnownTitlesAsync(CancellationToken ct, int? excludeItemId = null)
    {
        var newsTitles = await _db.News.AsNoTracking()
            .OrderByDescending(n => n.Id)
            .Take(500)
            .Select(n => n.Title)
            .ToListAsync(ct);

        var itemQuery = _db.ContentStudioItems.AsNoTracking()
            .Where(i => i.Status != "Rejected" && (!excludeItemId.HasValue || i.Id != excludeItemId.Value));
        var itemTitles = await itemQuery
            .OrderByDescending(i => i.Id)
            .Take(500)
            .Select(i => i.OriginalTitle)
            .ToListAsync(ct);

        return newsTitles
            .Concat(itemTitles)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ===== انتشار / رد / حذف =====

    public async Task<ContentStudioItem?> PublishItemAsync(int itemId, bool asDraft, CancellationToken ct = default)
    {
        var settings = await GetOrCreateSettingsAsync(ct);
        var item = await _db.ContentStudioItems.Include(i => i.Source).FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return null;

        if (item.NewsId.HasValue)
        {
            item.StatusDetail = "این آیتم قبلاً به خبر تبدیل شده است.";
            return item;
        }

        if (string.IsNullOrWhiteSpace(item.GeneratedTitle) || string.IsNullOrWhiteSpace(item.GeneratedContent))
        {
            item.StatusDetail = "متن تولیدشده آماده نیست؛ ابتدا «بازنویسی مجدد» را اجرا کنید.";
            await _db.SaveChangesAsync(ct);
            return item;
        }

        await PublishAsync(item, asDraft, settings, ct);
        return item;
    }

    public async Task<bool> PublishAsync(
        ContentStudioItem item,
        bool asDraft,
        ContentStudioSettings settings,
        CancellationToken ct = default)
    {
        if (item.NewsId.HasValue) return false;
        if (string.IsNullOrWhiteSpace(item.GeneratedTitle) || string.IsNullOrWhiteSpace(item.GeneratedContent)) return false;

        var now = DateTimeOffset.UtcNow;
        var source = item.Source ?? await _db.ContentStudioSources.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == item.SourceId, ct);
        var contentType = ContentTypeCatalog.FromStudioCategory(source?.Category) ?? ContentType.News;

        var news = new News
        {
            Title = item.GeneratedTitle,
            Slug = await SlugService.ResolveUniqueAsync(
                _db.News.Where(n => n.Slug != null).Select(n => n.Slug!),
                item.GeneratedTitle),
            Summary = item.GeneratedSummary,
            Content = item.GeneratedContent,
            ImageUrl = item.ImageUrl,
            Tags = item.GeneratedTags,
            AuthorName = settings.AuthorName,
            ContentType = contentType,
            SourceName = settings.IncludeSourceCredit ? source?.Name : null,
            SourceUrl = settings.IncludeSourceCredit ? item.SourceUrl : null,
            IsPublished = !asDraft,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.News.Add(news);
        await _db.SaveChangesAsync(ct);

        item.NewsId = news.Id;
        item.NewsIsPublished = !asDraft;
        item.Status = "Published";
        item.PublishedAt = now;
        item.UpdatedAt = now;
        item.StatusDetail = asDraft ? "به‌صورت پیش‌نویس در بخش اخبار ذخیره شد." : null;
        await _db.SaveChangesAsync(ct);

        SeoFallbackMiddleware.InvalidateHtmlCache();
        return true;
    }

    public async Task<ContentStudioItem?> RejectItemAsync(int itemId, CancellationToken ct = default)
    {
        var item = await _db.ContentStudioItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return null;
        if (item.Status == "Published") return item;

        item.Status = "Rejected";
        item.StatusDetail = "توسط ادمین رد شد.";
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return item;
    }

    public async Task<bool> DeleteItemAsync(int itemId, CancellationToken ct = default)
    {
        var item = await _db.ContentStudioItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item is null) return false;
        _db.ContentStudioItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>پاک‌سازی آیتم‌های بی‌مصرف قدیمی و تاریخچه اجراها (بدون دست‌زدن به اخبار منتشرشده).</summary>
    public async Task CleanupAsync(CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-45);
        var staleItems = await _db.ContentStudioItems
            .Where(i => i.CreatedAt < cutoff && (i.Status == "Rejected" || i.Status == "Duplicate" || i.Status == "Skipped"))
            .OrderBy(i => i.Id)
            .Take(2000)
            .ToListAsync(ct);
        if (staleItems.Count > 0)
        {
            _db.ContentStudioItems.RemoveRange(staleItems);
        }

        var oldRuns = await _db.ContentStudioRuns
            .OrderByDescending(r => r.Id)
            .Skip(200)
            .ToListAsync(ct);
        if (oldRuns.Count > 0)
        {
            _db.ContentStudioRuns.RemoveRange(oldRuns);
        }

        if (staleItems.Count > 0 || oldRuns.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    // ===== کمکی =====

    private static ContentStudioProgressDto Progress(string stage, string? source, int processed, int total)
        => new()
        {
            IsRunning = true,
            Stage = stage,
            CurrentSource = source,
            Processed = processed,
            Total = total,
            StartedAt = DateTimeOffset.UtcNow
        };

    private static string BuildSummary(ContentStudioRun run, int manualDrafts)
    {
        if (run.SourcesChecked == 0 && run.Fetched == 0 && run.Duplicates == 0 && run.Failed == 0)
            return "اجرا انجام شد؛ مورد جدیدی برای پردازش نبود.";

        var manual = manualDrafts > 0 ? $" · {manualDrafts} پیش‌نویس دستی (نیازمند ویرایش ادمین)" : "";
        return $"{run.SourcesChecked} منبع بررسی شد · {run.Fetched} مورد جدید · {run.Generated} آماده‌شده{manual} · {run.Duplicates} تکراری · {run.Skipped} رد‌شده · {run.Published} منتشرشده · {run.Failed} خطا";
    }

    private static string Shorten(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var s = value.Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }
}
