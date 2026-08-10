namespace ErrorService.Client.Services;

public static class RoleHelper
{
    private static readonly Dictionary<string, string> PersianTitles = new()
    {
        { "super_admin", "سوپر ادمین" },
        { "workshop_manager", "مدیر کارگاه" },
        { "workshop_staff", "کارمند کارگاه" },
        { "technician", "تکنسین فنی" },
        { "operator", "اپراتور" },
        { "reception", "پذیرش" },
        { "accountant", "حسابدار" },
        { "customer_service", "خدمات مشتریان" },
        { "sales", "فروش" },
        { "marketing", "بازاریابی" },
        { "content_manager", "مدیر محتوا" },
        { "support", "پشتیبانی" },
        { "admin", "مدیر سیستم" },
        { "user", "کاربر عادی" }
    };

    public static string GetPersianTitle(string roleKey)
    {
        if (string.IsNullOrWhiteSpace(roleKey))
            return "نامشخص";

        return PersianTitles.TryGetValue(roleKey.ToLowerInvariant(), out var title)
            ? title
            : roleKey;
    }
}
