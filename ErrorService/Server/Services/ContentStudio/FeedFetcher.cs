using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ErrorService.Server.Services.ContentStudio;

public sealed record FeedEntry(
    string Title,
    string Link,
    string? ExternalId,
    DateTimeOffset? PublishedAt,
    string? Summary,
    string? Content,
    string? ImageUrl);

public sealed record FeedDocument(string? Title, string? SiteUrl, List<FeedEntry> Entries);

/// <summary>نتیجه بررسی یک آدرس کاندید برای فید (برای نمایش شفاف در پنل ادمین).</summary>
public sealed record FeedProbeResult(
    string Url,
    string Method,
    bool Ok,
    int? HttpStatus,
    string? ContentType,
    string? Title,
    int ItemCount,
    string? LatestTitle,
    DateTimeOffset? LatestPublishedAt,
    string? Error);

public sealed record FeedDiscoveryResult(
    string SiteUrl,
    string? SiteTitle,
    string? SiteError,
    int Probed,
    List<FeedProbeResult> Results)
{
    public List<FeedProbeResult> Feeds => Results.Where(r => r.Ok).ToList();

    public FeedProbeResult? Best => Feeds
        .OrderByDescending(r => MainPathScore(r.Url))
        .ThenByDescending(r => r.ItemCount)
        .ThenByDescending(r => r.LatestPublishedAt ?? DateTimeOffset.MinValue)
        .FirstOrDefault();

    /// <summary>مسیرهای اصلی سایت (مثل /feed) بر فیدهای فرعی (کامنت‌ها و دسته‌ها) اولویت دارند.</summary>
    private static int MainPathScore(string url)
    {
        var path = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.PathAndQuery.ToLowerInvariant() : "";
        if (path.Contains("comment")) return 0;
        if (path.Contains("/category/") || path.Contains("/tag/") || path.Contains("/author/")) return 1;
        if (path is "/feed" or "/feed/" or "/rss" or "/rss/" or "/feed.xml" or "/rss.xml" or "/atom.xml" or "/index.xml") return 3;
        if (path.Contains("feed") || path.Contains("rss") || path.Contains("atom")) return 2;
        return 1;
    }
}

/// <summary>
/// خواندن RSS/Atom بدون وابستگی خارجی + کشف چندمرحله‌ای فید از صفحه سایت.
/// </summary>
public sealed class FeedFetcher
{
    private const int MaxFeedBytes = 3_000_000;
    private const int MaxHtmlBytes = 1_500_000;
    private const int MaxProbeBytes = 2_000_000;
    private const int MaxEntries = 60;
    private const int ProbeTimeoutSeconds = 12;
    private const int DiscoveryBudgetSeconds = 40;

