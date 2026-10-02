namespace ErrorService.Server.Models;

/// <summary>لاگ خام بازدید سایت عمومی — حداکثر ۹۰ روز نگهداری می‌شود.</summary>
public class VisitLog
{
    public long Id { get; set; }

    /// <summary>زمان UTC بازدید.</summary>
    public DateTime VisitedAtUtc { get; set; }

    /// <summary>تاریخ شمسی بازدید (برای کوئری‌های سریع ایندکس‌دار).</summary>
    public DateOnly VisitDate { get; set; }

    public string Ip { get; set; } = "";

    public string Path { get; set; } = "";

    public string Referrer { get; set; } = "";

    public string UserAgent { get; set; } = "";

    public string VisitorId { get; set; } = "";

    public bool IsBot { get; set; }
}

/// <summary>آمار روزانه تجمیعی بازدید — برای همیشه نگهداری می‌شود.</summary>
public class DailyVisitStat
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public int Visits { get; set; }

    public int Uniques { get; set; }
}

/// <summary>
/// آمار صفحه‌بازدید به تفکیک «روز + مسیر» — برای همیشه نگهداری می‌شود و
/// گزارش «پربحترین صفحات» (بیشترین پاخور) را می‌سازد.
/// هر ردیف = یک مسیر در یک روز؛ از روی لاگ خام (VisitLogs) بازمحاسبه می‌شود.
/// </summary>
public class DailyPagePathStat
{
    public int Id { get; set; }

    /// <summary>تاریخ شمسی بازدید (وقت تهران).</summary>
    public DateOnly Date { get; set; }

    /// <summary>مسیر نرمال‌شده صفحه (بدون query/hash).</summary>
    public string Path { get; set; } = "";

    /// <summary>تعداد صفحه‌بازدیدهای این مسیر در این روز (بدون ربات‌ها).</summary>
    public int PageViews { get; set; }
}

/// <summary>
/// جلسه (Session) استاندارد بازدید — معیار جهانی GA:
/// همه صفحاتی که یک بازدیدکننده در بازه ۳۰ دقیقه بدون وقفه می‌بیند «یک بازدید» محسوب می‌شود.
/// هر ردیف این جدول یعنی «یک بازدید»؛ ردیف‌های VisitLog فقط صفحه‌بازدیدهای (Page View) جزئی هستند.
/// </summary>
public class VisitSession
{
    public long Id { get; set; }

    /// <summary>زمان شروع جلسه (UTC).</summary>
    public DateTime StartedAtUtc { get; set; }

    /// <summary>زمان آخرین صفحه‌بازدید این جلسه (UTC) — برای محاسبه مدت و وقفه ۳۰ دقیقه.</summary>
    public DateTime EndedAtUtc { get; set; }

    /// <summary>تاریخ شمسی شروع جلسه (وقت تهران) — برای کوئری‌های ایندکس‌دار.</summary>
    public DateOnly VisitDate { get; set; }

    /// <summary>شناسه مرورگر/دستگاه (کلید ادامه جلسه).</summary>
    public string VisitorId { get; set; } = "";

    /// <summary>هویت اصلی بازدیدکننده: a{id} کاربر سایت · w{id} کاربر پنل کارگاه · v{visitorId} مهمان.</summary>
    public string IdentityKey { get; set; } = "";

    /// <summary>نام و نام خانوادگی کاربر واردشده (برای مهمان‌ها خالی).</summary>
    public string UserName { get; set; } = "";

    public string Ip { get; set; } = "";

    public string UserAgent { get; set; } = "";

    /// <summary>مبدأ ورود به جلسه.</summary>
    public string Referrer { get; set; } = "";

    /// <summary>صفحه ورودی (Entry Page).</summary>
    public string EntryPath { get; set; } = "";

    /// <summary>آخرین صفحه‌ای که در این جلسه دیده شده.</summary>
    public string LastPath { get; set; } = "";

    /// <summary>تعداد صفحه‌بازدیدهای این جلسه.</summary>
    public int PageViews { get; set; }

    public bool IsBot { get; set; }
}
