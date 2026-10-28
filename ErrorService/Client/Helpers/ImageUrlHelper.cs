namespace ErrorService.Client.Helpers;

/// <summary>
/// کمک‌کننده ساخت URL تصویر بهینه برای لیست‌ها (srcset).
/// فقط تصاویر آپلودشده تبدیل می‌شوند؛ بقیه (placeholder و …) بدون تغییر برمی‌گردند.
/// </summary>
public static class ImageUrlHelper
{
    public static readonly int[] CardWidths = { 200, 400, 800 };

    private static readonly string[] TransformableExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    public static bool CanTransform(string? src)
    {
        if (string.IsNullOrWhiteSpace(src)
            || !src.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            || src.Contains("..", StringComparison.Ordinal))
            return false;

        var path = src.Split('?', '#')[0];
        return TransformableExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    public static string Thumb(string? src, int width) =>
        CanTransform(src) ? $"/img/{width}{src}" : src ?? "";

    public static string SrcSet(string? src) => SrcSet(src, CardWidths);

    public static string SrcSet(string? src, int[] widths)
    {
        if (!CanTransform(src))
            return "";

        var parts = new string[widths.Length];
        for (var i = 0; i < widths.Length; i++)
            parts[i] = $"{Thumb(src, widths[i])} {widths[i]}w";

        return string.Join(", ", parts);
    }
}
