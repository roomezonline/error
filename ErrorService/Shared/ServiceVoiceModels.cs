using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

/// <summary>یک اکشن صوتی (مثل دیفراست یا استارت موتور) با فایل صدای ضبط‌شده</summary>
public sealed class ServiceVoiceActionDto
{
    public int Id { get; set; }

    /// <summary>دستگاه فعال کارگاهی که این اکشن برای آن تعریف شده است.</summary>
    public int? DeviceId { get; set; }
    public string? DeviceTitle { get; set; }
    public string? DeviceNumber { get; set; }

    public string Key { get; set; } = string.Empty;
    public string TitleFa { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int RepeatCount { get; set; }
    public int Priority { get; set; }
    public string? AudioUrl { get; set; }
    public long AudioSizeBytes { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>دستگاه فعال کارگاه برای انتخاب در فرم آواها</summary>
public sealed class ServiceVoiceDeviceDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DeviceNumber { get; set; } = string.Empty;
    public string? WorkshopTitle { get; set; }
    public bool IsActive { get; set; }
    public int ActionsCount { get; set; }
}

/// <summary>
/// پاسخ عمومی فهرست آواها برای دستگاه‌های سخت‌افزاری (ESP32).
/// فقط اکشن‌های فعال که فایل صدا دارند برگردانده می‌شوند.
/// </summary>
public sealed class DeviceServiceVoicesDto
{
    /// <summary>کد دستگاه که این فهرست برای آن است.</summary>
    public string DeviceNumber { get; set; } = string.Empty;

    public string? DeviceTitle { get; set; }

    /// <summary>نسخهٔ فهرست؛ با هر تغییر در آواها عوض می‌شود تا دستگاه دوباره دریافت کند.</summary>
    public long Version { get; set; }

    public int Count { get; set; }

    public List<DeviceServiceVoiceDto> Actions { get; set; } = new();
}

/// <summary>یک آوای قابل پخش روی دستگاه</summary>
public sealed class DeviceServiceVoiceDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string TitleFa { get; set; } = string.Empty;
    public int RepeatCount { get; set; }
    public int Priority { get; set; }

    /// <summary>آدرس مطلق فایل صدا (https)</summary>
    public string AudioUrl { get; set; } = string.Empty;

    /// <summary>پسوند فایل: mp3 یا wav</summary>
    public string Format { get; set; } = string.Empty;

    public long AudioSizeBytes { get; set; }

    /// <summary>نسخهٔ همین آیتم (ثانیهٔ یونیکس آخرین ویرایش)</summary>
    public long Version { get; set; }
}

/// <summary>
/// کلیدهای استاندارد رویداد صوتی. هنگام تغییر تجهیزات پایش، همین کلیدها
/// با اکشن‌های تعریف‌شدهٔ همان دستگاه تطبیق داده می‌شوند.
/// </summary>
public static class ServiceVoiceEventKeys
{
    public const string PrizOn = "priz_on";
    public const string PrizOff = "priz_off";
    public const string MotorOn = "motor_on";
    public const string MotorOff = "motor_off";
    public const string Element1On = "element1_on";
    public const string Element1Off = "element1_off";
    public const string Element2On = "element2_on";
    public const string Element2Off = "element2_off";

    /// <summary>گزینه‌های آمادهٔ انتخاب در پنل مدیریت.</summary>
    public static IReadOnlyList<ServiceVoiceEventOption> Options { get; } = new List<ServiceVoiceEventOption>
    {
        new() { Key = PrizOn,      TitleFa = "روشن شدن برق اصلی",   HintFa = "وقتی برق اصلی وصل می‌شود" },
        new() { Key = PrizOff,     TitleFa = "قطع برق اصلی",        HintFa = "وقتی برق اصلی قطع می‌شود" },
        new() { Key = MotorOn,     TitleFa = "روشن شدن موتور",      HintFa = "استارت موتور" },
        new() { Key = MotorOff,    TitleFa = "خاموش شدن موتور",     HintFa = "توقف موتور" },
        new() { Key = Element1On,  TitleFa = "روشن شدن المنت ۱",    HintFa = "شروع گرمایش المنت ۱" },
        new() { Key = Element1Off, TitleFa = "خاموش شدن المنت ۱",   HintFa = "پایان گرمایش المنت ۱" },
        new() { Key = Element2On,  TitleFa = "روشن شدن المنت ۲",    HintFa = "شروع گرمایش المنت ۲" },
        new() { Key = Element2Off, TitleFa = "خاموش شدن المنت ۲",   HintFa = "پایان گرمایش المنت ۲" }
    };

