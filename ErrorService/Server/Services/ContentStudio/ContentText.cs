using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ErrorService.Shared;

namespace ErrorService.Server.Services.ContentStudio;

/// <summary>
/// ابزارهای متن: نرمال‌سازی فارسی، سنجش تشابه (ضدتکرار/ضدکپی)، پاک‌سازی HTML و ساخت خروجی امن.
/// </summary>
public static class ContentText
{
    private static readonly Regex ScriptStyleRegex = new(
        @"<\s*(script|style|noscript)\b[^>]*>.*?<\s*/\s*\1\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex BlockBreakRegex = new(
        @"<\s*(br|/p|/div|/li|/h[1-6]|/tr|/blockquote|/section)\b[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TagRegex = new(@"<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex SpacesRegex = new(@"[ \t\u00A0]+", RegexOptions.Compiled);
    private static readonly Regex NewLinesRegex = new(@"\n{3,}", RegexOptions.Compiled);
    private static readonly Regex NonWordRegex = new(@"[^\p{L}\p{Nd}\s]", RegexOptions.Compiled);

    // ===== کلیدواژه‌های فارسی =====

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "و", "یا", "اما", "ولی", "که", "این", "آن", "اینکه", "از", "به", "با", "بی", "برای", "در", "را",
        "رو", "هم", "نیز", "تا", "بر", "است", "هست", "بود", "شد", "شده", "می", "نمی", "می‌شود", "میشود",
        "می‌کند", "میکند", "می‌توان", "میتوان", "می‌تواند", "میتواند", "می‌گویند", "میگویند", "می‌گوید", "میگوید",
        "کن", "کرد", "کند", "کنند", "کرده", "دارد", "دارند", "داشت", "داشته", "خود",
        "خودش", "ما", "شما", "او", "آن‌ها", "آنها", "من", "تو", "هر", "همه", "بعضی", "چند", "چه", "چرا",
        "چگونه", "کجا", "کی", "اگر", "چون", "زیرا", "پس", "باز", "دیگر", "بسیار", "خیلی", "کم", "بیش",
        "بیشتر", "کمتر", "حدود", "تقریبا", "مثل", "مانند", "طبق", "مورد", "درباره", "بین", "بدون", "روی",
        "داخل", "خارج", "قبل", "بعد", "اول", "آخر", "هنگام", "زمان", "وقت", "سال", "روز", "ماه", "هفته",
        "مدت", "بار", "یکی", "دوتا", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه", "ده", "صد", "هزار",
        "میلیون", "میلیارد", "درصد", "تومان", "ریال", "بودن", "شدن", "کردن", "داشتن", "گفت", "گفته",
        "می‌گوید", "گوید", "عنوان", "مطابق", "براساس", "بر اساس", "جهت", "طی", "ضمن", "فقط", "نیست",
        "هیچ", "چنین", "همچنین", "لذا", "بنابراین", "درحالی", "در حالی", "حال", "وجود", "شامل", "مانند",
        "استفاده", "انجام", "توجه", "بررسی", "نشان", "ممکن", "لازم", "ضروری", "اصلی", "جدید", "جدیدی",
        "بزرگ", "کوچک", "خوب", "بد", "بهتر", "بهترین", "اولین", "دومین", "سومین", "اخیر", "اخیرا",
        "کنید", "کنیم", "داریم", "دارید", "دهد", "دهند", "شود", "شوند", "گردد", "گردند", "باشد",
        "باشند", "هستند", "نیستند", "داده", "داد", "شاید", "هرگز", "همیشه", "معمولا", "حتما",
        "the", "and", "for", "with", "from", "that", "this", "are", "was", "were", "have", "has", "not",
        "you", "your", "but", "can", "will", "all", "our", "their", "they", "its", "into", "than", "then"
    };

    private static readonly HashSet<string> ShortKeywords = new(StringComparer.Ordinal)
    {
        "اب", "گاز", "برق", "فن", "یخ", "پیچ"
    };

    private static bool IsKeywordToken(string token)
    {
        if (token.Length < 3 && !ShortKeywords.Contains(token)) return false;
        if (StopWords.Contains(token)) return false;
        if (token.All(char.IsDigit)) return false;
        return true;
    }

