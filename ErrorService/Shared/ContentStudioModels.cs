using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

// ===== استودیوی محتوا (خودکارسازی اخبار/آموزش/ترفند) =====

public sealed class ContentStudioSettingsDto
{
    public bool Enabled { get; set; }
    public bool AutoPublish { get; set; }
    public int DailyRunHour { get; set; } = 8;
    public int MaxItemsPerRun { get; set; } = 3;
    public int DailyPublishLimit { get; set; } = 3;
    public double DuplicateThreshold { get; set; } = 0.82;
    public int MaxCopyOverlapPercent { get; set; } = 35;
    public int MinContentChars { get; set; } = 500;
    public double AiTemperature { get; set; } = 0.5;
    public int AiMaxTokens { get; set; } = 2600;
    public bool FetchFullArticle { get; set; } = true;
    public bool AutoKeywordTags { get; set; } = true;
    public int MaxTagsPerItem { get; set; } = 10;
    public string AuthorName { get; set; } = "تیم تحریریه ارورسرویس";
    public bool IncludeSourceCredit { get; set; } = true;

    /// <summary>اگر خاموش باشد، آیتم‌ها بدون AI به پیش‌نویس دستی تبدیل می‌شوند (حالت گردآوری).</summary>
    public bool UseAiRewrite { get; set; } = true;

    /// <summary>اگر AI خطا بدهد، به‌جای «ناموفق»، پیش‌نویس دستی ساخته شود.</summary>
    public bool AutoDraftOnAiFailure { get; set; } = true;

    public DateTimeOffset? LastRunAt { get; set; }
    public string? LastRunStatus { get; set; }
    public string? LastRunSummary { get; set; }
    public DateTimeOffset? NextScheduledRunAt { get; set; }
}

