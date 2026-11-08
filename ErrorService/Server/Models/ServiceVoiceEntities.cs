namespace ErrorService.Server.Models;

/// <summary>اکشن صوتی سامانه آوای ارورسرویس؛ فایل صدا در wwwroot/uploads/service-voices ذخیره می‌شود.</summary>
public sealed class ServiceVoiceAction
{
    public int Id { get; set; }
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
