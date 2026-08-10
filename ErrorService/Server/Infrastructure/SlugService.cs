using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Infrastructure;

public static class SlugService
{
    public static async Task<string> ResolveUniqueAsync(
        IQueryable<string> existingSlugs,
        string text,
        CancellationToken ct = default)
    {
        var candidate = SlugUtil.Slugify(text);
        var existing = await existingSlugs.ToListAsync(ct);
        if (!existing.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            return candidate;

        var i = 2;
        while (existing.Contains($"{candidate}-{i}", StringComparer.OrdinalIgnoreCase))
            i++;

        return $"{candidate}-{i}";
    }

    /// <summary>
    /// Keeps the current slug stable, unless the slug was machine-generated from
    /// the previous name (i.e. renames re-slug, manual-only edits keep the slug).
    /// </summary>
    public static async Task<string?> ResolveForUpdateAsync(
        IQueryable<string> existingSlugs,
        string oldName,
        string? currentSlug,
        string newName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(currentSlug) || SlugUtil.Slugify(oldName) != currentSlug)
            return currentSlug;

        var candidate = SlugUtil.Slugify(newName);
        var existing = await existingSlugs.ToListAsync(ct);
        if (!existing.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            return candidate;

        var i = 2;
        while (existing.Contains($"{candidate}-{i}", StringComparer.OrdinalIgnoreCase))
            i++;

        return $"{candidate}-{i}";
    }
}