using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ErrorService.Shared;

/// <summary>
/// تبدیل متن ساده به HTML امن برای پیش‌نویس دستی استودیو محتوا.
/// «## تیتر»، «### زیرتیتر»، «- فهرست»، «1. فهرست شماره‌دار»، «> نکته/نقل‌قول»، «---» خط جداکننده،
/// **درشت**، *مورب*، [متن](آدرس)، ![توضیح](آدرس عکس) و آدرس‌های خام http(s).
/// ورودی همیشه encode می‌شود؛ HTML خام پذیرفته نمی‌شود.
/// </summary>
public static class ManualMarkup
{
    private static readonly Regex SpacesRegex = new(@"[ \t\u00A0]+", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkRegex = new(
        @"\[([^\]\n]{1,200})\]\((https?://[^\s)]{1,800})\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BareUrlRegex = new(
        @"(^|[\s(])(https?://[^\s<>""]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BoldRegex = new(@"(?<!\*)\*\*(?=\S)(.+?)(?<=\S)\*\*(?!\*)", RegexOptions.Compiled);
    private static readonly Regex ItalicRegex = new(@"(?<!\*)\*(?=\S)([^*\n]+?)(?<=\S)\*(?!\*)", RegexOptions.Compiled);
    private static readonly Regex PlaceholderRegex = new("\u0002(\\d+)\u0002", RegexOptions.Compiled);
    private static readonly Regex ScriptStyleRegex = new(
        @"<\s*(script|style|noscript|template|svg)\b[^>]*>.*?<\s*/\s*\1\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex CommentRegex = new(@"<!--.*?-->", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex TagRegex = new(@"<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex LinkTagRegex = new(
        @"<a\b[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*>(.*?)</a>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex ImageLineRegex = new(
        @"^!\[([^\]\n]{0,200})\]\((https?://[^\s)]{1,800})\)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex OrderedLineRegex = new(
        @"^[0-9۰-۹]{1,3}[.)]\s+(.*)$",
        RegexOptions.Compiled);
    private static readonly Regex ImageTagRegex = new(
        @"<img\b[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SrcAttrRegex = new(
        @"src\s*=\s*[""']([^""']+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AltAttrRegex = new(
        @"alt\s*=\s*[""']([^""']*)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FigcaptionRegex = new(
        @"<figcaption\b[^>]*>.*?</figcaption>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex OrderedBlockRegex = new(
        @"<ol\b[^>]*>(.*?)</ol>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public static string ToHtml(string? plainText)
    {
        var text = (plainText ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        var html = new StringBuilder();
        var list = new List<string>();
        var ordered = new List<string>();
        var quote = new List<string>();

        void FlushList()
        {
            if (list.Count == 0) return;
            html.Append("<ul class=\"md-list\">");
            foreach (var item in list)
                html.Append("<li>").Append(Inline(item)).Append("</li>");
            html.Append("</ul>");
            list.Clear();
        }

        void FlushOrdered()
        {
            if (ordered.Count == 0) return;
            html.Append("<ol class=\"md-list md-list--ordered\">");
            foreach (var item in ordered)
                html.Append("<li>").Append(Inline(item)).Append("</li>");
            html.Append("</ol>");
            ordered.Clear();
        }

        void FlushQuote()
        {
            if (quote.Count == 0) return;
            html.Append("<blockquote class=\"md-quote\"><p>")
                .Append(string.Join("<br />", quote.Select(Inline)))
                .Append("</p></blockquote>");
            quote.Clear();
        }

        void FlushBlocks()
        {
            FlushList();
            FlushOrdered();
            FlushQuote();
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = SpacesRegex.Replace(rawLine, " ").Trim();
            if (line.Length == 0)
            {
                FlushBlocks();
                continue;
            }

            if (line is "---" or "***" or "___")
            {
                FlushBlocks();
                html.Append("<hr class=\"md-divider\" />");
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                FlushBlocks();
                html.Append("<h3>").Append(Inline(line[4..].Trim())).Append("</h3>");
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushBlocks();
                html.Append("<h2>").Append(Inline(line[3..].Trim())).Append("</h2>");
                continue;
            }

            if (line.StartsWith("![", StringComparison.Ordinal))
            {
                var img = ImageLineRegex.Match(line);
                if (img.Success && IsHttpUrl(img.Groups[2].Value))
                {
                    FlushBlocks();
                    var alt = img.Groups[1].Value.Trim();
                    var src = img.Groups[2].Value;
                    html.Append("<figure class=\"md-figure\"><img src=\"").Append(WebUtility.HtmlEncode(src))
                        .Append("\" alt=\"").Append(WebUtility.HtmlEncode(alt)).Append("\" loading=\"lazy\" />");
                    if (alt.Length > 0)
                        html.Append("<figcaption>").Append(WebUtility.HtmlEncode(alt)).Append("</figcaption>");
                    html.Append("</figure>");
                    continue;
                }
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("• ", StringComparison.Ordinal))
            {
                FlushOrdered();
                FlushQuote();
                var item = line[2..].Trim();
                if (item.Length > 0) list.Add(item);
                continue;
            }

            var orderedMatch = OrderedLineRegex.Match(line);
            if (orderedMatch.Success)
            {
                FlushList();
                FlushQuote();
                var item = orderedMatch.Groups[1].Value.Trim();
                if (item.Length > 0) ordered.Add(item);
                continue;
            }

            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                FlushList();
                FlushOrdered();
                quote.Add(line[2..].Trim());
                continue;
            }

            FlushBlocks();
            html.Append("<p>").Append(Inline(line)).Append("</p>");
        }

        FlushBlocks();
        return html.ToString();
    }

    /// <summary>
    /// تبدیل HTML (متن کپی‌شده از سایت/ورد یا محتوای تولیدشده) به متن نشانه‌دار قابل ویرایش:
    /// تیتر ## / ###، فهرست -، نقل‌قول >، خط ---، درشت **...**، مورب *...* و لینک [متن](آدرس).
    /// </summary>
    public static string FromHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";

        var s = ScriptStyleRegex.Replace(html, " ");
        s = CommentRegex.Replace(s, " ");

        s = Regex.Replace(s, @"<blockquote[^>]*>(.*?)</blockquote>", m =>
        {
            var inner = Regex.Replace(m.Groups[1].Value, @"<\s*/?\s*p[^>]*>|<\s*br\s*/?>", "\n", RegexOptions.IgnoreCase);
            var plain = WebUtility.HtmlDecode(TagRegex.Replace(inner, " ")).Replace('\u00A0', ' ');
            var quoted = plain
                .Split('\n')
                .Select(l => SpacesRegex.Replace(l, " ").Trim())
                .Where(l => l.Length > 0)
                .Select(l => "> " + l);
            return "\n" + string.Join("\n", quoted) + "\n\n";
        }, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        s = OrderedBlockRegex.Replace(s, m =>
        {
            var sb = new StringBuilder("\n");
            var number = 0;
            foreach (Match item in Regex.Matches(
                m.Groups[1].Value,
                @"<li\b[^>]*>(.*?)</li>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                var text = WebUtility.HtmlDecode(TagRegex.Replace(item.Groups[1].Value, " "))
                    .Replace('\u00A0', ' ');
                text = SpacesRegex.Replace(text, " ").Trim();
                if (text.Length == 0) continue;
                number++;
                sb.Append(number).Append(". ").Append(text).Append('\n');
            }
            return sb.Append('\n').ToString();
        });

        s = FigcaptionRegex.Replace(s, " ");

        s = ImageTagRegex.Replace(s, m =>
        {
            var tag = m.Value;
            var srcAttr = SrcAttrRegex.Match(tag);
            if (!srcAttr.Success) return " ";
            var src = WebUtility.HtmlDecode(srcAttr.Groups[1].Value).Trim();
            if (!IsHttpUrl(src)) return " ";
            var altAttr = AltAttrRegex.Match(tag);
            var alt = altAttr.Success ? WebUtility.HtmlDecode(altAttr.Groups[1].Value).Trim() : "";
            return "\n![" + alt + "](" + src + ")\n";
        });

        s = LinkTagRegex.Replace(s, m =>
        {
            var href = WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
            var label = WebUtility.HtmlDecode(TagRegex.Replace(m.Groups[2].Value, " ")).Trim();
            if (label.Length == 0) label = href;
            if (label.StartsWith("![", StringComparison.Ordinal)) return label;
            return IsHttpUrl(href) ? $"[{label}]({href})" : label;
        });

        s = Regex.Replace(s, @"<\s*strong[^>]*>|<\s*b\s*>", "**", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*/\s*strong\s*>|<\s*/\s*b\s*>", "**", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*em[^>]*>|<\s*i\s*>", "*", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*/\s*em\s*>|<\s*/\s*i\s*>", "*", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*hr\b[^>]*/?>", "\n---\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*h1[^>]*>|<\s*h2[^>]*>", "\n## ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*h[3-6][^>]*>", "\n### ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*/\s*h[1-6]\s*>", "\n\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*li[^>]*>", "\n- ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*/\s*li\s*>", "", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*/\s*p\s*>|<\s*/\s*ul\s*>|<\s*/\s*ol\s*>|<\s*/\s*div\s*>|<\s*/\s*section\s*>|<\s*/\s*article\s*>|<\s*/\s*header\s*>|<\s*/\s*footer\s*>",
            "\n\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*div\b[^>]*>|<\s*section\b[^>]*>|<\s*article\b[^>]*>|<\s*header\b[^>]*>|<\s*footer\b[^>]*>",
            "\n", RegexOptions.IgnoreCase);
        s = TagRegex.Replace(s, " ");
        s = WebUtility.HtmlDecode(s).Replace('\u00A0', ' ');

        var sb = new StringBuilder();
        var blank = 0;
        foreach (var raw in s.Split('\n'))
        {
            var line = SpacesRegex.Replace(raw, " ").Trim();
            if (line.Length == 0)
            {
                if (++blank > 1) continue;
            }
            else
            {
                blank = 0;
            }
            sb.Append(line).Append('\n');
        }

        return sb.ToString().Trim();
    }

    /// <summary>ساخت HTML از یک تکه متن تک‌خطی (پاراگراف، آیتم فهرست، تیتر)؛ بدون تگ بلوکی.</summary>
    private static string Inline(string raw)
    {
        var s = WebUtility.HtmlEncode(raw);

        var links = new List<string>();
        s = MarkdownLinkRegex.Replace(s, m =>
        {
            var href = m.Groups[2].Value;
            if (!IsHttpUrl(href)) return m.Groups[1].Value;
            links.Add($"<a href=\"{href}\" target=\"_blank\" rel=\"nofollow noopener noreferrer\">{m.Groups[1].Value}</a>");
            return $"\u0002{links.Count - 1}\u0002";
        });

        s = BareUrlRegex.Replace(s, m =>
        {
            var url = m.Groups[2].Value;
            var tail = string.Empty;
            var trimmed = url.TrimEnd('.', ',', ';', ':', '!', '?', '،', '؛', '»');
            if (trimmed.Length < url.Length)
            {
                tail = url[trimmed.Length..];
                url = trimmed;
            }

            if (!IsHttpUrl(url)) return m.Value;

            links.Add($"<a href=\"{url}\" target=\"_blank\" rel=\"nofollow noopener noreferrer\">{url}</a>");
            return m.Groups[1].Value + $"\u0002{links.Count - 1}\u0002" + tail;
        });

        s = BoldRegex.Replace(s, "<strong>$1</strong>");
        s = ItalicRegex.Replace(s, "<em>$1</em>");
        s = PlaceholderRegex.Replace(s, m =>
        {
            var index = int.Parse(m.Groups[1].Value);
            return index < links.Count ? links[index] : m.Value;
        });

        return s;
    }

    private static bool IsHttpUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;
        return value.IndexOfAny(new[] { ' ', '"', '\'', '<', '>' }) < 0;
    }
}
