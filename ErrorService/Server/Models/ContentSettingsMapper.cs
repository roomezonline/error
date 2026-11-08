using ErrorService.Shared;

namespace ErrorService.Server.Models;

public static class ContentSettingsMapper
{
    public static ContentSettingsDto ToDto(ContentSettings settings) => new()
    {
        TipsEnabled = settings.TipsEnabled,
        AnnouncementsEnabled = settings.AnnouncementsEnabled,
        NewsCommentsEnabled = settings.NewsCommentsEnabled,
        TipsCommentsEnabled = settings.TipsCommentsEnabled,
        AnnouncementsCommentsEnabled = settings.AnnouncementsCommentsEnabled,
        ShowViewCount = settings.ShowViewCount,
        ShowTableOfContents = settings.ShowTableOfContents,
        ShowAuthorBox = settings.ShowAuthorBox,
        ShowRelatedContent = settings.ShowRelatedContent,
        ShowReadingTime = settings.ShowReadingTime,
        ShowReadingProgress = settings.ShowReadingProgress,
        FeaturedEnabled = settings.FeaturedEnabled,
        PinnedEnabled = settings.PinnedEnabled,
        ScheduledPublishingEnabled = settings.ScheduledPublishingEnabled,
        PageSize = settings.PageSize,
        RelatedCount = settings.RelatedCount,
        HomeCardCount = settings.HomeCardCount,
        HomeListCount = settings.HomeListCount,
        HomeTitle = settings.HomeTitle,
        HomeTypes = settings.HomeTypes,
        DefaultAuthorName = settings.DefaultAuthorName ?? string.Empty
    };
}