    /// <summary>کلید رویداد متناظر با یک تغییر پایش؛ اگر نوع تغییر پشتیبانی نشود null.</summary>
    public static string? Resolve(string? changeType, bool isOn) => (changeType ?? string.Empty) switch
    {
        "Priz" => isOn ? PrizOn : PrizOff,
        "MotorState" => isOn ? MotorOn : MotorOff,
        "Element1" => isOn ? Element1On : Element1Off,
        "Element2" => isOn ? Element2On : Element2Off,
        _ => null
    };

    public static string? TitleOf(string? key) =>
        Options.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))?.TitleFa;
}

public sealed class ServiceVoiceEventOption
{
    public string Key { get; set; } = string.Empty;
    public string TitleFa { get; set; } = string.Empty;
    public string HintFa { get; set; } = string.Empty;
}

/// <summary>یک رویداد صوتی آمادهٔ پخش روی دستگاه.</summary>
public sealed class DeviceVoiceEventDto
{
    public long Id { get; set; }
    public string ActionKey { get; set; } = string.Empty;
    public string TitleFa { get; set; } = string.Empty;
    public int RepeatCount { get; set; }
    public int Priority { get; set; }
    public string AudioUrl { get; set; } = string.Empty;
    public string Format { get; set; } = "mp3";
    public long Version { get; set; }
    public bool IsTest { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>پاسخ long-poll رویدادها؛ AfterId برای ذخیره در حافظهٔ دستگاه است.</summary>
public sealed class DeviceVoiceEventsDto
{
    /// <summary>آخرین شناسهٔ بررسی‌شده؛ دستگاه آن را به‌عنوان cursor بعدی ذخیره می‌کند.</summary>
    public long AfterId { get; set; }
    public int Count { get; set; }
    public List<DeviceVoiceEventDto> Events { get; set; } = new();
}

/// <summary>
/// یک رویداد صوتی ثبت‌شده برای بازبینی در پنل مدیریت: چه تغییری رخ داده،
/// چه اکشنی انتخاب شده و کدام فایل MP3 روی دستگاه پخش شده است.
/// </summary>
public sealed class ServiceVoiceEventLogDto
{
    public long Id { get; set; }
    public string ActionKey { get; set; } = string.Empty;

    /// <summary>عنوان استاندارد رویداد؛ مثل «خاموش شدن موتور».</summary>
    public string? EventTitleFa { get; set; }

    /// <summary>نام نمایشی اکشنی که برای همین کلید ساخته شده است.</summary>
    public string? ActionTitleFa { get; set; }

    /// <summary>فایل صوتی متصل به همین اکشن در لحظهٔ خواندن گزارش.</summary>
    public string? AudioUrl { get; set; }

    public bool HasAudio { get; set; }
    public bool ActionIsActive { get; set; }

    public string SourceType { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? DescriptionFa { get; set; }
    public bool IsTest { get; set; }
    public bool IsExpired { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ServiceVoiceTestResultDto
{
    public bool Ok { get; set; }
    public int EventId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class ServiceVoiceActionUpsertRequest
{
    public int Id { get; set; }
    /// <summary>دستگاه فعال کارگاه که اکشن برای آن ساخته می‌شود.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب دستگاه فعال کارگاه الزامی است")]
    public int MonitoringDeviceId { get; set; }

    [Required(ErrorMessage = "کلید اکشن الزامی است")]
    [RegularExpression(@"^[a-z0-9_-]{2,40}$", ErrorMessage = "کلید باید ۲ تا ۴۰ کاراکتر و شامل حروف انگلیسی کوچک، عدد، خط تیره یا زیرخط باشد")]
    public string Key { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام اکشن الزامی است")]
    [MaxLength(120, ErrorMessage = "نام اکشن حداکثر ۱۲۰ کاراکتر است")]
    public string TitleFa { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [Range(1, 50, ErrorMessage = "تعداد تکرار باید بین ۱ تا ۵۰ باشد")]
    public int RepeatCount { get; set; } = 1;

    [Range(0, 1000, ErrorMessage = "اولویت باید بین ۰ تا ۱۰۰۰ باشد")]
    public int Priority { get; set; } = 10;
}
