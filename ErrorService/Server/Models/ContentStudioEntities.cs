namespace ErrorService.Server.Models;

/// <summary>
/// تنظیمات سراسری استودیوی محتوا (تک‌ردیفی). فقط از پنل ادمین تغییر می‌کند.
/// </summary>
public sealed class ContentStudioSettings
{
    public int Id { get; set; }

    // ===== کلید اصلی =====
    public bool Enabled { get; set; }

    /// <summary>اگر true باشد، آیتم‌های آماده به‌صورت خودکار منتشر می‌شوند؛ در غیر این صورت در صف بازبینی می‌مانند.</summary>
    public bool AutoPublish { get; set; }

    /// <summary>ساعت اجرای روزانه به وقت تهران (0-23).</summary>
    public int DailyRunHour { get; set; } = 8;

    /// <summary>حداکثر تعداد آیتم جدید در هر اجرا (کنترل هزینه AI).</summary>
    public int MaxItemsPerRun { get; set; } = 3;

    /// <summary>حداکثر انتشار خودکار در هر روز.</summary>
    public int DailyPublishLimit { get; set; } = 3;

    // ===== کنترل کیفیت =====
    /// <summary>آستانه تشابه عنوان (۰ تا ۱). بالاتر = سخت‌گیرانه‌تر.</summary>
    public double DuplicateThreshold { get; set; } = 0.82;

    /// <summary>آستانه همپوشانی عبارت‌های متن بازنویسی‌شده با متن اصلی (درصد).</summary>
    public int MaxCopyOverlapPercent { get; set; } = 35;

    /// <summary>حداقل طول متن تولیدشده (کاراکتر) برای قبول شدن.</summary>
    public int MinContentChars { get; set; } = 500;

    public double AiTemperature { get; set; } = 0.5;

    public int AiMaxTokens { get; set; } = 2600;

    /// <summary>
    /// اگر فید فقط خلاصه کوتاه بدهد، متن کامل مقاله از لینک منبع دریافت و جایگزین متن ناقص می‌شود
    /// (هم در بازنویسی AI و هم در پیش‌نویس دستی).
    /// </summary>
    public bool FetchFullArticle { get; set; } = true;

    // ===== تگ و کلیدواژه =====
    /// <summary>افزودن کلیدواژه‌های استخراج‌شده از متن منبع (بدون AI) به تگ‌ها.</summary>
    public bool AutoKeywordTags { get; set; } = true;

    /// <summary>حداکثر تعداد تگ برای هر آیتم.</summary>
    public int MaxTagsPerItem { get; set; } = 10;

    // ===== هویت و انتساب منبع =====
    public string AuthorName { get; set; } = "تیم تحریریه ارورسرویس";

    /// <summary>درج خط «منبع» در انتهای متن (پیشنهاد: روشن بماند).</summary>
    public bool IncludeSourceCredit { get; set; } = true;

    /// <summary>
    /// اگر خاموش باشد، پایپ‌لاین بازنویسی AI انجام نمی‌دهد و متن منبع را به‌صورت پیش‌نویس دستی
    /// آماده می‌کند تا ادمین خودش ویرایش و منتشر کند (حالت گردآوری بدون هزینه AI).
    /// </summary>
    public bool UseAiRewrite { get; set; } = true;

    /// <summary>اگر AI خطا بدهد یا خروجی‌اش رد شود، به‌جای وضعیت «ناموفق» پیش‌نویس دستی ساخته می‌شود.</summary>
    public bool AutoDraftOnAiFailure { get; set; } = true;

    // ===== وضعیت اجرا =====
    public DateTimeOffset? LastRunAt { get; set; }

    public string? LastRunStatus { get; set; }

    public string? LastRunSummary { get; set; }

    /// <summary>تاریخ آخرین اجرای زمان‌بندی‌شده به وقت تهران (yyyy-MM-dd).</summary>
    public string? LastScheduledRunDate { get; set; }

    public int ScheduledRunAttempts { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// یک منبع خبری/آموزشی (RSS یا Atom) با قواعد اختصاصی خودش.
/// </summary>
public sealed class ContentStudioSource
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string FeedUrl { get; set; } = string.Empty;

    public string? SiteUrl { get; set; }

    /// <summary>News | Tutorial | Tip</summary>
    public string Category { get; set; } = "News";

    /// <summary>تگ‌های ثابت با کاما.</summary>
    public string? DefaultTags { get; set; }

    /// <summary>فقط آیتم‌هایی که یکی از این کلیدواژه‌ها را دارند (کاما). خالی = بدون فیلتر.</summary>
    public string? IncludeKeywords { get; set; }

    /// <summary>آیتم‌هایی که این کلیدواژه‌ها را دارند رد می‌شوند (کاما).</summary>
    public string? ExcludeKeywords { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>Inherit | Always | Never</summary>
    public string AutoPublishMode { get; set; } = "Inherit";

    public int MaxItemsPerDay { get; set; } = 2;

    /// <summary>None | OwnFallback | SourceHotlink</summary>
    public string ImageMode { get; set; } = "None";

    /// <summary>تصویر اختصاصی سایت برای حالت OwnFallback.</summary>
    public string? FallbackImageUrl { get; set; }

    public int Priority { get; set; } = 100;

    public DateTimeOffset? LastFetchedAt { get; set; }

    public string? LastStatus { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// آیتم دریافت‌شده از منبع؛ از لحظه ورود تا انتشار/رد قابل ردیابی است.
/// </summary>
public sealed class ContentStudioItem
{
    public int Id { get; set; }

    public int SourceId { get; set; }

    public ContentStudioSource? Source { get; set; }

    public string? ExternalId { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>SHA-256 آدرس یکسان‌شده برای ضدتکرار قطعی.</summary>
    public string SourceUrlHash { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public string? OriginalSummary { get; set; }

    public string? OriginalContent { get; set; }

    public string? OriginalImageUrl { get; set; }

    public DateTimeOffset? SourcePublishedAt { get; set; }

    public string? GeneratedTitle { get; set; }

    public string? GeneratedSummary { get; set; }

    public string? GeneratedContent { get; set; }

    public string? GeneratedTags { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>Pending | Ready | Published | Duplicate | Skipped | Rejected | Failed</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>متن نهایی توسط ادمین نوشته/ویرایش شده است (نه AI).</summary>
    public bool IsManualDraft { get; set; }

    public string? StatusDetail { get; set; }

    /// <summary>درصد همپوشانی متن تولیدشده با متن اصلی (ضدکپی).</summary>
    public double? CopyOverlapPercent { get; set; }

    /// <summary>بیشترین تشابه عنوان با محتوای موجود سایت.</summary>
    public double? TitleSimilarity { get; set; }

    public int? NewsId { get; set; }

    /// <summary>اگر خبر ساخته‌شده پیش‌نویس باشد false است.</summary>
    public bool NewsIsPublished { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}

/// <summary>
/// تاریخچه هر اجرای پایپ‌لاین (زمان‌بندی‌شده یا دستی) برای شفافیت و عیب‌یابی.
/// </summary>
public sealed class ContentStudioRun
{
    public int Id { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>schedule | manual</summary>
    public string Trigger { get; set; } = "manual";

    /// <summary>running | success | partial | failed</summary>
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
