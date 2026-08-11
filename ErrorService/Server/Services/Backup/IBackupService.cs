using ErrorService.Shared;

namespace ErrorService.Server.Services.Backup;

public interface IBackupService
{
    Task<BackupResult> CreateBackupAsync(bool isManual = false, string? note = null, CancellationToken ct = default);
    Task<RestoreResult> RestoreBackupAsync(string filePath, CancellationToken ct = default);
    Task<List<BackupHistoryItemDto>> GetHistoryAsync(int limit = 50, CancellationToken ct = default);
    Task<BackupSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task SaveSettingsAsync(BackupSettingsDto settings, CancellationToken ct = default);
    Task CleanupOldFilesAsync(CancellationToken ct = default);
    string GetBackupDirectory();
}
