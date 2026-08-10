using System.Text;

namespace ErrorService.Shared;

public static class SlugUtil
{
    public static string Slugify(string text, int maxLength = 190)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "item";

        var norm = text.Replace('ي', 'ی').Replace('ك', 'ک').Replace('ۀ', 'ه');
        var sb = new StringBuilder(norm.Length);

        foreach (var c in norm.Trim())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (char.IsWhiteSpace(c))
                sb.Append('-');
            else if (c is '-' or '_' or '+' or '.' or '%' or '&' or '/')
                sb.Append('-');
        }

        var slug = sb.ToString();
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-').ToLowerInvariant();

        return slug.Length > maxLength ? slug[..maxLength].Trim('-') : slug;
    }
}

public static class SeoUrl
{
    public static string Product(int id, string? slug)
        => string.IsNullOrEmpty(slug) ? $"/products/{id}" : $"/products/{slug}";

    public static string News(int id, string? slug)
        => string.IsNullOrEmpty(slug) ? $"/news/{id}" : $"/news/{slug}";

    public static string Course(int id, string? slug)
        => string.IsNullOrEmpty(slug) ? $"/academy/{id}" : $"/academy/{slug}";

    public static string Article(int id, string? slug)
        => string.IsNullOrEmpty(slug) ? $"/academy/articles/{id}" : $"/academy/articles/{slug}";

    public static string Lesson(int courseId, int lessonId)
        => $"/academy/{courseId}/{lessonId}";
}