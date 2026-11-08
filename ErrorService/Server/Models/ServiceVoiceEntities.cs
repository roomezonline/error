namespace ErrorService.Server.Models;

/// <summary>اکشن صوتی سامانه آوای ارورسرویس؛ فایل صدا در wwwroot/uploads/service-voices ذخیره می‌شود.</summary>
public sealed class ServiceVoiceAction
{
    public int Id { get; set; }

    /// <summary>دستگاه فعال کارگاه که این آوا برای آن ضبط شده است.</summary>
    public int? MonitoringDeviceId { get; set; }
    public MonitoringDevice? MonitoringDevice { get; set; }

    public string Key { get; set; } = string.Empty;
    public string TitleFa { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int RepeatCount { get; set; } = 1;
    public int Priority { get; set; } = 10;
    public string? AudioUrl { get; set; }
    public long AudioSizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// رویداد صوتی آمادهٔ پخش برای یک دستگاه؛ هنگام تغییر تجهیزات پایش
/// (برق، موتور، المنت‌ها) ساخته می‌شود و دستگاه با long-poll آن را می‌گیرد.
/// </summary>
public sealed class DeviceVoiceEvent
{
    public int Id { get; set; }

    public int MonitoringDeviceId { get; set; }
    public MonitoringDevice? MonitoringDevice { get; set; }

    /// <summary>کد دستگاه برای جستجوی سریع بدون join.</summary>
    public string DeviceCode { get; set; } = string.Empty;

    /// <summary>کلید اکشنی که باید پخش شود؛ مثل priz_on یا motor_off.</summary>
    public string ActionKey { get; set; } = string.Empty;

    /// <summary>نوع تغییر در گزارش پایش؛ Priz/MotorState/Element1/Element2.</summary>
    public string SourceType { get; set; } = string.Empty;

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? DescriptionFa { get; set; }

    /// <summary>رویداد آزمایشی که از پنل مدیریت ارسال شده است.</summary>
    public bool IsTest { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>پس از این زمان رویداد کهنه است و پخش نمی‌شود.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
