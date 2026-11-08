using System.Text.RegularExpressions;

namespace ErrorService.Shared;

/// <summary>
/// توضیح متای صفحات پویا؛ خزنده و کلاینت باید دقیقاً یک متن تولید کنند
/// تا گوگل دو کپشن متفاوت برای یک URL نبیند.
/// </summary>
public static class SeoDescriptions
{
    public const int MaxLength = 160;

    public static string Product(string? description, string name, string? categoryName)
    {
        var text = Normalize(description);
        if (text.Length > 0) return Truncate(text);
        return string.IsNullOrWhiteSpace(categoryName)
            ? $"خرید {name} با مشخصات فنی، قیمت به‌روز و ضمانت اصالت از فروشگاه ارورسرویس"
            : $"خرید و مشاهده مشخصات فنی {name} در دسته {categoryName}";
    }

    public static string News(string? summary, string title)
    {
        var text = Normalize(summary);
        return text.Length > 0 ? Truncate(text) : Truncate($"مشاهده جزئیات خبر: {title}");
    }

    public static string Article(string? summary, string title)
    {
        var text = Normalize(summary);
        return text.Length > 0 ? Truncate(text) : Truncate($"مطالعه مقاله آموزشی {title} در آکادمی ارورسرویس");
    }

    public static string Course(string? summary, string title)
    {
        var text = Normalize(summary);
        return text.Length > 0 ? Truncate(text) : Truncate($"شرکت در دوره آموزشی {title} در آکادمی ارورسرویس");
    }

    public static string Lesson(string title, string? courseTitle) =>
        Truncate(string.IsNullOrWhiteSpace(courseTitle)
            ? $"مشاهده {title} در آکادمی ارورسرویس"
            : $"مشاهده قسمت {title} از دوره {courseTitle} در آکادمی ارورسرویس");

    public static string ErrorCode(string code, string brand, string device, string? description, string? solution)
    {
        var head = $"کد خطای {code} {brand} {device}".Trim();
        var desc = Truncate(Normalize(description), 110);
        if (desc.Length == 0) return Truncate($"{head} و راه حل تعمیر");
        var text = $"{head}: {desc}";
        var sol = Truncate(Normalize(solution), 60);
        return Truncate(sol.Length == 0 ? text : $"{text}. راه حل: {sol}");
    }

    public static string Truncate(string? text, int max = MaxLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var value = text.Trim();
        if (value.Length <= max) return value;
        var cut = value[..(max - 3)].TrimEnd();
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > max / 2) cut = cut[..lastSpace];
        return cut + "...";
    }

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var value = Regex.Replace(text, @"<(script|style)[\s\S]*?</\1>", " ", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, "<[^>]+>", " ");
        value = value.Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
                     .Replace("&amp;", "&")
                     .Replace("&quot;", "\"")
                     .Replace("&#39;", "'");
        return Regex.Replace(value, @"\s+", " ").Trim();
    }
}
