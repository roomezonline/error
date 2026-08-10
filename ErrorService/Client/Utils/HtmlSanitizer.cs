using System.Text.RegularExpressions;

namespace ErrorService.Client.Utils;

public static class HtmlSanitizer
{
    private static readonly HashSet<string> SafeTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "span", "h1", "h2", "h3", "h4", "h5", "h6",
        "ul", "ol", "li", "dl", "dt", "dd",
        "table", "thead", "tbody", "tfoot", "tr", "td", "th", "caption", "colgroup", "col",
        "a", "img", "figure", "figcaption",
        "strong", "em", "b", "i", "u", "s", "mark", "small", "sub", "sup", "abbr", "cite", "code", "kbd", "pre", "q",
        "br", "hr", "wbr",
        "blockquote", "address",
        "video", "audio", "source", "track",
        "iframe"
    };

    private static readonly HashSet<string> SafeUriSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "mailto", "tel", "data"
    };

    private static readonly Regex TagRegex = new(@"<(\/?)(\w[\w-]*)([^>]*?)>", RegexOptions.Compiled);
    private static readonly Regex AttrRegex = new(@"(\w[\w-]*)(?:\s*=\s*(?:""([^""]*)""|'([^']*)'|(\S+)))?", RegexOptions.Compiled);

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        return TagRegex.Replace(html, match =>
        {
            var isClosing = match.Groups[1].Value == "/";
            var tagName = match.Groups[2].Value;
            var attrs = match.Groups[3].Value;

            if (!SafeTags.Contains(tagName))
                return "";
            
            if (isClosing)
                return $"</{tagName}>";

            var safeAttrs = SanitizeAttributes(tagName, attrs);
            return safeAttrs.Length > 0 ? $"<{tagName} {safeAttrs}>" : $"<{tagName}>";
        });
    }

    private static string SanitizeAttributes(string tagName, string attrs)
    {
        var result = new System.Text.StringBuilder();
        
        foreach (Match m in AttrRegex.Matches(attrs))
        {
            var name = m.Groups[1].Value.ToLower();
            var value = m.Groups[2].Success ? m.Groups[2].Value
                       : m.Groups[3].Success ? m.Groups[3].Value
                       : m.Groups[4].Success ? m.Groups[4].Value
                       : "";

            if (IsDangerousAttribute(name, value))
                continue;

            if (!IsAttributeAllowed(tagName, name))
                continue;

            if (result.Length > 0) result.Append(' ');
            if (string.IsNullOrEmpty(value))
                result.Append(name);
            else
            {
                var escaped = value.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
                result.Append($"{name}=\"{escaped}\"");
            }
        }

        return result.ToString();
    }

    private static bool IsDangerousAttribute(string name, string value)
    {
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
            return true;

        if (name is "href" or "src" or "action" or "formaction")
        {
            var trimmed = value.Trim().ToLower();
            if (trimmed.StartsWith("javascript:") || trimmed.StartsWith("vbscript:") || trimmed.StartsWith("data:") && !trimmed.StartsWith("data:image/"))
                return true;
        }

        return false;
    }

    private static bool IsAttributeAllowed(string tagName, string name)
    {
        return name switch
        {
            "id" or "class" or "style" or "title" or "lang" or "dir" or "tabindex" => true,
            "href" or "target" or "rel" or "download" => tagName == "a",
            "src" or "alt" or "width" or "height" or "loading" => tagName is "img" or "source" or "video" or "audio" or "iframe",
            "poster" or "preload" or "controls" or "autoplay" or "loop" or "muted" => tagName is "video" or "audio",
            "type" => tagName is "source",
            "allowfullscreen" or "sandbox" or "referrerpolicy" or "frameborder" => tagName == "iframe",
            "colspan" or "rowspan" or "scope" => tagName is "th" or "td",
            "start" or "reversed" or "type" => tagName is "ol" or "li",
            "cite" => tagName is "blockquote" or "q",
            "datetime" => tagName is "time",
            _ => false
        };
    }
}
