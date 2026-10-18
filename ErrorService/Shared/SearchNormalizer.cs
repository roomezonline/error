using System.Text;

namespace ErrorService.Shared;

public static class SearchNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch) || ch == '\u200C')
                continue;

            var c = ch switch
            {
                '\u064A' => 'ی',
                '\u0649' => 'ی',
                '\u0643' => 'ک',
                '\u0623' => 'ا',
                '\u0625' => 'ا',
                '\u0671' => 'ا',
                '\u0622' => 'ا',
                >= '\u06F0' and <= '\u06F9' => (char)('0' + (ch - '\u06F0')),
                >= '\u0660' and <= '\u0669' => (char)('0' + (ch - '\u0660')),
                >= '\u064B' and <= '\u0652' => '\0',
                '\u0670' => '\0',
                _ => ch,
            };

            if (c != '\0')
                sb.Append(c);
        }

        return sb.ToString();
    }

    public static List<string> QueryVariants(string? text)
    {
        var list = new List<string>(4);
        var raw = (text ?? string.Empty).Trim();
        if (raw.Length > 0)
            list.Add(raw);

        var canonical = Normalize(raw);
        if (canonical.Length > 0 && !list.Contains(canonical))
            list.Add(canonical);

        var persianDigits = ToPersianDigits(canonical);
        if (persianDigits.Length > 0 && !list.Contains(persianDigits))
            list.Add(persianDigits);

        var rawPersianDigits = ToPersianDigits(raw.Trim());
        if (rawPersianDigits.Length > 0 && !list.Contains(rawPersianDigits))
            list.Add(rawPersianDigits);

        return list;
    }

    public static string ToPersianDigits(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
            sb.Append(ch >= '0' && ch <= '9' ? (char)('۰' + (ch - '0')) : ch);
        return sb.ToString();
    }
}
