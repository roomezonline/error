using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/sms-settings")]
[Authorize(Policy = "perm:admin.sms.manage")]
public sealed class SmsSettingsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public SmsSettingsController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<SmsSettingsDto>> Get()
    {
        var settings = await _db.SmsSettings.FirstOrDefaultAsync();
        if (settings == null)
        return Ok(new SmsSettingsDto
        {
            IsActive = true,
            OtpEnabled = true,
            TariffPerSms = 0,
            MonthlyQuota = 0,
            WarningThreshold = 0
        });

        return Ok(new SmsSettingsDto
        {
            IsActive = settings.IsActive,
            OtpEnabled = settings.OtpEnabled,
            TariffPerSms = settings.TariffPerSms,
            ApiKey = settings.ApiKey,
            SenderNumber = settings.SenderNumber,
            MonthlyQuota = settings.MonthlyQuota,
            WarningThreshold = settings.WarningThreshold,
            UpdatedAt = settings.UpdatedAt
        });
    }

    [HttpPut]
    public async Task<IActionResult> Update(SmsSettingsUpdateRequest request)
    {
        var settings = await _db.SmsSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SmsSettings();
            _db.SmsSettings.Add(settings);
        }

        settings.IsActive = request.IsActive;
        settings.OtpEnabled = request.OtpEnabled;
        settings.TariffPerSms = request.TariffPerSms;
        Console.WriteLine($"[SmsSettings] Saving: IsActive={request.IsActive}, OtpEnabled={request.OtpEnabled}, Tariff={request.TariffPerSms}, ApiKey={request.ApiKey}, Sender={request.SenderNumber}, Quota={request.MonthlyQuota}, Threshold={request.WarningThreshold}");
        settings.ApiKey = request.ApiKey;
        settings.SenderNumber = request.SenderNumber;
        settings.MonthlyQuota = request.MonthlyQuota;
        settings.WarningThreshold = request.WarningThreshold;
        settings.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