    /// <summary>
    /// استخراج کلیدواژه از عنوان و متن (بدون AI). عنوان وزن بیشتری دارد و عبارت‌های دوقسمتی هم بررسی می‌شوند.
    /// </summary>
    public static List<string> ExtractKeywords(string? title, string? body, int max = 8)
    {
        if (max <= 0) return new List<string>();

        var titleNorm = NormalizeForCompare(title);
        var bodyNorm = NormalizeForCompare(body);
        if (titleNorm.Length == 0 && bodyNorm.Length == 0) return new List<string>();

        var scores = new Dictionary<string, int>(StringComparer.Ordinal);
        var bodyCount = new Dictionary<string, int>(StringComparer.Ordinal);
        var inTitle = new HashSet<string>(StringComparer.Ordinal);

        void Count(string text, int weight, bool isTitle)
        {
            var tokens = Tokenize(text);
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                if (IsKeywordToken(token))
                {
                    scores[token] = scores.GetValueOrDefault(token) + weight;
                    if (isTitle) inTitle.Add(token);
                    else bodyCount[token] = bodyCount.GetValueOrDefault(token) + 1;
                }

                if (i + 1 >= tokens.Length) continue;
                var next = tokens[i + 1];
                if (token.Length < 2 || next.Length < 2) continue;
                if (StopWords.Contains(token) || StopWords.Contains(next)) continue;
                if (token.All(char.IsDigit) || next.All(char.IsDigit)) continue;

                var pair = token + " " + next;
                scores[pair] = scores.GetValueOrDefault(pair) + weight;
                if (isTitle) inTitle.Add(pair);
                else bodyCount[pair] = bodyCount.GetValueOrDefault(pair) + 1;
            }
        }

        if (titleNorm.Length > 0) Count(titleNorm, 4, true);
        if (bodyNorm.Length > 0) Count(bodyNorm, 1, false);

        var chosen = new List<string>();
        foreach (var (term, score) in scores
                     .Where(kv => kv.Key.Contains(' ')
                         ? bodyCount.GetValueOrDefault(kv.Key) >= 2
                         : bodyCount.GetValueOrDefault(kv.Key) >= 2
                           || (bodyCount.GetValueOrDefault(kv.Key) >= 1 && inTitle.Contains(kv.Key)))
                     .OrderByDescending(kv => kv.Value)
                     .ThenByDescending(kv => kv.Key.Length)
                     .ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (chosen.Count >= max) break;
            var overlaps = chosen.Any(c =>
                string.Equals(c, term, StringComparison.Ordinal) ||
                (c.Contains(' ') && c.Contains(term, StringComparison.Ordinal)) ||
                (term.Contains(' ') && term.Contains(c, StringComparison.Ordinal)));
            if (overlaps) continue;
            chosen.Add(term);
        }