public sealed class ContentStudioSourceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public string? SiteUrl { get; set; }
    public string Category { get; set; } = "News";
    public string? DefaultTags { get; set; }
    public string? IncludeKeywords { get; set; }
    public string? ExcludeKeywords { get; set; }
    public bool Enabled { get; set; }
    public string AutoPublishMode { get; set; } = "Inherit";
    public int MaxItemsPerDay { get; set; }
    public string ImageMode { get; set; } = "None";
    public string? FallbackImageUrl { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset? LastFetchedAt { get; set; }
    public string? LastStatus { get; set; }
    public string? LastError { get; set; }
    public int PendingCount { get; set; }
    public int PublishedCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ContentStudioSourceRequest
{
    [Required(ErrorMessage = "نام منبع الزامی است")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// آدرس فید RSS/Atom. اگر خالی بماند، سرور آن را از روی آدرس سایت پیدا می‌کند.
    /// </summary>
    [MaxLength(1000)]
    public string? FeedUrl { get; set; }

    [MaxLength(1000)]
    public string? SiteUrl { get; set; }

    [Required]
    [MaxLength(20)]
    public string Category { get; set; } = "News";

    [MaxLength(500)]
    public string? DefaultTags { get; set; }

    [MaxLength(1000)]
    public string? IncludeKeywords { get; set; }

    [MaxLength(1000)]
    public string? ExcludeKeywords { get; set; }

    public bool Enabled { get; set; } = true;

    [MaxLength(20)]
    public string AutoPublishMode { get; set; } = "Inherit";

    [Range(1, 10)]
    public int MaxItemsPerDay { get; set; } = 2;

    [MaxLength(20)]
    public string ImageMode { get; set; } = "None";

    [MaxLength(1000)]
    public string? FallbackImageUrl { get; set; }

    [Range(1, 1000)]
    public int Priority { get; set; } = 100;
}

public sealed class ContentStudioItemDto
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string Category { get; set; } = "News";
    public string SourceUrl { get; set; } = string.Empty;
    public string OriginalTitle { get; set; } = string.Empty;
    public string? OriginalSummary { get; set; }
    public string? OriginalContent { get; set; }
    public string? OriginalImageUrl { get; set; }
    public DateTimeOffset? SourcePublishedAt { get; set; }

    public string? GeneratedTitle { get; set; }
    public string? GeneratedSummary { get; set; }
    public string? GeneratedContent { get; set; }
    /// <summary>نسخه متنی محتوا با نشانه‌گذاری ## تیتر و - فهرست، برای ویرایش.</summary>
    public string? GeneratedContentText { get; set; }
    public string? GeneratedTags { get; set; }
    public string? ImageUrl { get; set; }

    public string Status { get; set; } = "Pending";
    public bool IsManualDraft { get; set; }
    public string? StatusDetail { get; set; }
    public double? CopyOverlapPercent { get; set; }
    public double? TitleSimilarity { get; set; }

    public int? NewsId { get; set; }
    public bool NewsIsPublished { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class ContentStudioItemListDto
{
    public List<ContentStudioItemDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public Dictionary<string, int> Counts { get; set; } = new();
}

public sealed class ContentStudioStatsDto
{
    public int Pending { get; set; }
    public int Ready { get; set; }
    public int Published { get; set; }
    public int Duplicate { get; set; }
    public int Skipped { get; set; }
    public int Rejected { get; set; }
    public int Failed { get; set; }
    public int Sources { get; set; }
    public int EnabledSources { get; set; }
    public int TodayPublished { get; set; }
}

public sealed class ContentStudioRunDto
{
    public int Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Trigger { get; set; } = "manual";
    public string Status { get; set; } = "running";
    public string? Message { get; set; }
    public int SourcesChecked { get; set; }
    public int Fetched { get; set; }
    public int Generated { get; set; }
    public int Duplicates { get; set; }
    public int Skipped { get; set; }
    public int Published { get; set; }
    public int Failed { get; set; }
}

public sealed class ContentStudioAiStatusDto
{
    public bool Configured { get; set; }
    public int Providers { get; set; }
    public int Models { get; set; }
    public int Keys { get; set; }
    public string? Message { get; set; }
}

public sealed class ContentStudioProgressDto
{
    public bool IsRunning { get; set; }
    public string? Trigger { get; set; }
    public string? Stage { get; set; }
    public string? CurrentSource { get; set; }
    public int Processed { get; set; }
    public int Total { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
}

public sealed class ContentStudioOverviewDto
{
    public ContentStudioSettingsDto Settings { get; set; } = new();
    public ContentStudioStatsDto Stats { get; set; } = new();
    public ContentStudioAiStatusDto Ai { get; set; } = new();
    public ContentStudioProgressDto Progress { get; set; } = new();
    public List<ContentStudioRunDto> RecentRuns { get; set; } = new();
}

public sealed class ContentStudioTestFeedResultDto
{
    public bool Ok { get; set; }
    public string? FeedTitle { get; set; }
    public int ItemCount { get; set; }
    public string? Error { get; set; }
    public List<string> Samples { get; set; } = new();

    /// <summary>آدرس نهایی پس از ریدایرکت‌ها.</summary>
    public string? ResolvedUrl { get; set; }
    public string? ContentType { get; set; }
    public int? HttpStatus { get; set; }
    public string? LatestItemTitle { get; set; }
    public DateTimeOffset? LatestItemAt { get; set; }

    /// <summary>اگر آدرس واردشده سایت بود، بهترین فید پیدا‌شده برای جایگزینی.</summary>
    public string? SuggestedFeedUrl { get; set; }
}

/// <summary>یک آدرس کاندید فید که در فرایند کشف بررسی شده است.</summary>
public sealed class ContentStudioFeedCandidateDto
{
    /// <summary>direct | link | anchor | probe</summary>
    public string Method { get; set; } = "probe";
    public string Url { get; set; } = string.Empty;
    public bool Ok { get; set; }
    public string? Title { get; set; }
    public int ItemCount { get; set; }
    public string? LatestItemTitle { get; set; }
    public DateTimeOffset? LatestItemAt { get; set; }
    public int? HttpStatus { get; set; }
    public string? ContentType { get; set; }
    public string? Error { get; set; }
}

public sealed class ContentStudioFeedDiscoverResultDto
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? SiteUrl { get; set; }
    public string? SiteTitle { get; set; }
    public string? SiteError { get; set; }
    public int Probed { get; set; }

    /// <summary>آدرس فیدهای معتبر (سازگاری با نسخه قبل).</summary>
    public List<string> Feeds { get; set; } = new();

    /// <summary>همه کاندیدها با دلیل موفقیت/شکست.</summary>
    public List<ContentStudioFeedCandidateDto> Candidates { get; set; } = new();

    public string? Best { get; set; }
}

public sealed class ContentStudioPublishRequest
{
    /// <summary>true = فقط پیش‌نویس خبر ساخته شود.</summary>
    public bool AsDraft { get; set; }
}

public sealed class ContentStudioDraftRequest
{
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Summary { get; set; }

    [MaxLength(20000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Tags { get; set; }
}

public sealed class ContentStudioRunStartDto
{
    public bool Started { get; set; }
    public string? Message { get; set; }
}

public sealed class ContentStudioAiTestResultDto
{
    public bool Ok { get; set; }
    public string? Reply { get; set; }
    public string? Error { get; set; }
    public long ElapsedMs { get; set; }
}

public sealed class ContentStudioUrlRequest
{
    [Required(ErrorMessage = "آدرس الزامی است")]
    [MaxLength(1000)]
    public string Url { get; set; } = string.Empty;
}
