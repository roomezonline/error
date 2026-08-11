using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services.Backup;

public sealed class SqlServerBackupService : IBackupService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SqlServerBackupService> _logger;

    public SqlServerBackupService(IServiceProvider services, ILogger<SqlServerBackupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public string GetBackupDirectory()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
        var settings = db.BackupSettings.AsNoTracking().FirstOrDefault();
        var path = settings?.BackupPath ?? "backups";
        if (!Path.IsPathRooted(path))
        {
            var baseDir = AppContext.BaseDirectory;
            path = Path.Combine(baseDir, path);
        }
        Directory.CreateDirectory(path);
        return path;
    }

    public async Task<BackupResult> CreateBackupAsync(bool isManual = false, string? note = null, CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        var backupDir = GetBackupDirectory();

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"ErrorService_{timestamp}.bak";
        var filePath = Path.Combine(backupDir, fileName);

        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
            var conn = db.Database.GetConnectionString();

            if (string.IsNullOrEmpty(conn))
                throw new InvalidOperationException("Database connection string is empty");

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(conn);
            await connection.OpenAsync(ct);

            var dbName = connection.Database;

            var backupQuery = $@"
                BACKUP DATABASE [{dbName}]
                TO DISK = @path
                WITH COMPRESSION, INIT, NAME = 'ErrorService Backup {timestamp}'";

            using var command = new Microsoft.Data.SqlClient.SqlCommand(backupQuery, connection);
            command.Parameters.AddWithValue("@path", filePath);
            command.CommandTimeout = 600;

            _logger.LogInformation("Starting backup to {Path}", filePath);
            await command.ExecuteNonQueryAsync(ct);

            var fileInfo = new FileInfo(filePath);
            var fileSize = fileInfo.Length;

            _logger.LogInformation("Backup completed: {File} ({Size} bytes)", fileName, fileSize);

            var history = new BackupHistory
            {
                FileName = fileName,
                FilePath = filePath,
                FileSizeBytes = fileSize,
                Status = "Success",
                IsManual = isManual,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.BackupHistories.Add(history);
            await db.SaveChangesAsync(ct);

            await CleanupOldFilesAsync(db, ct);

            return new BackupResult
            {
                Success = true,
                FileName = fileName,
                FilePath = filePath,
                FileSizeBytes = fileSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed");

            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
                db.BackupHistories.Add(new BackupHistory
                {
                    FileName = fileName,
                    FilePath = filePath,
                    FileSizeBytes = 0,
                    Status = "Failed",
                    ErrorMessage = ex.Message,
                    IsManual = isManual,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync(ct);
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to log backup failure");
            }

            return new BackupResult
            {
                Success = false,
                FileName = fileName,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<RestoreResult> RestoreBackupAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(filePath))
                return new RestoreResult { Success = false, ErrorMessage = "فایل بک‌آپ یافت نشد" };

            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
            var conn = db.Database.GetConnectionString();

            if (string.IsNullOrEmpty(conn))
                return new RestoreResult { Success = false, ErrorMessage = "ConnectionString خالی است" };

            using var connection = new Microsoft.Data.SqlClient.SqlConnection(conn);
            await connection.OpenAsync(ct);
            var dbName = connection.Database;

            var killQuery = $@"
                DECLARE @spid INT;
                DECLARE cur CURSOR FOR
                    SELECT session_id FROM sys.dm_exec_sessions
                    WHERE database_id = DB_ID('{dbName}') AND session_id <> @@SPID;
                OPEN cur;
                FETCH NEXT FROM cur INTO @spid;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    EXEC('KILL ' + @spid);
                    FETCH NEXT FROM cur INTO @spid;
                END;
                CLOSE cur;
                DEALLOCATE cur;";

            using (var killCmd = new Microsoft.Data.SqlClient.SqlCommand(killQuery, connection))
            {
                killCmd.CommandTimeout = 30;
                await killCmd.ExecuteNonQueryAsync(ct);
            }

            var restoreQuery = $@"
                ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                RESTORE DATABASE [{dbName}]
                FROM DISK = @path
                WITH REPLACE;
                ALTER DATABASE [{dbName}] SET MULTI_USER;";

            using var command = new Microsoft.Data.SqlClient.SqlCommand(restoreQuery, connection);
            command.Parameters.AddWithValue("@path", filePath);
            command.CommandTimeout = 600;

            _logger.LogWarning("Starting database restore from {Path}", filePath);
            await command.ExecuteNonQueryAsync(ct);
            _logger.LogWarning("Database restore completed");

            return new RestoreResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed");
            return new RestoreResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<List<BackupHistoryItemDto>> GetHistoryAsync(int limit = 50, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();

        return await db.BackupHistories
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new BackupHistoryItemDto
            {
                Id = x.Id,
                FileName = x.FileName,
                FileSizeBytes = x.FileSizeBytes,
                FileSizeFormatted = FormatFileSize(x.FileSizeBytes),
                Status = x.Status,
                ErrorMessage = x.ErrorMessage,
                CreatedAtFa = x.CreatedAt.ToString("yyyy/MM/dd HH:mm"),
                CreatedAtUtc = x.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                IsManual = x.IsManual,
                EmailSent = x.EmailSent
            })
            .ToListAsync(ct);
    }

    public async Task<BackupSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();

        var settings = await db.BackupSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var lastBackup = await db.BackupHistories
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var totalCount = await db.BackupHistories.CountAsync(ct);

        if (settings == null)
            return new BackupSettingsDto
            {
                TotalBackups = totalCount,
                LastBackupAt = lastBackup?.CreatedAt.ToString("yyyy/MM/dd HH:mm")
            };

        return new BackupSettingsDto
        {
            IsAutoBackupEnabled = settings.IsAutoBackupEnabled,
            FrequencyHours = settings.FrequencyHours,
            BackupPath = settings.BackupPath,
            MaxFiles = settings.MaxFiles,
            EmailNotifyEnabled = settings.EmailNotifyEnabled,
            EmailTo = settings.EmailTo,
            SmtpHost = settings.SmtpHost,
            SmtpPort = settings.SmtpPort,
            SmtpUser = settings.SmtpUser,
            SmtpPassword = string.IsNullOrEmpty(settings.SmtpPassword) ? "" : "••••••••",
            SmtpUseSsl = settings.SmtpUseSsl,
            SmtpFromName = settings.SmtpFromName,
            SmtpFromEmail = settings.SmtpFromEmail,
            LastBackupAt = lastBackup?.CreatedAt.ToString("yyyy/MM/dd HH:mm"),
            TotalBackups = totalCount
        };
    }

    public async Task SaveSettingsAsync(BackupSettingsDto dto, CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();

        var settings = await db.BackupSettings.FirstOrDefaultAsync(ct);
        if (settings == null)
        {
            settings = new BackupSettings();
            db.BackupSettings.Add(settings);
        }

        settings.IsAutoBackupEnabled = dto.IsAutoBackupEnabled;
        settings.FrequencyHours = dto.FrequencyHours;
        settings.BackupPath = dto.BackupPath;
        settings.MaxFiles = dto.MaxFiles;
        settings.EmailNotifyEnabled = dto.EmailNotifyEnabled;
        settings.EmailTo = dto.EmailTo;
        settings.SmtpHost = dto.SmtpHost;
        settings.SmtpPort = dto.SmtpPort;
        settings.SmtpUser = dto.SmtpUser;
        if (!string.IsNullOrEmpty(dto.SmtpPassword) && dto.SmtpPassword != "••••••••")
            settings.SmtpPassword = dto.SmtpPassword;
        settings.SmtpUseSsl = dto.SmtpUseSsl;
        settings.SmtpFromName = dto.SmtpFromName;
        settings.SmtpFromEmail = dto.SmtpFromEmail;
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task CleanupOldFilesAsync(CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
        await CleanupOldFilesAsync(db, ct);
    }

    private async Task CleanupOldFilesAsync(ErrorServiceDbContext db, CancellationToken ct)
    {
        var settings = await db.BackupSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var maxFiles = settings?.MaxFiles ?? 2;

        var oldFiles = await db.BackupHistories
            .AsNoTracking()
            .Where(x => x.Status == "Success")
            .OrderByDescending(x => x.CreatedAt)
            .Skip(maxFiles)
            .ToListAsync(ct);

        foreach (var file in oldFiles)
        {
            try
            {
                if (File.Exists(file.FilePath))
                    File.Delete(file.FilePath);
                _logger.LogInformation("Deleted old backup: {File}", file.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old backup: {File}", file.FileName);
            }
        }
    }

    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1048576 => $"{bytes / 1024.0:F1} KB",
        < 1073741824 => $"{bytes / 1048576.0:F1} MB",
        _ => $"{bytes / 1073741824.0:F2} GB"
    };

    public async Task<SmtpConfig> GetSmtpConfigAsync(CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
        var s = await db.BackupSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        return new SmtpConfig
        {
            Host = s?.SmtpHost ?? "smtp.gmail.com",
            Port = s?.SmtpPort ?? 587,
            User = s?.SmtpUser ?? "",
            Password = s?.SmtpPassword ?? "",
            UseSsl = s?.SmtpUseSsl ?? true,
            FromName = s?.SmtpFromName ?? "ErrorService Backup",
            FromEmail = s?.SmtpFromEmail
        };
    }
}
