using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Server.Services.Backup;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services.Backup;

public sealed class BackupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BackupBackgroundService> _logger;

    public BackupBackgroundService(IServiceProvider services, ILogger<BackupBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BackupBackgroundService starting");
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackupBackgroundService is running");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var shouldBackup = await ShouldBackupNowAsync(stoppingToken);
                if (shouldBackup)
                {
                    await RunBackupAsync(stoppingToken);
                }

                var interval = await GetCheckIntervalAsync(stoppingToken);
                _ = await WaitForNextCheckAsync(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in backup schedule check");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task<bool> ShouldBackupNowAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();

        var settings = await db.BackupSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings == null || !settings.IsAutoBackupEnabled)
            return false;

        var lastBackup = await db.BackupHistories
            .AsNoTracking()
            .Where(x => x.Status == "Success")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (lastBackup == null)
            return true;

        var elapsed = DateTimeOffset.UtcNow - lastBackup.CreatedAt;
        return elapsed.TotalHours >= settings.FrequencyHours;
    }

    private async Task RunBackupAsync(CancellationToken ct)
    {
        _logger.LogInformation("Running scheduled backup");

        BackupSettingsDto settings;
        BackupResult result;
        SmtpConfig? smtpConfig = null;

        using (var scope = _services.CreateScope())
        {
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            settings = await backupService.GetSettingsAsync(ct);
            smtpConfig = await ((SqlServerBackupService)backupService).GetSmtpConfigAsync(ct);
            result = await backupService.CreateBackupAsync(isManual: false, ct: ct);
        }

        if (settings.EmailNotifyEnabled && !string.IsNullOrEmpty(settings.EmailTo) && smtpConfig != null)
        {
            try
            {
                using var scope = _services.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                ((SmtpEmailService)emailService).Configure(smtpConfig);
                await emailService.SendBackupNotificationAsync(
                    settings.EmailTo,
                    result.FileName ?? "unknown",
                    result.FileSizeBytes,
                    result.Success,
                    result.ErrorMessage,
                    ct);

                using var dbScope = _services.CreateScope();
                var db = dbScope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
                var history = await db.BackupHistories
                    .Where(x => x.FileName == result.FileName)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync(ct);
                if (history != null)
                {
                    history.EmailSent = true;
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send backup notification email");
            }
        }
    }

    private async Task<TimeSpan> GetCheckIntervalAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
        var settings = await db.BackupSettings.AsNoTracking().FirstOrDefaultAsync(ct);

        if (settings == null || !settings.IsAutoBackupEnabled)
            return TimeSpan.FromHours(1);

        return TimeSpan.FromMinutes(Math.Max(10, settings.FrequencyHours * 60 / 6));
    }

    private async Task<bool> WaitForNextCheckAsync(TimeSpan interval, CancellationToken ct)
    {
        try
        {
            await Task.Delay(interval, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BackupBackgroundService is stopping");
        return base.StopAsync(cancellationToken);
    }
}
