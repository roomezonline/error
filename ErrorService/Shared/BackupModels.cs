namespace ErrorService.Shared;

public sealed class BackupSettingsDto
{
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
    public string? LastBackupAt { get; set; }
    public int TotalBackups { get; set; }
}

public sealed class BackupHistoryItemDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public string FileSizeFormatted { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public string? CreatedAtFa { get; set; }
    public string? CreatedAtUtc { get; set; }
    public bool IsManual { get; set; }
    public bool EmailSent { get; set; }
}

public sealed class BackupInfoDto
{
    public BackupSettingsDto Settings { get; set; } = new();
    public List<BackupHistoryItemDto> RecentHistory { get; set; } = new();
    public bool IsServiceRunning { get; set; }
    public string? NextScheduledAt { get; set; }
}

public sealed class BackupCreateRequest
{
    public string? Note { get; set; }
}

public sealed class BackupRestoreRequest
{
    public int HistoryId { get; set; }
    public bool Confirm { get; set; }
}

public sealed class BackupResult
{
    public bool Success { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class RestoreResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
