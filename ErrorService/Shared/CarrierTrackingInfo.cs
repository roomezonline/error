using System;
using System.Collections.Generic;

namespace ErrorService.Shared;

/// <summary>
/// شناسه شرکت‌های حمل و ساخت لینک رهگیری مرسوله بر اساس شرکت حمل و کد رهگیری.
/// این کلاس در سرور و کلاینت مشترک است تا لینک رهگیری همه‌جا یکسان باشد.
/// </summary>
public static class CarrierTrackingInfo
{
    /// <summary>مقدار گزینهٔ «سایر» در سلکتور شرکت حمل.</summary>
    public const string OtherCarrier = "سایر";

    /// <summary>شرکت‌های حمل پیشنهادی برای سلکتور پنل ادمین.</summary>
    public static IReadOnlyList<string> SuggestedCarriers { get; } = new[]
    {
        "پست پیشتاز",
        "پست سفارشی",
        "تیپاکس",
        "اسنپ‌باکس"
    };

    public static bool IsPost(string? carrier)
    {
        var value = NormalizeCarrier(carrier);
        return value.Contains("پست") || value.Contains("پیشتاز") || value.Contains("post") || value.Contains("pishtaz");
    }

    public static bool IsTipax(string? carrier)
    {
        var value = NormalizeCarrier(carrier);
        return value.Contains("تیپاکس") || value.Contains("tipax");
    }

    public static bool IsSnappBox(string? carrier)
    {
        var value = NormalizeCarrier(carrier);
        return value.Contains("اسنپ") || value.Contains("snapp");
    }

    /// <summary>
    /// لینک صفحهٔ رهگیری رسمی شرکت حمل را برمی‌گرداند.
    /// برای تیپاکس صفحهٔ رسمی رهگیری باز می‌شود (سایت تیپاکس کد را از آدرس نمی‌خواند)
    /// و کد رهگیری جداگانه کنار لینک نمایش داده/کپی می‌شود.
    /// </summary>
    public static string? BuildUrl(string? carrier, string? trackingNumber)
    {
        var code = (trackingNumber ?? string.Empty).Trim();
        if (code.Length == 0) return null;

        var encodedCode = Uri.EscapeDataString(code);

        if (IsTipax(carrier))
            return "https://tipaxco.com/tracking";

        if (IsSnappBox(carrier))
            return $"https://snappbox.ir/tracking/{encodedCode}";

        if (IsPost(carrier) || string.IsNullOrWhiteSpace(carrier))
            return $"https://tracking.post.ir/?id={encodedCode}";

        return null;
    }

    private static string NormalizeCarrier(string? carrier) =>
        (carrier ?? string.Empty).Trim().ToLowerInvariant();
}
