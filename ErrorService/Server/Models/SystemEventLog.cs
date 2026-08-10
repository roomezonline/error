using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErrorService.Shared;

namespace ErrorService.Server.Models;

/// <summary>
/// سیستم گزارش‌دهی جامع رویدادها و خطاها - چشمانی قوی برای مانیتورینگ
/// </summary>
public class SystemEventLog
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// زمان دقیق وقوع رویداد
    /// </summary>
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// سطح اهمیت رویداد
    /// </summary>
    public EventSeverity Severity { get; set; } = EventSeverity.Info;
    
    /// <summary>
    /// دسته‌بندی رویداد
    /// </summary>
    public EventCategory Category { get; set; } = EventCategory.System;
    
    /// <summary>
    /// عنوان فارسی قابل فهم برای ادمین
    /// </summary>
    [MaxLength(200)]
    public string PersianTitle { get; set; } = string.Empty;
    
    /// <summary>
    /// توضیحات فارسی کامل
    /// </summary>
    public string PersianDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// عنوان فنی انگلیسی (برای تیم فنی)
    /// </summary>
    [MaxLength(200)]
    public string TechnicalTitle { get; set; } = string.Empty;
    
    /// <summary>
    /// جزئیات فنی کامل انگلیسی
    /// </summary>
    public string TechnicalDetails { get; set; } = string.Empty;
    
    /// <summary>
    /// stack trace یا traceback خطا
    /// </summary>
    public string? StackTrace { get; set; }
    
    /// <summary>
    /// کد خطا یا exception type
    /// </summary>
    [MaxLength(100)]
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// آدرس IP کاربر/کلاینت
    /// </summary>
    [MaxLength(50)]
    public string? ClientIp { get; set; }
    
    /// <summary>
    /// User-Agent مرورگر/دستگاه
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// شناسه کاربر (اگر لاگین بوده)
    /// </summary>
    public int? UserId { get; set; }
    
    /// <summary>
    /// نام کاربری (برای ردیابی سریع)
    /// </summary>
    [MaxLength(100)]
    public string? UserName { get; set; }
    
    /// <summary>
    /// مسیر URL مربوطه
    /// </summary>
    [MaxLength(500)]
    public string? RequestPath { get; set; }
    
    /// <summary>
    /// متد HTTP
    /// </summary>
    [MaxLength(10)]
    public string? HttpMethod { get; set; }
    
    /// <summary>
    /// پارامترهای ورودی
    /// </summary>
    public string? RequestParameters { get; set; }
    
    /// <summary>
    /// کد پاسخ HTTP
    /// </summary>
    public int? ResponseStatusCode { get; set; }
    
    /// <summary>
    /// زمان پاسخ‌دهی (میلی‌ثانیه)
    /// </summary>
    public long? ResponseTimeMs { get; set; }
    
    /// <summary>
    /// کامپوننت یا سرویس مربوطه
    /// </summary>
    [MaxLength(100)]
    public string? Component { get; set; }
    
    /// <summary>
    /// تگ‌ها برای دسته‌بندی سریع (JSON array)
    /// </summary>
    public string? Tags { get; set; }
    
    /// <summary>
    /// وضعیت بررسی
    /// </summary>
    public InvestigationStatus Status { get; set; } = InvestigationStatus.New;
    
    /// <summary>
    /// یادداشت‌های تیم فنی
    /// </summary>
    public string? AdminNotes { get; set; }
    
    /// <summary>
    /// تعداد تکرار (برای خطاهای مشابه)
    /// </summary>
    public int OccurrenceCount { get; set; } = 1;
    
    /// <summary>
    /// آخرین زمان تکرار
    /// </summary>
    public DateTimeOffset? LastOccurrenceAt { get; set; }
    
    /// <summary>
    /// شناسه دستگاه/سیستم
    /// </summary>
    [MaxLength(100)]
    public string? DeviceId { get; set; }
    
    /// <summary>
    /// نوع دستگاه (Mobile/Desktop/Tablet)
    /// </summary>
    [MaxLength(20)]
    public string? DeviceType { get; set; }
    
    /// <summary>
    /// آیا اطلاع‌رسانی real-time انجام شده؟
    /// </summary>
    public bool IsNotified { get; set; } = false;
    
    /// <summary>
    /// آیا خطا حل شده است؟
    /// </summary>
    public bool IsResolved { get; set; } = false;
    
    /// <summary>
    /// زمان حل خطا
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; set; }
    
    /// <summary>
    /// کاربر حل‌کننده
    /// </summary>
    public int? ResolvedByUserId { get; set; }
}
