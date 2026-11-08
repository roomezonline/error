namespace ErrorService.Shared;

/// <summary>
/// عنوان‌های استاندارد صفحات پویا؛ کلاینت و خزنده باید دقیقاً یک متن تولید کنند
/// تا گوگل دو عنوان متفاوت برای یک URL نبیند.
/// </summary>
public static class SeoTitles
{
    private const string Brand = "ارورسرویس";

    public static string Product(string name) => $"{name} | قیمت و مشخصات | {Brand}";

    public static string News(string title) => $"{title} | {Brand}";

    public static string Course(string title) => $"{title} | دوره آموزشی | {Brand}";

    public static string Article(string title) => $"{title} | مقاله آموزشی | {Brand}";

    public static string Lesson(string title, string? courseTitle)
    {
        var course = string.IsNullOrWhiteSpace(courseTitle) ? null : courseTitle.Trim();
        return course == null
            ? $"{title} | آکادمی {Brand}"
            : $"{title} - {course} | آکادمی {Brand}";
    }

    public static string ErrorCode(string code, string brand, string device)
        => $"کد خطای {code} {brand} {device} | {Brand}";
}
