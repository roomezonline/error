namespace ErrorService.Shared;

public sealed record SiteFaqCategory(string Key, string Title, string Icon);

public static class SiteFaqCatalog
{
    public const string DefaultCategoryKey = "shop";

    public static readonly IReadOnlyList<SiteFaqCategory> Categories = new List<SiteFaqCategory>
    {
        new("shop", "خرید و سفارش", "basket"),
        new("repair", "تعمیرات و سرویس", "wrench"),
        new("payment", "پرداخت و هزینه", "credit-card"),
        new("shipping", "ارسال و تحویل", "location"),
        new("academy", "آکادمی و آموزش", "book"),
        new("account", "حساب کاربری", "person"),
    };

    public static SiteFaqCategory? Find(string? key)
        => Categories.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

    public static int OrderOf(string? key)
    {
        for (var i = 0; i < Categories.Count; i++)
        {
            if (string.Equals(Categories[i].Key, key, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return int.MaxValue;
    }

    public static string TitleOf(string? key) => Find(key)?.Title ?? "سایر";
    public static string IconOf(string? key) => Find(key)?.Icon ?? "list";
}

public class SiteFaqEntryDto
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string CategoryKey { get; set; } = SiteFaqCatalog.DefaultCategoryKey;
    public string? Keywords { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class SiteFaqUpsertRequest
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string CategoryKey { get; set; } = SiteFaqCatalog.DefaultCategoryKey;
    public string? Keywords { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
}