    // برخی سایت‌ها فید را فقط برای مرورگر واقعی برمی‌گردانند؛ UA مرورگری + شناسه ربات.
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36 ErrorServiceContentStudio/1.1 (+https://errorservice.ir)";

    private const string AcceptFeed =
        "application/rss+xml, application/atom+xml, application/rdf+xml, application/xml, text/xml, */*;q=0.8";

    private const string AcceptHtml =
        "text/html, application/xhtml+xml, application/xml;q=0.9, */*;q=0.5";

    private static readonly string[] CommonFeedPaths =
    {
        "/feed", "/feed/", "/rss", "/rss/", "/feed.xml", "/rss.xml", "/atom.xml", "/index.xml",
        "/feed/rss2", "/?feed=rss2", "/feeds/posts/default", "/feed/atom",
        "/blog/feed", "/blog/rss.xml", "/news/feed", "/news/rss", "/news/rss.xml",
        "/fa/feed", "/fa/rss", "/atom"
    };

    private static readonly Regex ImgSrcRegex = new(
        @"<img\b[^>]*\bsrc\s*=\s*(['""])(?<src>.+?)\1",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex LinkTagRegex = new(
        @"<link\b[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AnchorTagRegex = new(
        @"<a\b[^>]*\bhref\s*=\s*(['""])(?<href>.+?)\1[^>]*>(?<text>.*?)<\s*/\s*a\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex AttrRegex = new(
        @"(?<name>[\w:-]+)\s*=\s*(?:[""'](?<value>[^""']*)[""']|(?<value>[^\s>]+))",
        RegexOptions.Compiled);

    private static readonly Regex TitleTagRegex = new(
        @"<\s*title\b[^>]*>(?<t>.*?)<\s*/\s*title\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex MetaCharsetRegex = new(
        @"<(?:meta|meta\s+charset)\b[^>]*?(?:charset|encoding)\s*=\s*[""']?\s*(?<cs>[A-Za-z0-9_\-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex StripBlocksRegex = new(
        @"<\s*(script|style|noscript|svg|iframe|form|nav|header|footer|aside|template)\b[^>]*>.*?<\s*/\s*\1\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex CommentRegex = new(
        @"<!--.*?-->",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ArticleRegionRegex = new(
        @"<\s*article\b[^>]*>(?<body>.*?)<\s*/\s*article\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex BodyRegionRegex = new(
        @"<\s*body\b[^>]*>(?<body>.*?)<\s*/\s*body\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex BlockTextRegex = new(
        @"<\s*(?<tag>h2|p)\b[^>]*>(?<text>.*?)<\s*/\s*\k<tag>\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex InnerTagRegex = new(
        @"<[^>]+>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex InnerSpacesRegex = new(
        @"[ \t\u00A0]+",
        RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FeedFetcher> _logger;

    static FeedFetcher()
    {
        // برای فیدهای قدیمی فارسی با windows-1256
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public FeedFetcher(IHttpClientFactory httpClientFactory, ILogger<FeedFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<FeedDocument> FetchAsync(string feedUrl, CancellationToken ct = default)
    {
        if (!ContentText.IsSafeHttpUrl(feedUrl))
            throw new InvalidOperationException("آدرس فید معتبر نیست (فقط http/https عمومی).");

        var (body, finalUri, status, contentType) = await FetchRawAsync(feedUrl, MaxFeedBytes, AcceptFeed, ct);
        var kind = DetectKind(body);
        if (kind == "html")
        {
            throw new InvalidOperationException(
                $"این آدرس یک صفحه وب (HTML) برمی‌گرداند، نه فید RSS/Atom (HTTP {status}). " +
                "آدرس سایت را در فیلد «آدرس سایت» بگذارید و دکمه «کشف فید» را بزنید.");
        }

        if (kind == "unknown" && string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException($"پاسخ خالی بود (HTTP {status}؛ نوع: {contentType ?? "نامشخص"}).");

        return Parse(body, finalUri);
    }

    /// <summary>
    /// کشف فید از آدرس سایت: ابتدا لینک‌های اعلام‌شده در HTML، سپس لینک‌های متنی،
    /// و اگر چیزی پیدا نشد مسیرهای رایج (feed/rss/atom) همان دامنه بررسی می‌شوند.
    /// همه کاندیدها اعتبارسنجی و با تعداد آیتم گزارش می‌شوند.
    /// </summary>
    public async Task<FeedDiscoveryResult> DiscoverAsync(string siteUrl, CancellationToken ct = default)
    {
        if (!ContentText.IsSafeHttpUrl(siteUrl))
            throw new InvalidOperationException("آدرس سایت معتبر نیست (فقط http/https عمومی).");

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(DiscoveryBudgetSeconds));

        var results = new List<FeedProbeResult>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? siteTitle = null;
        string? siteError = null;
        Uri? siteUri = null;
        string? html = null;

        // 1) خود آدرس ممکن است فید باشد یا صفحه سایت.
        try
        {
            var (body, finalUri, status, contentType) = await FetchRawAsync(siteUrl, MaxHtmlBytes, AcceptHtml, budget.Token);
            siteUri = finalUri;

            if (DetectKind(body) == "feed")
            {
                var direct = BuildFeedResult(body, finalUri, status, contentType, "direct", null);
                results.Add(direct);
                if (direct.Ok)
                    return new FeedDiscoveryResult(siteUrl, direct.Title, null, 1, results);
            }
            else
            {
                html = body;
                siteTitle = ExtractHtmlTitle(body);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            siteError = ShortenError(ex, 300);
        }

        var origin = siteUri is not null
            ? new Uri(siteUri.GetLeftPart(UriPartial.Authority))
            : new Uri(new Uri(siteUrl).GetLeftPart(UriPartial.Authority));

        // 2) لینک‌های اعلام‌شده در HTML (link rel=alternate/feed + anchor متنی).
        var candidates = new List<(string Url, string Method)>();
        if (html is not null)
        {
            foreach (var url in ExtractDeclaredFeedLinks(html, siteUri!))
            {
                if (seen.Add(url)) candidates.Add((url, "link"));
            }

            foreach (var url in ExtractAnchorFeedLinks(html, siteUri!))
            {
                if (seen.Add(url)) candidates.Add((url, "anchor"));
            }
        }

        // 3) اگر در HTML فیدی نیست (یا صفحه اصلاً خوانده نشد)، مسیرهای رایج را امتحان کن.
        var linkResults = await ProbeManyAsync(
            candidates.Take(8).ToList(), maxConcurrency: 4, budget.Token);
        results.AddRange(linkResults);

        // لینک اعلام‌شده ممکن است روی هاست بی‌www باشد که ۴۰۴ می‌دهد؛ همان مسیر را روی هاست سایت هم امتحان کن.
        if (!results.Any(r => r.Ok) && candidates.Count > 0)
        {
            var hostRetries = new List<(string Url, string Method)>();
            foreach (var (url, method) in candidates.Take(8))
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) continue;
                if (string.Equals(uri.Host, origin.Host, StringComparison.OrdinalIgnoreCase)) continue;
                if (!SameSiteHost(uri.Host, origin.Host)) continue;

                var alt = ContentText.CanonicalizeUrl(new Uri(origin, uri.PathAndQuery).AbsoluteUri);
                if (alt is null || !seen.Add(alt)) continue;
                hostRetries.Add((alt, method));
                if (hostRetries.Count >= 4) break;
            }

            if (hostRetries.Count > 0)
                results.AddRange(await ProbeManyAsync(hostRetries, maxConcurrency: 4, budget.Token));
        }

        if (!results.Any(r => r.Ok))
        {
            var probeCandidates = new List<(string Url, string Method)>();
            foreach (var path in CommonFeedPaths)
            {
                var url = ContentText.CanonicalizeUrl(new Uri(origin, path).AbsoluteUri);
                if (url is null || !seen.Add(url)) continue;
                probeCandidates.Add((url, "probe"));
            }

            var probeResults = await ProbeManyAsync(probeCandidates, maxConcurrency: 6, budget.Token);
            results.AddRange(probeResults);
        }

        return new FeedDiscoveryResult(siteUrl, siteTitle, siteError, results.Count, results);
    }

    /// <summary>
    /// دریافت متن اصلی مقاله از صفحه منبع؛ برای فیدهایی که فقط خلاصه می‌دهند.
    /// در صورت شکست یا استخراج خالی، null برمی‌گردد تا فراخوان از داده موجود استفاده کند.
    /// </summary>
    public async Task<string?> FetchArticleTextAsync(string articleUrl, int maxChars = 20000, CancellationToken ct = default)
    {
        if (!ContentText.IsSafeHttpUrl(articleUrl)) return null;

        var (html, _) = await FetchStringAsync(articleUrl, MaxHtmlBytes, AcceptHtml, ct);
        var text = ExtractArticleText(html, maxChars);
        return text.Length >= 120 ? text : null;
    }

    /// <summary>
    /// استخراج متن مقاله از HTML صفحه: حذف منو/فوتر/اسکریپت، انتخاب غنی‌ترین ناحیه article،
    /// و برداشتن تیترهای h2 و پاراگراف‌ها به ترتیب سند (h3 معمولاً تیتر کارت‌های مطالب مرتبط است).
    /// </summary>
    public static string ExtractArticleText(string? html, int maxChars = 20000)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";

        var cleaned = CommentRegex.Replace(html, " ");
        cleaned = StripBlocksRegex.Replace(cleaned, " ");

        var region = PickRichestRegion(cleaned) ?? cleaned;
        var blocks = new List<string>();
        var total = 0;

        foreach (Match block in BlockTextRegex.Matches(region))
        {
            var isHeading = block.Groups["tag"].Value.StartsWith("h", StringComparison.OrdinalIgnoreCase);
            var text = InnerTagRegex.Replace(block.Groups["text"].Value, " ");
            text = WebUtility.HtmlDecode(text).Replace('\u00A0', ' ');
            text = InnerSpacesRegex.Replace(text, " ").Trim();
            if (text.Length == 0) continue;
            if (isHeading ? text.Length < 3 : text.Length < 40) continue;
            if (blocks.Count > 0 && string.Equals(blocks[^1], text, StringComparison.Ordinal)) continue;

            var line = isHeading ? "## " + text : text;
            if (total + line.Length > maxChars)
            {
                var room = maxChars - total;
                if (room > 80) blocks.Add(line[..room].TrimEnd() + "…");
                break;
            }

            blocks.Add(line);
            total += line.Length + 2;
        }

        if (blocks.Count == 0)
        {
            // سایت‌هایی که متن را در div می‌گذارند: برگشت به متن ساده کل ناحیه.
            return ContentText.HtmlToText(region, maxChars);
        }

        return string.Join("\n\n", blocks).Trim();
    }

    // ===== کشف =====

    private async Task<List<FeedProbeResult>> ProbeManyAsync(
        List<(string Url, string Method)> candidates,
        int maxConcurrency,
        CancellationToken ct)
    {
        if (candidates.Count == 0) return new List<FeedProbeResult>();

        var gate = new SemaphoreSlim(Math.Max(1, maxConcurrency));
        var tasks = candidates.Select(async candidate =>
        {
            await gate.WaitAsync(ct);
            try
            {
                return await ProbeAsync(candidate.Url, candidate.Method, ct);
            }
            catch (OperationCanceledException)
            {
                return new FeedProbeResult(candidate.Url, candidate.Method, false, null, null, null, 0, null, null,
                    ct.IsCancellationRequested ? "زمان بررسی تمام شد." : "مهلت بررسی این آدرس تمام شد.");
            }
            finally
            {
                gate.Release();
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private async Task<FeedProbeResult> ProbeAsync(string url, string method, CancellationToken outerCt)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        timeout.CancelAfter(TimeSpan.FromSeconds(ProbeTimeoutSeconds));

        try
        {
            var (body, finalUri, status, contentType) = await FetchRawAsync(url, MaxProbeBytes, AcceptFeed, timeout.Token);
            return BuildFeedResult(body, finalUri, status, contentType, method, null);
        }
        catch (OperationCanceledException) when (!outerCt.IsCancellationRequested)
        {
            return new FeedProbeResult(url, method, false, null, null, null, 0, null, null,
                $"پاسخی در {ProbeTimeoutSeconds} ثانیه دریافت نشد.");
        }
        catch (Exception ex)
        {
            return new FeedProbeResult(url, method, false, null, null, null, 0, null, null, ShortenError(ex, 220));
        }
    }

    private static FeedProbeResult BuildFeedResult(
        string body,
        Uri finalUri,
        int status,
        string? contentType,
        string method,
        string? forcedError)
    {
        if (forcedError is not null)
            return new FeedProbeResult(finalUri.AbsoluteUri, method, false, status, contentType, null, 0, null, null, forcedError);

        var kind = DetectKind(body);
        if (kind != "feed")
        {
            var reason = kind == "html"
                ? "یک صفحه وب است، نه فید."
                : "محتوای XML معتبر فید نیست.";
            return new FeedProbeResult(finalUri.AbsoluteUri, method, false, status, contentType, null, 0, null, null, reason);
        }

        try
        {
            var doc = Parse(body, finalUri);
            var latest = doc.Entries.OrderByDescending(e => e.PublishedAt ?? DateTimeOffset.MinValue).FirstOrDefault();
            return new FeedProbeResult(
                finalUri.AbsoluteUri,
                method,
                true,
                status,
                contentType,
                doc.Title,
                doc.Entries.Count,
                latest is null ? null : ContentText.CleanTitle(latest.Title, 160),
                latest?.PublishedAt,
                null);
        }
        catch (Exception ex)
        {
            return new FeedProbeResult(finalUri.AbsoluteUri, method, false, status, contentType, null, 0, null, null,
                "فید قابل خواندن نیست: " + ShortenError(ex, 200));
        }
    }

    private static IEnumerable<string> ExtractDeclaredFeedLinks(string html, Uri baseUri)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match tag in LinkTagRegex.Matches(html))
        {
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match attr in AttrRegex.Matches(tag.Value))
            {
                var name = attr.Groups["name"].Value;
                if (!attrs.ContainsKey(name)) attrs[name] = attr.Groups["value"].Value;
            }

            if (!attrs.TryGetValue("href", out var href)) continue;
            attrs.TryGetValue("rel", out var rel);
            attrs.TryGetValue("type", out var type);

            var rels = (rel ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var isAlternate = rels.Any(r => r.Equals("alternate", StringComparison.OrdinalIgnoreCase));
            var isFeedRel = rels.Any(r => r.Equals("feed", StringComparison.OrdinalIgnoreCase));
            var isFeedType = type is not null &&
                (type.Contains("rss", StringComparison.OrdinalIgnoreCase)
                 || type.Contains("atom", StringComparison.OrdinalIgnoreCase)
                 || type.Contains("rdf", StringComparison.OrdinalIgnoreCase)
                 || type.Contains("xml", StringComparison.OrdinalIgnoreCase));

            if (!(isFeedRel || (isAlternate && isFeedType))) continue;

            var canonical = ContentText.CanonicalizeUrl(href, baseUri);
            if (canonical is null || !seen.Add(canonical)) continue;
            found.Add(canonical);
            if (found.Count >= 8) break;
        }

        return found;
    }

    private static IEnumerable<string> ExtractAnchorFeedLinks(string html, Uri baseUri)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in AnchorTagRegex.Matches(html))
        {
            var href = match.Groups["href"].Value;
            var text = InnerTagRegex.Replace(match.Groups["text"].Value, " ").Trim();

            var pathish = href.Contains("/feed", StringComparison.OrdinalIgnoreCase)
                          || href.Contains("/rss", StringComparison.OrdinalIgnoreCase)
                          || href.Contains("/atom", StringComparison.OrdinalIgnoreCase)
                          || href.Contains("feed=", StringComparison.OrdinalIgnoreCase)
                          || href.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
            var textish = text.Contains("rss", StringComparison.OrdinalIgnoreCase)
                          || text.Contains("atom", StringComparison.OrdinalIgnoreCase)
                          || text.Contains("فید", StringComparison.Ordinal)
                          || text.Contains("خوراک", StringComparison.Ordinal);
            if (!pathish && !textish) continue;

            var canonical = ContentText.CanonicalizeUrl(href, baseUri);
            if (canonical is null || !seen.Add(canonical)) continue;
            if (!IsSameHost(canonical, baseUri)) continue;

            // فقط متن لینک نشانه فید است: لینک‌های ناوبری کم‌عمق را بپذیر تا لینک مقاله (که ممکن است کلمه rss در تیترش باشد) رد شود.
            if (!pathish && LinkPathDepth(canonical) > 2) continue;

            found.Add(canonical);
            if (found.Count >= 5) break;
        }

        return found;
    }

    private static bool IsSameHost(string url, Uri baseUri)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase);

    private static bool SameSiteHost(string hostA, string hostB)
        => string.Equals(TrimWww(hostA), TrimWww(hostB), StringComparison.OrdinalIgnoreCase);

    private static string TrimWww(string host)
        => host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;

    private static int LinkPathDepth(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length
            : 99;

    public static string? ExtractHtmlTitle(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var match = TitleTagRegex.Match(html);
        if (!match.Success) return null;
        var title = ContentText.CleanTitle(WebUtility.HtmlDecode(match.Groups["t"].Value), 160);
        return string.IsNullOrWhiteSpace(title) ? null : title;
    }

    /// <summary>تشخیص نوع پاسخ: فید | html | نامشخص.</summary>
    public static string DetectKind(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "empty";
        var head = body.Length > 1200 ? body[..1200] : body;
        head = head.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (head.Length == 0) return "empty";
        if (head[0] != '<') return "text";

        var lower = head.ToLowerInvariant();
        if (lower.Contains("<rss") || lower.Contains("<feed") || lower.Contains("<rdf") || lower.Contains("<rdf:rdf"))
            return "feed";
        if (lower.StartsWith("<!doctype html", StringComparison.Ordinal) || lower.Contains("<html"))
            return "html";
        return "xml";
    }

    // ===== دریافت =====

    private async Task<(string Body, Uri FinalUri)> FetchStringAsync(string url, int maxBytes, string accept, CancellationToken ct)
    {
        var (body, finalUri, _, _) = await FetchRawAsync(url, maxBytes, accept, ct);
        return (body, finalUri);
    }

    private async Task<(string Body, Uri FinalUri, int Status, string? ContentType)> FetchRawAsync(
        string url, int maxBytes, string accept, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("ContentStudioFeed");
        var current = new Uri(url);
        var transientRetries = 0;

        for (var hop = 0; hop <= 4; hop++)
        {
            if (!ContentText.IsSafeHttpUrl(current.AbsoluteUri))
                throw new InvalidOperationException("آدرس مقصد مجاز نیست.");

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
            request.Headers.TryAddWithoutValidation("Accept", accept);
            request.Headers.TryAddWithoutValidation("Accept-Language", "fa-IR,fa;q=0.9,en;q=0.7");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if ((int)response.StatusCode is >= 300 and <= 399 && response.Headers.Location is not null)
            {
                current = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(current, response.Headers.Location);
                continue;
            }

            var status = (int)response.StatusCode;
            // سایت‌های پشت محافظ: یک بار تلاش مجدد کوتاه برای خطاهای گذرا.
            if ((status is 408 or 429 or 500 or 502 or 503 or 504) && transientRetries++ < 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(700), ct);
                hop--;
                continue;
            }

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"دریافت پاسخ ناموفق بود (HTTP {status}).");

            var declared = response.Content.Headers.ContentLength;
            if (declared is > 0 && declared > maxBytes)
                throw new InvalidOperationException("حجم پاسخ بیش از حد مجاز است.");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var ms = new MemoryStream();
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                if (ms.Length + read > maxBytes)
                    throw new InvalidOperationException("حجم پاسخ بیش از حد مجاز است.");
                ms.Write(buffer, 0, read);
            }

            var bytes = ms.ToArray();
            var encoding = DetectEncoding(bytes, response.Content.Headers.ContentType?.CharSet);
            return (encoding.GetString(bytes), current, status, response.Content.Headers.ContentType?.MediaType);
        }

        throw new InvalidOperationException("تعداد ریدایرکت‌ها بیش از حد مجاز است.");
    }

