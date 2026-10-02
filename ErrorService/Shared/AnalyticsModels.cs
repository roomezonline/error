namespace ErrorService.Shared;

public class VisitTrackRequest
{
    public string? Path { get; set; }
    public string? Referrer { get; set; }
    public string? VisitorId { get; set; }
}

public class VisitSummaryDto
{
    public int TodayVisits { get; set; }
    public int TodayUniques { get; set; }

    /// <summary>تعداد صفحه‌بازدیدهای امروز (جزئی‌تر از «بازدید»).</summary>
    public int TodayPageViews { get; set; }

    public int WeekVisits { get; set; }
    public int WeekUniques { get; set; }
    public int MonthVisits { get; set; }
    public int MonthUniques { get; set; }
    public int YearVisits { get; set; }
    public int TotalVisits { get; set; }
    public int ActiveDays { get; set; }
}

public class VisitListItemDto
{
    public long Id { get; set; }

    /// <summary>زمان شروع بازدید (جلسه).</summary>
    public DateTime VisitedAtUtc { get; set; }
    public DateTime VisitedAtLocal { get; set; }

    /// <summary>زمان پایان بازدید (آخرین صفحه دیده‌شده).</summary>
    public DateTime EndedAtLocal { get; set; }

    public int DurationMinutes { get; set; }
    public string Ip { get; set; } = "";

    /// <summary>صفحه ورودی بازدید.</summary>
    public string Path { get; set; } = "";

    /// <summary>نام فارسی نمایشی صفحه ورودی (مثلاً «صفحه اصلی») — اگر قابل شناسایی نباشد خالی است.</summary>
    public string PathTitle { get; set; } = "";

    /// <summary>آخرین صفحه دیده‌شده.</summary>
    public string LastPath { get; set; } = "";

    /// <summary>نام فارسی نمایشی آخرین صفحه — اگر قابل شناسایی نباشد خالی است.</summary>
    public string LastPathTitle { get; set; } = "";

    public string Referrer { get; set; } = "";
    public string UserAgent { get; set; } = "";
    public string VisitorId { get; set; } = "";

    /// <summary>نام و نام خانوادگی کاربر واردشده — برای مهمان‌ها خالی.</summary>
    public string UserName { get; set; } = "";

    public string IdentityKey { get; set; } = "";

    /// <summary>تعداد صفحات دیده‌شده در این بازدید.</summary>
    public int PageViews { get; set; }
}

public class VisitListResponse
{
    public int Total { get; set; }

    /// <summary>مجموع صفحه‌بازدیدهای بازه انتخابی.</summary>
    public int TotalPageViews { get; set; }

    public List<VisitListItemDto> Items { get; set; } = new();
}

public class VisitTrendPointDto
{
    public string Date { get; set; } = "";
    public int Visits { get; set; }
    public int Uniques { get; set; }
}

public class VisitTrendResponse
{
    public List<VisitTrendPointDto> Points { get; set; } = new();
}

/// <summary>یک مسیر در گزارش «پربحترین صفحات».</summary>
public class VisitPathStatDto
{
    /// <summary>مسیر نرمال‌شده صفحه (بدون query/hash).</summary>
    public string Path { get; set; } = "";

    /// <summary>نام فارسی نمایشی صفحه (مثلاً «صفحه اصلی» یا نام محصول) — اگر قابل شناسایی نباشد خالی است.</summary>
    public string Title { get; set; } = "";

    /// <summary>تعداد صفحه‌بازدیدهای این مسیر در بازه انتخابی.</summary>
    public int PageViews { get; set; }

    /// <summary>درصد سهم این مسیر از کل صفحه‌بازدیدهای بازه (۰ تا ۱۰۰).</summary>
    public double Share { get; set; }
}

public class VisitPathsResponse
{
    /// <summary>بازه بر حسب روز.</summary>
    public int Days { get; set; }

    /// <summary>کل صفحه‌بازدیدهای بازه (مبنای درصد سهم).</summary>
    public int TotalPageViews { get; set; }

    public List<VisitPathStatDto> Items { get; set; } = new();
}

public class HostDiskInfoDto
{
    public bool Available { get; set; }
    public string Volume { get; set; } = "";

    /// <summary>سهمیه فضای هاست (چون QuotaBased) یا کل فضای درایو سرور.</summary>
    public double TotalGb { get; set; }
    public double UsedGb { get; set; }
    public double FreeGb { get; set; }
    public double UsedPercent { get; set; }

    /// <summary>سهمیه این حساب (فقط وقتی QuotaBased باشد).</summary>
    public double QuotaGb { get; set; }

    /// <summary>جزئیات اشغال به مگابایت: فایل‌های برنامه + بانک اطلاعاتی.</summary>
    public double UsedMb { get; set; }

    /// <summary>آیا محاسبات بر اساس سهمیه اختصاص‌یافته به هاست انجام شده یا کل درایو سرور؟</summary>
    public bool QuotaBased { get; set; }

    /// <summary>اطلاعات کل درایو سرور — صرفاً برای مقایسه، خارج از سهمیه هاست.</summary>
    public string PhysicalVolume { get; set; } = "";
    public double PhysicalTotalGb { get; set; }
    public double PhysicalFreeGb { get; set; }
}

public class HostFolderDto
{
    public string Name { get; set; } = "";
    public double SizeMb { get; set; }
    public double Percent { get; set; }
}

public class HostFilesDto
{
    /// <summary>حجم wwwroot — تفکیک پوشه‌ها.</summary>
    public double TotalMb { get; set; }

    /// <summary>حجم کل فایل‌های برنامه (پوشه هاست، بدون bin/obj/.git) — مبنای سهمیه هاست.</summary>
    public double AppTotalMb { get; set; }

    public double RootFilesMb { get; set; }
    public List<HostFolderDto> Folders { get; set; } = new();
}

public class HostDbTableDto
{
    public string Name { get; set; } = "";
    public long Rows { get; set; }
    public double SizeMb { get; set; }
}

public class HostDatabaseDto
{
    public bool Available { get; set; }
    public double TotalMb { get; set; }
    public double DataMb { get; set; }
    public double LogMb { get; set; }
    public List<HostDbTableDto> TopTables { get; set; } = new();
}

public class HostProcessDto
{
    public double ManagedMemoryMb { get; set; }
    public double WorkingSetMb { get; set; }
    public double UptimeHours { get; set; }
    public int ProcessorCount { get; set; }
}

public class HostInfoDto
{
    public DateTime GeneratedAt { get; set; }
    public bool Refreshing { get; set; }
    public HostDiskInfoDto Disk { get; set; } = new();
    public HostFilesDto Files { get; set; } = new();
    public HostDatabaseDto Database { get; set; } = new();
    public HostProcessDto Process { get; set; } = new();
}
