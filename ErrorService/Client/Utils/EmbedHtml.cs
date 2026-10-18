using System.Text;
using System.Text.RegularExpressions;

namespace ErrorService.Client.Utils;

public static class EmbedHtml
{
    private static readonly Regex AparatScriptRegex = new(
        "<script[^>]*src\\s*=\\s*[\"']https://(?:www\\.)?aparat\\.com/embed/([^?\"'/]+)([^\"']*)[\"'][^>]*>\\s*</script>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex StyleBlockRegex = new(
        "<\\s*style\\b[^>]*>.*?<\\s*/\\s*style\\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex OpenStyleOrScriptRegex = new(
        "<\\s*(style|script)\\b[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CloseStyleOrScriptRegex = new(
        "<\\s*/\\s*(style|script)\\s*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex IframeTagRegex = new(
        "<iframe\\b[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const string IframeAllow = "autoplay; fullscreen; picture-in-picture; encrypted-media";

    /// <summary>
    /// کدهای embed جاسازی‌شده را قبل از نمایش آماده می‌کند.
    /// اسکریپت embed آپارات با innerHTML اجرا نمی‌شود، آن را به iframe استاندارد تبدیل می‌کنیم.
    /// برای نمایش امن (بدون اجرای style/script خارجی) تگ‌های style و script همراه محتوایشان حذف می‌شوند.
    /// </summary>
    public static string Fix(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        var result = AparatScriptRegex.Replace(html, static m => BuildAparatFrame(m.Groups[1].Value, m.Groups[2].Value));

        result = StyleBlockRegex.Replace(result, "");
        result = OpenStyleOrScriptRegex.Replace(result, "");
        result = CloseStyleOrScriptRegex.Replace(result, "");

        return EnsureIframeAllow(result);
    }

    private static string EnsureIframeAllow(string html)
    {
        if (html.IndexOf('<') < 0) return html;

        return IframeTagRegex.Replace(html, static m =>
        {
            var tag = m.Value;
            if (tag.Contains("allow=", StringComparison.OrdinalIgnoreCase)) return tag;

            var end = tag.LastIndexOf('>');
            if (end < 0) return tag;

            return tag[..end] + $" allow=\"{IframeAllow}\"" + tag[end..];
        });
    }

    private static string BuildAparatFrame(string id, string rawQuery)
    {
        var query = CleanQuery(rawQuery);

        var sb = new StringBuilder();
        sb.Append("<div class=\"h_iframe-aparat_embed_frame\">");
        sb.Append("<span style=\"display: block; padding-top: 57%\"></span>");
        sb.Append("<iframe src=\"https://www.aparat.com/video/video/embed/videohash/")
          .Append(id)
          .Append("/vt/frame")
          .Append(query)
          .Append("\" width=\"100%\" height=\"100%\" allowfullscreen=\"true\" ")
          .Append("title=\"ویدیو آموزشی آکادمی ارورسرویس\"></iframe>");
        sb.Append("</div>");

        return sb.ToString();
    }

    private static string CleanQuery(string rawQuery)
    {
        var query = rawQuery.Trim().TrimStart('?', '&', ' ');
        if (query.Length == 0) return "?titleShow=true";

        var kept = new List<string>();

        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var key = part;
            var eq = part.IndexOf('=');
            if (eq >= 0) key = part[..eq];

            if (key.StartsWith("data[", StringComparison.OrdinalIgnoreCase)) continue;
            kept.Add(part);
        }

        if (kept.Count == 0) return "?titleShow=true";

        return "?" + string.Join("&", kept);
    }
}
