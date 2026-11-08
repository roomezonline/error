namespace ErrorService.Client.Utils;

/// <summary>کمک‌کنندهٔ ورودی مبالغ: فقط ارقام را نگه می‌دارد و هنگام تایپ سه‌رقمی جدا می‌کند.</summary>
public static class MoneyFormat
{
    public static string DigitsOnly(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (ch >= '0' && ch <= '9') sb.Append(ch);
            else if (ch >= '\u06F0' && ch <= '\u06F9') sb.Append((char)(ch - '\u06F0' + '0'));
            else if (ch >= '\u0660' && ch <= '\u0669') sb.Append((char)(ch - '\u0660' + '0'));
        }
        return sb.ToString();
    }

    public static string Group(string? raw)
    {
        var digits = DigitsOnly(raw);
        if (digits.Length == 0) return string.Empty;

        var first = digits.Length % 3;
        var sb = new System.Text.StringBuilder(digits.Length + digits.Length / 3);
        var i = 0;
        if (first > 0)
        {
            sb.Append(digits, 0, first);
            i = first;
        }
        for (; i < digits.Length; i += 3)
        {
            if (sb.Length > 0) sb.Append(',');
            sb.Append(digits, i, 3);
        }
        return sb.ToString();
    }

    public static decimal? Parse(string? raw)
    {
        var digits = DigitsOnly(raw);
        return digits.Length > 0 && decimal.TryParse(digits, out var value) ? value : null;
    }
}