        return chosen;
    }

    private static readonly HashSet<string> TrackingQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
        "fbclid", "gclid", "yclid", "msclkid", "mc_cid", "mc_eid", "igshid", "ref", "ref_src"
    };

    // ===== نرمال‌سازی =====

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '\u064A': sb.Append('\u06CC'); break; // ي → ی
                case '\u0643': sb.Append('\u06A9'); break; // ك → ک
                case '\u0622': sb.Append('\u0627'); break; // آ → ا
                case '\u0623': sb.Append('\u0627'); break;
                case '\u0625': sb.Append('\u0627'); break;
                case '\u0629': sb.Append('\u0647'); break; // ة → ه
                case '\u0624': sb.Append('\u0648'); break; // ؤ → و
                case '\u0626': sb.Append('\u06CC'); break; // ئ → ی
                case '\u200C':
                case '\u200D':
                case '\u200E':
                case '\u200F':
                case '\uFEFF':
                    break; // نیم‌فاصله و نویسه‌های کنترلی
                default:
                    if (c is >= '\u064B' and <= '\u0652') break; // اعراب
                    if (c is >= '\u06F0' and <= '\u06F9') sb.Append((char)('0' + (c - '\u06F0'))); // ۰-۹
                    else if (c is >= '\u0660' and <= '\u0669') sb.Append((char)('0' + (c - '\u0660'))); // ٠-٩
                    else sb.Append(c);
                    break;
            }
        }

        var normalized = SpacesRegex.Replace(sb.ToString(), " ").Trim();
        return normalized.ToLowerInvariant();
    }

    /// <summary>فقط حروف/اعداد و فاصله برای مقایسه عنوان.</summary>
    public static string NormalizeForCompare(string? text)
    {
        var n = Normalize(text);
        n = NonWordRegex.Replace(n, " ");
        return SpacesRegex.Replace(n, " ").Trim();
    }

    public static string[] Tokenize(string? normalizedText)
        => string.IsNullOrWhiteSpace(normalizedText)
            ? Array.Empty<string>()
            : normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // ===== تشابه =====

    public static HashSet<string> Shingles(string? text, int size = 3)
    {
        var tokens = Tokenize(NormalizeForCompare(text));
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (tokens.Length == 0) return set;
        if (tokens.Length <= size)
        {
            set.Add(string.Join(' ', tokens));
            return set;
        }

        for (var i = 0; i + size <= tokens.Length; i++)
            set.Add(string.Join(' ', tokens, i, size));
        return set;
    }

    /// <summary>
    /// درصدی از متن تولیدشده که عیناً در متن مبنا هم وجود دارد (۰ تا ۱۰۰).
    /// برای تشخیص کپی‌شدن جمله‌ها استفاده می‌شود.
    /// </summary>
    public static double CopyOverlapPercent(string? generated, string? source)
    {
        var gen = Shingles(generated);
        if (gen.Count == 0) return 0;
        var src = Shingles(source);
        if (src.Count == 0) return 0;

        var common = gen.Count(src.Contains);
        return Math.Round(common * 100.0 / gen.Count, 1);
    }

    public static double Similarity(string? a, string? b, int shingleSize = 3)
    {
        var sa = Shingles(a, shingleSize);
        var sb = Shingles(b, shingleSize);
        if (sa.Count == 0 || sb.Count == 0) return 0;
        var inter = sa.Count(sb.Contains);
        var union = sa.Count + sb.Count - inter;
        return union == 0 ? 0 : Math.Round((double)inter / union, 3);
    }

    public static double TitleSimilarity(string? a, string? b)
    {
        var x = NormalizeForCompare(a);
        var y = NormalizeForCompare(b);
        if (x.Length == 0 || y.Length == 0) return 0;
        if (x == y) return 1;
        var distance = Levenshtein(x, y);
        return Math.Round(1 - (double)distance / Math.Max(x.Length, y.Length), 3);
    }

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    // ===== پاک‌سازی متن/HTML منبع =====

    public static string HtmlToText(string? html, int maxLength = 12000)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";

        var s = ScriptStyleRegex.Replace(html, " ");
        s = BlockBreakRegex.Replace(s, "\n");
        s = TagRegex.Replace(s, " ");
        s = WebUtility.HtmlDecode(s);
        s = s.Replace('\u00A0', ' ');
        s = NewLinesRegex.Replace(s, "\n\n");

        var lines = s.Split('\n')
            .Select(l => SpacesRegex.Replace(l, " ").Trim())
            .Where(l => l.Length > 0);
        s = string.Join("\n", lines).Trim();

        if (s.Length > maxLength)
        {
            var cut = s.LastIndexOf(' ', Math.Min(maxLength, s.Length - 1));
            if (cut < maxLength / 2) cut = maxLength;
            s = s[..cut].TrimEnd() + "…";
        }

        return s;
    }

    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? "").Replace("\r", "").Replace("\n", " ");

    public static string CleanTitle(string? raw, int maxLength = 250)
    {
        var s = SpacesRegex.Replace(HtmlToText(raw, 1000).Replace("\n", " "), " ").Trim();
        return s.Length <= maxLength ? s : s[..maxLength].TrimEnd();
    }

    /// <summary>
    /// ساخت HTML امن برای خبر از ساختار JSON مدل. هیچ HTML خام از AI وارد نمی‌شود.
    /// </summary>
    public static string BuildNewsHtml(
        IEnumerable<(string? Heading, IReadOnlyList<string> Paragraphs)> sections,
        IReadOnlyList<string> keyPoints,
        string? sourceName,
        string? sourceUrl,
        bool includeCredit,
        bool imageCredit = false)
    {
        var sb = new StringBuilder();
        foreach (var (heading, paragraphs) in sections)
        {
            if (!string.IsNullOrWhiteSpace(heading))
                sb.Append("<h2>").Append(Encode(heading.Trim())).Append("</h2>");

            foreach (var p in paragraphs)
            {
                var clean = SpacesRegex.Replace(p.Replace("\n", " "), " ").Trim();
                if (clean.Length == 0) continue;
                sb.Append("<p>").Append(Encode(clean)).Append("</p>");
            }
        }

        var points = keyPoints
            .Select(p => SpacesRegex.Replace(p.Replace("\n", " "), " ").Trim())
            .Where(p => p.Length is > 4 and < 300)
            .Take(6)
            .ToList();
        if (points.Count > 0)
        {
            sb.Append("<h2>نکات کلیدی</h2><ul>");
            foreach (var point in points)
                sb.Append("<li>").Append(Encode(point)).Append("</li>");
            sb.Append("</ul>");
        }

        if (includeCredit && !string.IsNullOrWhiteSpace(sourceName))
        {
            sb.Append("<p style=\"font-size:13px;color:#64748b;border-top:1px solid #e2e8f0;padding-top:10px;margin-top:18px\">")
              .Append(imageCredit ? "منبع متن و تصویر: " : "منبع: ");
            if (IsSafeHttpUrl(sourceUrl))
            {
                sb.Append("<a href=\"").Append(Encode(sourceUrl))
                  .Append("\" target=\"_blank\" rel=\"nofollow noopener noreferrer\">")
                  .Append(Encode(sourceName.Trim())).Append("</a>");
            }
            else
            {
                sb.Append(Encode(sourceName.Trim()));
            }
            sb.Append("</p>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// ساخت HTML امن از متن ساده ادمین. «## عنوان» به تیتر، «- مورد» به فهرست، «> متن» به نقل‌قول،
    /// **درشت**، *مورب*، [متن](آدرس) و آدرس خام به لینک تبدیل می‌شوند.
    /// هیچ HTML خامی از ورودی پذیرفته نمی‌شود.
    /// </summary>
    public static string BuildManualHtml(
        string? plainText,
        bool includeCredit = false,
        string? sourceName = null,
        string? sourceUrl = null)
    {
        var sb = new StringBuilder(ManualMarkup.ToHtml(plainText));

        if (includeCredit && !string.IsNullOrWhiteSpace(sourceName))
        {
            sb.Append("<p style=\"font-size:13px;color:#64748b;border-top:1px solid #e2e8f0;padding-top:10px;margin-top:18px\">")
              .Append("منبع: ");
            if (IsSafeHttpUrl(sourceUrl))
            {
                sb.Append("<a href=\"").Append(Encode(sourceUrl))
                  .Append("\" target=\"_blank\" rel=\"nofollow noopener noreferrer\">")
                  .Append(Encode(sourceName.Trim())).Append("</a>");
            }
            else
            {
                sb.Append(Encode(sourceName.Trim()));
            }
            sb.Append("</p>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// تبدیل HTML تولیدشده به متن قابل ویرایش با نشانه‌ها: تیتر ## / ###، فهرست -، نقل‌قول >، خط ---،
    /// درشت **...**، مورب *...* و لینک [متن](آدرس) تا قالب‌بندی هنگام ویرایش از دست نرود.
    /// </summary>
    public static string ToEditableText(string? html) => ManualMarkup.FromHtml(html);

    // ===== URL =====

    public static bool IsSafeHttpUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

        var host = uri.Host.ToLowerInvariant();
        if (host is "localhost" || host.EndsWith(".local") || host.EndsWith(".internal") || host.EndsWith(".localhost"))
            return false;

        if (!IPAddress.TryParse(host.Trim('[', ']'), out var ip)) return true;

        if (IPAddress.IsLoopback(ip)) return false;
        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            if (bytes[0] == 10) return false;
            if (bytes[0] == 127) return false;
            if (bytes[0] == 0) return false;
            if (bytes[0] == 169 && bytes[1] == 254) return false;
            if (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) return false;
            if (bytes[0] == 192 && bytes[1] == 168) return false;
            if (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) return false;
        }
        else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal) return false;
            if (bytes.Length == 16 && bytes[0] == 0xfc) return false;
        }

        return true;
    }

    public static string? CanonicalizeUrl(string? raw, Uri? baseUri = null)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        Uri? uri = null;
        if (!Uri.TryCreate(raw.Trim(), UriKind.Absolute, out uri))
        {
            if (baseUri is null || !Uri.TryCreate(baseUri, raw.Trim(), out uri)) return null;
        }

        if (uri is null) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        if (!IsSafeHttpUrl(uri.AbsoluteUri)) return null;

        var builder = new UriBuilder(uri) { Fragment = "" };
        builder.Host = builder.Host.ToLowerInvariant();
        if ((builder.Scheme == Uri.UriSchemeHttp && builder.Port == 80) ||
            (builder.Scheme == Uri.UriSchemeHttps && builder.Port == 443))
            builder.Port = -1;

        var pairs = new List<(string Key, string Value)>();
        if (!string.IsNullOrEmpty(builder.Query))
        {
            var query = builder.Query.TrimStart('?');
            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = part.IndexOf('=');
                var key = idx >= 0 ? part[..idx] : part;
                var value = idx >= 0 ? part[(idx + 1)..] : "";
                key = Uri.UnescapeDataString(key.Replace("+", " "));
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (TrackingQueryKeys.Contains(key)) continue;
                pairs.Add((key, value));
            }
            if (pairs.Count == 0)
            {
                var keepAll = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(part =>
                    {
                        var idx = part.IndexOf('=');
                        return idx >= 0 ? part[..idx] : part;
                    });
                if (keepAll.Any(k => TrackingQueryKeys.Contains(Uri.UnescapeDataString(k))))
                    pairs.Clear();
            }
        }

        var queryString = pairs.Count == 0
            ? ""
            : "?" + string.Join("&", pairs
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{Uri.EscapeDataString(p.Key)}={p.Value}"));

        var path = builder.Path;
        if (string.IsNullOrEmpty(path) || path == "/") path = "";
        else path = path.TrimEnd('/');

        return $"{builder.Scheme}://{builder.Host}{(builder.Port > 0 ? ":" + builder.Port : "")}{path}{queryString}";
    }

    public static string HashUrl(string canonicalUrl)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalUrl));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
