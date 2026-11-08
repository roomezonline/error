namespace ErrorService.Shared;

/// <summary>
/// تیتر و توضیح صفحه فهرست فروشگاه؛ برای هر دسته و هر صفحه متن یکتا می‌سازد
/// تا تمام حالت‌های فیلتر با یک کپشن تکراری در گوگل ظاهر نشوند.
/// </summary>
public static class ShopListingSeo
{
    private const string BaseTitle = "فروشگاه قطعات یدکی و لوازم الکترونیک | ارورسرویس";
    private const string BaseDescription = "خرید قطعات یدکی اصل لوازم خانگی، قطعات الکترونیک، ابزار دقیق و تجهیزات صنعتی با ضمانت اصالت و ارسال سریع از فروشگاه ارورسرویس";
    private const string BaseKeywords = "فروشگاه قطعات یدکی, قطعات الکترونیک, ابزار دقیق, خرید قطعات لوازم خانگی, ارورسرویس";

    public static (string Title, string Description, string Keywords) Build(
        string? categoryName,
        int page,
        string? categorySeoTitle = null,
        string? categorySeoDescription = null)
    {
        var category = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();

        string title, description, keywords;
        if (category == null)
        {
            title = BaseTitle;
            description = BaseDescription;
            keywords = BaseKeywords;
        }
        else
        {
            title = string.IsNullOrWhiteSpace(categorySeoTitle)
                ? $"خرید {category} | فروشگاه ارورسرویس"
                : categorySeoTitle.Trim();
            description = string.IsNullOrWhiteSpace(categorySeoDescription)
                ? $"خرید {category} با مشخصات فنی، قیمت به‌روز، ضمانت اصالت و ارسال سریع از فروشگاه ارورسرویس"
                : categorySeoDescription.Trim();
            keywords = $"{category}, خرید {category}, قطعات یدکی, ارورسرویس";
        }

        if (page > 1)
            title = $"{title} - صفحه {page}";

        return (title, description, keywords);
    }
}
