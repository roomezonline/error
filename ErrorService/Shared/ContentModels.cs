using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public enum ContentType
{
    News = 1,
    Tip = 2,
    Announcement = 3
}

public enum ContentStatus
{
    Draft = 0,
    Scheduled = 1,
    Published = 2,
    Archived = 3
}

public static class ContentTypeCatalog
{
    public static readonly ContentType[] All = { ContentType.News, ContentType.Tip, ContentType.Announcement };

    public static string Label(ContentType type) => type switch
    {
        ContentType.Tip => "ترفند",
        ContentType.Announcement => "اطلاعیه",
        _ => "خبر"
    };

    public static string PluralLabel(ContentType type) => type switch
    {
        ContentType.Tip => "ترفندها",
        ContentType.Announcement => "اطلاعیه‌ها",
        _ => "اخبار"
    };

    public static string Slug(ContentType type) => type switch
    {
        ContentType.Tip => "tips",
        ContentType.Announcement => "announcements",
        _ => "news"
    };

    public static string Route(ContentType type) => "/" + Slug(type);

    public static string DetailRoute(ContentType type, int id, string? slug)
        => $"{Route(type)}/{(string.IsNullOrWhiteSpace(slug) ? id.ToString() : slug)}";

    public static string CategoryRoute(ContentType type, string slug)
        => $"{Route(type)}/category/{Uri.EscapeDataString(slug)}";

    public static string TagRoute(ContentType type, string tag)
        => $"{Route(type)}/tag/{Uri.EscapeDataString(tag)}";

    public static string BadgeClass(ContentType type) => type switch
    {
        ContentType.Tip => "bg-warning-subtle text-warning",
        ContentType.Announcement => "bg-danger-subtle text-danger",
        _ => "bg-primary-subtle text-primary"
    };

    public static string Icon(ContentType type) => type switch
    {
        ContentType.Tip => "oi-lightbulb",
        ContentType.Announcement => "oi-bell",
        _ => "oi-list"
    };

    public static string ArticleSchemaType(ContentType type) => type switch
    {
        ContentType.News => "NewsArticle",
        _ => "Article"
    };

    public static ContentType FromSlug(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "tips" or "tip" or "2" => ContentType.Tip,
        "announcements" or "announcement" or "3" => ContentType.Announcement,
        _ => ContentType.News
    };

    public static ContentType? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Enum.TryParse<ContentType>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;
        return value.Trim().ToLowerInvariant() switch
        {
            "tips" => ContentType.Tip,
            "announcements" => ContentType.Announcement,
            "news" => ContentType.News,
            _ => null
        };
    }

    public static ContentType? FromStudioCategory(string? category) => category?.Trim().ToLowerInvariant() switch
    {
        "tip" => ContentType.Tip,
        "announcement" => ContentType.Announcement,
        "news" or "tutorial" => ContentType.News,
        _ => null
    };
}

public static class ContentStatusResolver
{
    public static ContentStatus Resolve(bool isPublished, DateTimeOffset? publishAt, DateTimeOffset? expireAt, DateTimeOffset now)
    {
        if (!isPublished) return ContentStatus.Draft;
        if (expireAt.HasValue && expireAt.Value <= now) return ContentStatus.Archived;
        if (publishAt.HasValue && publishAt.Value > now) return ContentStatus.Scheduled;
        return ContentStatus.Published;
    }

    public static bool IsVisible(bool isPublished, DateTimeOffset? publishAt, DateTimeOffset? expireAt, DateTimeOffset now)
        => Resolve(isPublished, publishAt, expireAt, now) == ContentStatus.Published;

    public static string Label(ContentStatus status) => status switch
    {
        ContentStatus.Scheduled => "زمان‌بندی‌شده",
        ContentStatus.Published => "منتشرشده",
        ContentStatus.Archived => "آرشیو",
        _ => "پیش‌نویس"
    };

