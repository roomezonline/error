namespace ErrorService.Shared;

/// <summary>
/// دسته‌بندی‌های پیشنهادی آکادمی. ادمین می‌تواند دسته‌ی دلخواه هم وارد کند؛
/// این فهرست فقط برای پیشنهاد در فرم‌ها و ترتیب نمایش چیپ‌ها استفاده می‌شود.
/// </summary>
public static class AcademyCatalog
{
    public static readonly IReadOnlyList<string> SuggestedCategories = new[]
    {
        "یخچال و فریزر",
        "لباسشویی و ظرفشویی",
        "کولر و تهویه",
        "مایکروویو و اجاق",
        "برد و الکترونیک",
        "ابزار دقیق و اندازه‌گیری",
        "ایمنی و عیب‌یابی",
        "عمومی"
    };

    /// <summary>دسته‌های پیشنهادی اول، بقیه به ترتیب الفبا.</summary>
    public static IEnumerable<string> Order(IEnumerable<string> categories)
    {
        var suggested = SuggestedCategories;
        return categories
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c =>
            {
                var idx = suggested.ToList().FindIndex(s => string.Equals(s, c, StringComparison.OrdinalIgnoreCase));
                return idx < 0 ? int.MaxValue : idx;
            })
            .ThenBy(c => c, StringComparer.Ordinal);
    }
}