    private static Encoding DetectEncoding(byte[] bytes, string? charset)
    {
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try { return Encoding.GetEncoding(charset.Trim('"', ' ')); }
            catch { /* fall through */ }
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(false);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode;
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return Encoding.BigEndianUnicode;

        // بعضی فیدهای قدیمی charset را فقط در XML declaration یا meta اعلام می‌کنند.
        var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 2048));
        var declared = MetaCharsetRegex.Match(head);
        if (declared.Success)
        {
            try { return Encoding.GetEncoding(declared.Groups["cs"].Value); }
            catch { /* fall through */ }
        }

        try
        {
            _ = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            return Encoding.UTF8;
        }
        catch (DecoderFallbackException)
        {
            try { return Encoding.GetEncoding("windows-1256"); }
            catch { return Encoding.UTF8; }
        }
    }

    private static string? PickRichestRegion(string html)
    {
        var candidates = new List<string>();
        foreach (Match match in ArticleRegionRegex.Matches(html))
            candidates.Add(match.Groups["body"].Value);

        if (candidates.Count == 0)
        {
            var body = BodyRegionRegex.Match(html);
            if (body.Success) candidates.Add(body.Groups["body"].Value);
        }

        if (candidates.Count == 0) return null;

        return candidates
            .OrderByDescending(c => InnerTagRegex.Replace(c, " ").Length)
            .FirstOrDefault();
    }

    public static FeedDocument Parse(string xml, Uri feedUri)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("محتوای فید XML معتبر نیست.", ex);
        }

        var root = doc.Root ?? throw new InvalidOperationException("فید خالی است.");
        var isAtom = root.Name.LocalName.Equals("feed", StringComparison.OrdinalIgnoreCase);

        string? feedTitle = null;
        string? siteUrl = null;

        if (isAtom)
        {
            feedTitle = ContentText.CleanTitle(Value(root, "title"));
            siteUrl = root.Elements().FirstOrDefault(e => e.Name.LocalName == "link" && IsAlternate(e))
                ?.Attribute("href")?.Value;
        }
        else
        {
            var channel = Descendants(root, "channel").FirstOrDefault();
            feedTitle = ContentText.CleanTitle(Value(channel ?? root, "title"));
            siteUrl = Value(channel ?? root, "link");
        }

        var nodes = isAtom
            ? root.Elements().Where(e => e.Name.LocalName == "entry").ToList()
            : Descendants(root, "item").ToList();

        var entries = new List<FeedEntry>();
        var seenLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            try
            {
                var title = ContentText.CleanTitle(Value(node, "title"), 400);
                if (string.IsNullOrWhiteSpace(title)) continue;

                var linkRaw = isAtom
                    ? (node.Elements().FirstOrDefault(e => e.Name.LocalName == "link" && IsAlternate(e))
                        ?? node.Elements().FirstOrDefault(e => e.Name.LocalName == "link"))?.Attribute("href")?.Value
                    : Value(node, "link");

                if (string.IsNullOrWhiteSpace(linkRaw))
                {
                    var guid = Value(node, "guid") ?? Value(node, "id");
                    if (ContentText.IsSafeHttpUrl(guid)) linkRaw = guid;
                }

                var link = ContentText.CanonicalizeUrl(linkRaw, feedUri);
                if (link is null) continue;
                if (!seenLinks.Add(link)) continue;

                var contentHtml = isAtom
                    ? (Value(node, "content") ?? Value(node, "summary"))
                    : (FirstValueByLocalName(node, "encoded") ?? Value(node, "description"));

                var summaryHtml = isAtom ? Value(node, "summary") : Value(node, "description");

                var content = ContentText.HtmlToText(contentHtml);
                var summary = ContentText.HtmlToText(summaryHtml, 1200);

                if (content.Length < 80 && summary.Length > content.Length)
                    content = summary;

                var externalId = isAtom ? Value(node, "id") : Value(node, "guid");
                var published = ParseDate(isAtom ? (Value(node, "published") ?? Value(node, "updated")) : Value(node, "pubDate"));
                published ??= ParseDate(FirstValueByLocalName(node, "date"));

                var image = FindImage(node, contentHtml, linkRaw, feedUri);

                entries.Add(new FeedEntry(
                    title,
                    link,
                    Truncate(externalId, 500),
                    published,
                    Truncate(summary, 2000),
                    Truncate(content, 12000),
                    image));
            }
            catch (Exception ex)
            {
                // یک آیتم خراب نباید کل فید را متوقف کند.
                _ = ex;
            }
        }

        var ordered = entries
            .OrderByDescending(e => e.PublishedAt ?? DateTimeOffset.MinValue)
            .Take(MaxEntries)
            .ToList();

        return new FeedDocument(
            string.IsNullOrWhiteSpace(feedTitle) ? null : feedTitle,
            ContentText.CanonicalizeUrl(siteUrl, feedUri),
            ordered);
    }

    private static string? FindImage(XElement node, string? contentHtml, string? link, Uri feedUri)
    {
        var candidates = new List<string?>();

        foreach (var el in node.Descendants().Where(e => e.Name.LocalName is "content" or "thumbnail" or "enclosure"))
        {
            var url = el.Attribute("url")?.Value;
            var type = el.Attribute("type")?.Value;
            var medium = el.Attribute("medium")?.Value;
            if (string.IsNullOrWhiteSpace(url)) continue;
            if (type is not null && !type.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) continue;
            if (medium is not null && !medium.Equals("image", StringComparison.OrdinalIgnoreCase)) continue;
            if (type is null && medium is null && !LooksLikeImage(url)) continue;
            candidates.Add(url);
        }

        foreach (var linkEl in node.Descendants().Where(e => e.Name.LocalName == "link"))
        {
            if (!string.Equals(linkEl.Attribute("rel")?.Value, "enclosure", StringComparison.OrdinalIgnoreCase)) continue;
            var type = linkEl.Attribute("type")?.Value;
            var href = linkEl.Attribute("href")?.Value;
            if (type is not null && type.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(href))
                candidates.Add(href);
        }

        if (!string.IsNullOrWhiteSpace(contentHtml))
        {
            var match = ImgSrcRegex.Match(contentHtml);
            if (match.Success) candidates.Add(match.Groups["src"].Value);
        }

        foreach (var candidate in candidates)
        {
            var canonical = ContentText.CanonicalizeUrl(candidate, feedUri);
            if (canonical is null) continue;
            if (link is not null && canonical == ContentText.CanonicalizeUrl(link, feedUri)) continue;
            if (!IsSafeImageUrl(canonical)) continue;
            return canonical;
        }

        return null;
    }

    private static bool LooksLikeImage(string url)
        => url.Contains(".jpg", StringComparison.OrdinalIgnoreCase)
           || url.Contains(".jpeg", StringComparison.OrdinalIgnoreCase)
           || url.Contains(".png", StringComparison.OrdinalIgnoreCase)
           || url.Contains(".webp", StringComparison.OrdinalIgnoreCase)
           || url.Contains(".gif", StringComparison.OrdinalIgnoreCase);

    private static bool IsSafeImageUrl(string url) => ContentText.IsSafeHttpUrl(url);

    private static bool IsAlternate(XElement link)
    {
        var rel = link.Attribute("rel")?.Value;
        return string.IsNullOrWhiteSpace(rel) || rel.Equals("alternate", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Value(XElement? element, string localName)
        => element?.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? FirstValueByLocalName(XElement element, string localName)
        => element.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))?.Value;

    private static IEnumerable<XElement> Descendants(XElement element, string localName)
        => element.Descendants().Where(e => e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    private static DateTimeOffset? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            return parsed;
        return null;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static string ShortenError(Exception ex, int max)
    {
        var message = ex.GetBaseException().Message;
        if (string.IsNullOrWhiteSpace(message)) message = ex.Message;
        return message.Length <= max ? message : message[..max] + "…";
    }
}
