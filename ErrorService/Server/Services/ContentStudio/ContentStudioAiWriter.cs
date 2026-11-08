using System.Text;
using System.Text.Json;

namespace ErrorService.Server.Services.ContentStudio;

public sealed record GeneratedSection(string? Heading, IReadOnlyList<string> Paragraphs);

public sealed record GeneratedArticle(
    string Title,
    string Summary,
    IReadOnlyList<GeneratedSection> Sections,
    IReadOnlyList<string> KeyPoints,
    IReadOnlyList<string> Tags);

/// <summary>
/// پرامپت و پارس خروجی مدل. خروجی مدل فقط داده ساختاریافته است؛ HTML در سرور و به‌صورت امن ساخته می‌شود.
/// </summary>
public static class ContentStudioAiWriter
{
    public const string SystemPrompt = """
        تو سردبیر و خبرنگار حرفه‌ای فارسی‌زبان سایت «ارورسرویس» هستی؛ تخصص سایت: تعمیرات و عیب‌یابی لوازم خانگی، کدهای خطا، راهنمای خرید و آموزش فناوری.
        وظیفه: از اطلاعات خام یک خبر/مقاله، یک متن کاملاً تازه، دقیق و مستقل به فارسی روان تولید کن.

        قواعد اجباری:
        ۱) بازنویسی کامل: هیچ عبارت بیش از ۸ کلمه را عیناً از متن اصلی کپی نکن. ساختار جمله‌ها، ترتیب مطالب و واژگان را از نو بنویس.
        ۲) فقط از اطلاعات متن اصلی استفاده کن؛ عدد، تاریخ، نام، قیمت، آمار یا ادعای جدید نساز. اطلاعات نامطمئن را حذف کن.
        ۳) لحن خبری/آموزشی رسمی، روشن و بی‌طرف؛ بدون اغراق، بدون تبلیغ و بدون کلیک‌بیت.
        ۴) محتوا باید مستقل و کامل باشد: عنوان دقیق و جذاب، خلاصه ۲ تا ۳ جمله‌ای، ۳ تا ۶ بخش با تیتر، و در پایان جمع‌بندی یا نکات کلیدی.
        ۵) اگر موضوع خارج از حوزه سایت است ولی ارزش خبری/آموزشی عمومی دارد، بازنویسی کن و ارتباطش با حوزه فناوری و لوازم خانگی را در حد اطلاعات موجود روشن کن.
        ۶) هیچ آدرس، لینک، تبلیغ یا نام برند جدیدی که در متن اصلی نیست اضافه نکن.
        ۷) فیلد tags را با ۸ تا ۱۲ تگ و کلیدواژه مرتبط پر کن؛ ترکیبی از موضوع اصلی، نام فناوری/قطعه/برند، نوع محتوا (خبر، آموزش، ترفند) و واژه‌هایی که کاربر واقعاً جستجو می‌کند. هر تگ کوتاه (۲ تا ۴ کلمه) و بدون # باشد.
        ۸) خروجی را فقط و فقط به شکل JSON معتبر فارسی بده؛ بدون مقدمه، بدون توضیح و بدون code fence:
        {"title":"...","summary":"...","tags":["...","..."],"sections":[{"heading":"...","paragraphs":["..."]}],"keyPoints":["..."]}
        """;

    public static string BuildUserPrompt(
        string sourceName,
        string categoryLabel,
        string originalTitle,
        DateTimeOffset? publishedAt,
        string originalContent)
    {
        var text = originalContent.Trim();
        if (text.Length > 6000)
        {
            var cut = text.LastIndexOfAny(new[] { ' ', '\n', '.', '،' }, 5999);
            if (cut < 3000) cut = 6000;
            text = text[..cut].TrimEnd() + "…";
        }

        var sb = new StringBuilder();
        sb.Append("منبع: ").AppendLine(sourceName);
        sb.Append("دسته محتوا: ").AppendLine(categoryLabel);
        sb.Append("تاریخ انتشار: ").AppendLine(publishedAt?.ToString("yyyy-MM-dd") ?? "نامشخص");
        sb.Append("عنوان اصلی: ").AppendLine(originalTitle);
        sb.AppendLine("متن اصلی:");
        sb.AppendLine(text);
        sb.AppendLine();
        sb.AppendLine("اکنون فقط JSON خروجی بده.");
        return sb.ToString();
    }

