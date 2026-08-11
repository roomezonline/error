namespace ErrorService.Server.Services.ChatAi;

public interface IChatAiService
{
    string Name { get; }

    Task<string?> GetReplyAsync(string userMessage, string operatorName);
}

public static class PersianTextMatcher
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text
            .Replace('\u064A', '\u06CC') // ی
            .Replace('\u0643', '\u06A9') // ک
            .Replace('\u0621', '\u0627') // ء → ا
            .Replace('\u0623', '\u0627') // أ → ا
            .Replace('\u0625', '\u0627') // إ → ا
            .Replace('\u0622', '\u0627') // آ → ا
            .Replace("\u0647\u200C", "") // ه + ZWNJ
            .ToLowerInvariant()
            .Trim();
    }

    public static bool ContainsKeyword(string normalizedText, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return false;
        var nk = Normalize(keyword);
        if (nk.Length == 0) return false;
        return normalizedText.Contains(nk);
    }

    public static double Coef(string a, string b) => 1 - (double)Levenshtein(a, b) / Math.Max(a.Length, b.Length);

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[a.Length, b.Length];
    }
}