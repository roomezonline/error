namespace ErrorService.Server.Models;

/// <summary>
/// تنظیمات تحلیل‌گر فضای هاست. این مقادیر از طریق مدال تنظیمات در صفحه
/// /admin/hostinfo قابل ویرایش هستند و بر روی محاسبه ظرفیت کل اثر می‌گذارند.
/// </summary>
public sealed class HostInfoSettings
{
    public int Id { get; set; }

    /// <summary>فضای کل اختصاص‌یافته به هاست به گیگابایت؛ صفر یعنی محاسبه بر اساس کل دیسک سرور.</summary>
    public double QuotaGb { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
