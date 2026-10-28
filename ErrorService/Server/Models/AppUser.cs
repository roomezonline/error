using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public class AppUser
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? PasswordPlain { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>حذف نرم: کاربر از همه لیست‌ها، ورود و ثبت‌نام کنار گذاشته می‌شود اما رکورد و سابقه‌اش حفظ می‌گردد.</summary>
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }

    [MaxLength(1000)]
    public string? Address { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool ChatAiDisabled { get; set; }
    public int? ChatAiDailyLimitOverride { get; set; }

    /// <summary>
    /// نسخهٔ سبد سروری؛ با هر تغییر (مشتری یا ادمین) افزایش می‌یابد تا همگام‌سازی
    /// قدیمی کلاینت، تغییرات ادمین را بازنویسی نکند.
    /// </summary>
    public long CartRevision { get; set; }
}
