namespace ErrorService.Server.Models;

public sealed class BackupSettings
{
    public int Id { get; set; }
    public bool IsAutoBackupEnabled { get; set; }
    public int FrequencyHours { get; set; } = 24;
    public string BackupPath { get; set; } = "backups";
    public int MaxFiles { get; set; } = 2;
    public bool EmailNotifyEnabled { get; set; }
    public string EmailTo { get; set; } = "";
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public bool SmtpUseSsl { get; set; } = true;
    public string? SmtpFromName { get; set; } = "ErrorService Backup";
    public string? SmtpFromEmail { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BackupHistory
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public bool IsManual { get; set; }
    public bool EmailSent { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
