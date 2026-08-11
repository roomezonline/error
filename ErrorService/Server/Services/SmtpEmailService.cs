using System.Net;
using System.Net.Mail;

namespace ErrorService.Server.Services;

public sealed class SmtpEmailService : IEmailService
{
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(ILogger<SmtpEmailService> logger)
        => _logger = logger;

    public async Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        try
        {
            using var client = CreateClient();
            using var mail = new MailMessage
            {
                From = new MailAddress(
                    _smtpConfig.FromEmail ?? _smtpConfig.User,
                    _smtpConfig.FromName ?? "ErrorService"),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(to);

            await client.SendMailAsync(mail, ct);
            _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            return false;
        }
    }

    public async Task<bool> SendBackupNotificationAsync(
        string to, string fileName, long fileSizeBytes, bool isSuccess,
        string? errorMessage = null, CancellationToken ct = default)
    {
        var sizeFormatted = FormatFileSize(fileSizeBytes);
        var status = isSuccess ? "موفق" : "ناموفق";
        var color = isSuccess ? "#22c55e" : "#ef4444";
        var icon = isSuccess ? "&#10004;" : "&#10008;";

        var errorRow = isSuccess ? "" : $@"
                        <tr>
                            <td style=""padding: 8px 0; color: #64748b;"">خطا:</td>
                            <td style=""padding: 8px 0; color: #ef4444;"">{errorMessage}</td>
                        </tr>";

        var html = $@"<!DOCTYPE html>
<html dir=""rtl"" lang=""fa"">
<head><meta charset=""utf-8""></head>
<body style=""font-family: Tahoma, Arial, sans-serif; background: #f8fafc; padding: 20px;"">
    <div style=""max-width: 500px; margin: 0 auto; background: white; border-radius: 12px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.1);"">
        <div style=""background: {color}; color: white; padding: 20px; text-align: center; font-size: 24px;"">
            {icon} اعلان بک‌آپ
        </div>
        <div style=""padding: 24px;"">
            <h3 style=""color: #1e293b; margin: 0 0 16px;"">نتیجه بک‌آپ پایگاه داده</h3>
            <table style=""width: 100%; border-collapse: collapse;"">
                <tr>
                    <td style=""padding: 8px 0; color: #64748b;"">وضعیت:</td>
                    <td style=""padding: 8px 0; color: {color}; font-weight: bold;"">{status}</td>
                </tr>
                <tr>
                    <td style=""padding: 8px 0; color: #64748b;"">فایل:</td>
                    <td style=""padding: 8px 0; color: #1e293b;"">{fileName}</td>
                </tr>
                <tr>
                    <td style=""padding: 8px 0; color: #64748b;"">حجم:</td>
                    <td style=""padding: 8px 0; color: #1e293b;"">{sizeFormatted}</td>
                </tr>
                <tr>
                    <td style=""padding: 8px 0; color: #64748b;"">زمان:</td>
                    <td style=""padding: 8px 0; color: #1e293b;"">{DateTime.Now:yyyy/MM/dd HH:mm:ss}</td>
                </tr>
{errorRow}
            </table>
        </div>
        <div style=""background: #f1f5f9; padding: 12px; text-align: center; color: #94a3b8; font-size: 12px;"">
            ErrorService — سیستم مدیریت ارور
        </div>
    </div>
</body>
</html>";

        return await SendAsync(to, $"[{status}] بک‌آپ دیتابیس — {fileName}", html, ct);
    }

    private SmtpClient CreateClient()
    {
        var client = new SmtpClient(_smtpConfig.Host, _smtpConfig.Port)
        {
            Credentials = new NetworkCredential(_smtpConfig.User, _smtpConfig.Password),
            EnableSsl = _smtpConfig.UseSsl,
            Timeout = 30000
        };
        return client;
    }

    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1048576 => $"{bytes / 1024.0:F1} KB",
        < 1073741824 => $"{bytes / 1048576.0:F1} MB",
        _ => $"{bytes / 1073741824.0:F2} GB"
    };

    // Will be configured from DB at runtime
    internal SmtpConfig _smtpConfig = new();
    public void Configure(SmtpConfig config) => _smtpConfig = config;
}

public sealed class SmtpConfig
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public bool UseSsl { get; set; } = true;
    public string? FromName { get; set; } = "ErrorService Backup";
    public string? FromEmail { get; set; }
}
