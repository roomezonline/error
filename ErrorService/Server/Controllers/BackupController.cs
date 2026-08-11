using ErrorService.Server.Services.Backup;
using ErrorService.Server.Services;
using ErrorService.Shared;
using ErrorService.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/backup")]
public class BackupController : ControllerBase
{
    private readonly IBackupService _backupService;
    private readonly IEmailService _emailService;
    private readonly ILogger<BackupController> _logger;

    public BackupController(IBackupService backupService, IEmailService emailService, ILogger<BackupController> logger)
    {
        _backupService = backupService;
        _emailService = emailService;
        _logger = logger;
    }

    [Authorize(Policy = "perm:admin.backup.view")]
    [HttpGet("info")]
    public async Task<ActionResult<BackupInfoDto>> GetInfo(CancellationToken ct)
    {
        var settings = await _backupService.GetSettingsAsync(ct);
        var history = await _backupService.GetHistoryAsync(20, ct);
        return Ok(new BackupInfoDto
        {
            Settings = settings,
            RecentHistory = history,
            IsServiceRunning = true
        });
    }

    [Authorize(Policy = "perm:admin.backup.view")]
    [HttpGet("history")]
    public async Task<ActionResult<List<BackupHistoryItemDto>>> GetHistory(
        [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        return Ok(await _backupService.GetHistoryAsync(limit, ct));
    }

    [Authorize(Policy = "perm:admin.backup.settings")]
    [HttpGet("settings")]
    public async Task<ActionResult<BackupSettingsDto>> GetSettings(CancellationToken ct)
        => Ok(await _backupService.GetSettingsAsync(ct));

    [Authorize(Policy = "perm:admin.backup.settings")]
    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] BackupSettingsDto dto, CancellationToken ct)
    {
        await _backupService.SaveSettingsAsync(dto, ct);
        return Ok(new { message = "تنظیمات ذخیره شد" });
    }

    [Authorize(Policy = "perm:admin.backup.create")]
    [HttpPost("create")]
    public async Task<ActionResult<BackupResult>> CreateBackup(
        [FromBody] BackupCreateRequest? request = null, CancellationToken ct = default)
    {
        var result = await _backupService.CreateBackupAsync(
            isManual: true,
            note: request?.Note,
            ct: ct);

        if (result.Success)
        {
            try
            {
                var settings = await _backupService.GetSettingsAsync(ct);
                if (settings.EmailNotifyEnabled && !string.IsNullOrEmpty(settings.EmailTo))
                {
                    using var scope = HttpContext.RequestServices.CreateScope();
                    var smtpService = scope.ServiceProvider.GetRequiredService<SqlServerBackupService>();
                    var smtpConfig = await smtpService.GetSmtpConfigAsync(ct);
                    ((SmtpEmailService)_emailService).Configure(smtpConfig);
                    await _emailService.SendBackupNotificationAsync(
                        settings.EmailTo,
                        result.FileName!,
                        result.FileSizeBytes,
                        true,
                        ct: ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send manual backup email");
            }
        }

        return Ok(result);
    }

    [Authorize(Policy = "perm:admin.backup.restore")]
    [HttpPost("restore")]
    public async Task<ActionResult<RestoreResult>> RestoreBackup(
        [FromBody] BackupRestoreRequest request, CancellationToken ct)
    {
        if (!request.Confirm)
            return BadRequest(new RestoreResult { Success = false, ErrorMessage = "تایید بازیابی ضروری است" });

        using var scope = HttpContext.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Data.ErrorServiceDbContext>();
        var history = await db.BackupHistories.FindAsync(new object[] { request.HistoryId }, ct);

        if (history == null)
            return NotFound(new RestoreResult { Success = false, ErrorMessage = "رکورد بک‌آپ یافت نشد" });

        if (history.Status != "Success")
            return BadRequest(new RestoreResult { Success = false, ErrorMessage = "فقط بک‌آپ‌های موفق قابل بازیابی هستند" });

        var result = await _backupService.RestoreBackupAsync(history.FilePath, ct);
        return Ok(result);
    }

    [Authorize(Policy = "perm:admin.backup.create")]
    [HttpPost("cleanup")]
    public async Task<IActionResult> Cleanup(CancellationToken ct)
    {
        await _backupService.CleanupOldFilesAsync(ct);
        return Ok(new { message = "پاکسازی انجام شد" });
    }
}
