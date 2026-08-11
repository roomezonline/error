namespace ErrorService.Server.Services;

public interface IEmailService
{
    Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    Task<bool> SendBackupNotificationAsync(string to, string fileName, long fileSizeBytes, bool isSuccess, string? errorMessage = null, CancellationToken ct = default);
}