    public static string BadgeClass(ContentStatus status) => status switch
    {
        ContentStatus.Scheduled => "bg-info-subtle text-info",
        ContentStatus.Published => "bg-success-subtle text-success",
        ContentStatus.Archived => "bg-secondary-subtle text-secondary",
        _ => "bg-warning-subtle text-warning"
    };
}

public static class ContentTags
{
    public static string Normalize(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags)) return string.Empty;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var part in tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var value = part.Trim().TrimStart('#');
            if (value.Length == 0 || value.Length > 60) continue;
            if (seen.Add(value)) result.Add(value);
        }
        return string.Join(",", result);
    }

    public static string[] Split(string? tags)
        => string.IsNullOrWhiteSpace(tags)
            ? Array.Empty<string>()
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string Slug(string tag) => SlugUtil.Slugify(tag, 120);
}

public sealed class ContentCategoryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public ContentType? ContentType { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public int ContentCount { get; set; }
}

public sealed class ContentCategoryUpsertRequest
{
    [Required(ErrorMessage = "عنوان دسته الزامی است")]
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? Slug { get; set; }

    public ContentType? ContentType { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class ContentTagDto
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class ContentTagRenameRequest
{
    [Required(ErrorMessage = "نام فعلی برچسب الزامی است")]
    [MaxLength(60)]
    public string OldTitle { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام جدید برچسب الزامی است")]
    [MaxLength(60)]
    public string NewTitle { get; set; } = string.Empty;
}

public sealed class ContentSettingsDto
{
    public bool TipsEnabled { get; set; } = true;
    public bool AnnouncementsEnabled { get; set; } = true;
    public bool NewsCommentsEnabled { get; set; } = true;
    public bool TipsCommentsEnabled { get; set; } = true;
    public bool AnnouncementsCommentsEnabled { get; set; }
    public bool ShowViewCount { get; set; } = true;
    public bool ShowTableOfContents { get; set; } = true;
    public bool ShowAuthorBox { get; set; } = true;
    public bool ShowRelatedContent { get; set; } = true;
    public bool ShowReadingTime { get; set; } = true;
    public bool ShowReadingProgress { get; set; } = true;
    public bool FeaturedEnabled { get; set; } = true;
    public bool PinnedEnabled { get; set; } = true;
    public bool ScheduledPublishingEnabled { get; set; } = true;
    public int PageSize { get; set; } = 12;
    public int RelatedCount { get; set; } = 3;
    public int HomeCardCount { get; set; } = 3;
    public int HomeListCount { get; set; } = 5;
    public string HomeTitle { get; set; } = "آخرین اخبار";
    public string HomeTypes { get; set; } = "News,Tip,Announcement";
    public string DefaultAuthorName { get; set; } = "تحریریه ارورسرویس";

    public bool IsTypeEnabled(ContentType type) => type switch
    {
        ContentType.Tip => TipsEnabled,
        ContentType.Announcement => AnnouncementsEnabled,
        _ => true
    };

    public bool AreCommentsEnabled(ContentType type) => type switch
    {
        ContentType.Tip => TipsCommentsEnabled,
        ContentType.Announcement => AnnouncementsCommentsEnabled,
        _ => NewsCommentsEnabled
    };

    public IReadOnlyList<ContentType> HomeContentTypes()
    {
        var list = new List<ContentType>();
        foreach (var part in ContentTags.Split(HomeTypes))
        {
            var parsed = ContentTypeCatalog.TryParse(part);
            if (parsed.HasValue && !list.Contains(parsed.Value) && IsTypeEnabled(parsed.Value))
                list.Add(parsed.Value);
        }
        if (list.Count == 0) list.Add(ContentType.News);
        return list;
    }
}

public sealed class ContentBulkRequest
{
    public List<int> Ids { get; set; } = new();

    /// <summary>publish | unpublish | archive | delete</summary>
    public string Action { get; set; } = string.Empty;
}