    public static string BuildStrictRetryPrompt(string originalUserPrompt)
        => originalUserPrompt + "\n\nهشدار: نسخه قبلی بیش از حد به متن اصلی نزدیک بود. این بار با ساختار، جمله‌بندی و واژگان کاملاً متفاوت بازنویسی کن و هیچ عبارت هفت‌کلمه‌ای مشترکی با متن اصلی باقی نگذار.";

    public static GeneratedArticle? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start) return null;

        var json = raw[start..(end + 1)];
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            var root = doc.RootElement;

            var title = GetString(root, "title") ?? GetString(root, "headline");
            if (string.IsNullOrWhiteSpace(title)) return null;

            var summary = GetString(root, "summary") ?? GetString(root, "description") ?? "";

            var sections = new List<GeneratedSection>();
            if (root.TryGetProperty("sections", out var sectionsEl))
            {
                var sectionItems = sectionsEl.ValueKind == JsonValueKind.Array
                    ? sectionsEl.EnumerateArray().ToList()
                    : sectionsEl.ValueKind == JsonValueKind.Object
                        ? new List<JsonElement> { sectionsEl }
                        : new List<JsonElement>();

                foreach (var s in sectionItems)
                {
                    if (s.ValueKind == JsonValueKind.Object)
                    {
                        var heading = GetString(s, "heading") ?? GetString(s, "title");
                        var paragraphs = ReadParagraphs(s, "paragraphs", "text", "content");
                        if (paragraphs.Count == 0) continue;
                        sections.Add(new GeneratedSection(
                            Clean(heading, 120),
                            paragraphs.Select(p => Clean(p, 1200) ?? "").Where(p => p.Length > 0).ToList()));
                    }
                    else if (s.ValueKind == JsonValueKind.String)
                    {
                        var paragraph = Clean(s.GetString(), 1200);
                        if (!string.IsNullOrWhiteSpace(paragraph))
                            sections.Add(new GeneratedSection(null, new[] { paragraph }));
                    }
                }
            }

            if (sections.Count == 0)
            {
                var body = GetString(root, "content");
                if (!string.IsNullOrWhiteSpace(body))
                {
                    var paragraphs = body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(p => Clean(p, 1200))
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Cast<string>()
                        .ToList();
                    if (paragraphs.Count > 0)
                        sections.Add(new GeneratedSection(null, paragraphs));
                }
            }

            if (sections.Count == 0) return null;

            var tags = ReadKeywordList(root, "tags")
                .Select(t => Clean(t, 40))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList();

            var keyPoints = ReadKeywordList(root, "keyPoints")
                .Select(t => Clean(t, 220))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Cast<string>()
                .Take(6)
                .ToList();

            return new GeneratedArticle(
                Clean(title, 140) ?? "",
                Clean(summary, 400) ?? "",
                sections,
                keyPoints,
                tags);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static List<string> ReadStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return new List<string>();

        var list = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
            }
        }
        return list;
    }

    private static List<string> ReadParagraphs(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            var list = ReadStringArray(element, name);
            if (list.Count > 0) return list;

            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(p => p.Length > 0)
                        .ToList();
                }
            }
        }
        return new List<string>();
    }

    private static List<string> ReadKeywordList(JsonElement element, string name)
    {
        var list = ReadStringArray(element, name);
        if (list.Count > 0) return list;

        if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text
                    .Split(new[] { '\n', ',', '،' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(t => t.Length > 0)
                    .ToList();
            }
        }
        return list;
    }

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = value.Replace("<", "‹").Replace(">", "›").Trim();
        if (s.Length > max) s = s[..max].TrimEnd();
        return s;
    }
}
