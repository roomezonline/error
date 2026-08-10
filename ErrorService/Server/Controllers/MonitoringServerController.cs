using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using ErrorService.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/monitoring/server")]
public class MonitoringServerController : ControllerBase
{
    private readonly MonitoringCacheService _cache;
    private readonly ErrorServiceDbContext _db;
    private readonly ILogger<MonitoringServerController> _logger;

    public MonitoringServerController(MonitoringCacheService cache, ErrorServiceDbContext db, ILogger<MonitoringServerController> logger)
    {
        _cache = cache;
        _db = db;
        _logger = logger;
    }

    [HttpPost("command")]
    public async Task<IActionResult> SendCommand([FromBody] SendCommandRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest("code is required");

        if (request.Value != "on" && request.Value != "off")
            return BadRequest("value must be 'on' or 'off'");

        var deviceCode = request.Code;
        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitoring-data");
        var commandDir = Path.Combine(baseDir, deviceCode, "command");
        Directory.CreateDirectory(commandDir);
        var commandFile = Path.Combine(commandDir, $"device_{deviceCode}.txt");

        var cachedData = _cache.Get(deviceCode);
        string currentTypeNum = "1";
        string motorTypeNum = "1";

        if (!string.IsNullOrEmpty(request.SensorType))
        {
            currentTypeNum = request.SensorType.ToLower() switch
            {
                "loop" or "circle" => "1",
                "clamp" => "2",
                _ => "1"
            };
        }
        else if (cachedData?.CurrentType != null)
        {
            currentTypeNum = cachedData.CurrentType.ToLower() switch
            {
                "loop" or "circle" => "1",
                "clamp" => "2",
                _ => "2"
            };
        }

        if (!string.IsNullOrEmpty(request.MotorType))
        {
            motorTypeNum = request.MotorType.ToLower() switch
            {
                "simple" => "1",
                "inverter" => "2",
                _ => "1"
            };
        }
        else if (cachedData?.Inverter != null)
        {
            motorTypeNum = cachedData.Inverter.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ? "2" : "1";
        }

        var now = DateTime.Now;
        var cal = new PersianCalendar();
        var y = cal.GetYear(now);
        var m = cal.GetMonth(now);
        var d = cal.GetDayOfMonth(now);
        var persianNow = $"{y:0000}/{m:00}/{d:00} {now:HH:mm:ss}";

        var content = $"{request.Value},{currentTypeNum},{motorTypeNum},{persianNow}";
        await System.IO.File.WriteAllTextAsync(commandFile, content, Encoding.UTF8);

        return Ok(new { message = "ok" });
    }

    [HttpPost("setting")]
    public async Task<IActionResult> SaveSetting([FromBody] SaveSettingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest("code is required");

        var deviceCode = request.Code;
        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitoring-data");
        var settingDir = Path.Combine(baseDir, deviceCode, "setting");
        Directory.CreateDirectory(settingDir);
        var settingFile = Path.Combine(settingDir, $"device_{deviceCode}.txt");

        var content = $"{request.MotorMax},{request.MotorMin},{request.ElementMax},{request.ElementMin},{request.Simultaneity.ToString().ToLower()},{request.TimeoutMinutes},{request.AmbientTempAlarmEnabled.ToString().ToLower()},{request.AmbientTempThreshold}";
        await System.IO.File.WriteAllTextAsync(settingFile, content, Encoding.UTF8);

        return Ok(new { message = "ok" });
    }

    [HttpGet("setting")]
    public async Task<IActionResult> GetSetting([FromQuery] string code, [FromQuery] bool raw = false)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("code is required");

        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitoring-data");
        var settingFile = Path.Combine(baseDir, code, "setting", $"device_{code}.txt");

        if (!System.IO.File.Exists(settingFile))
        {
            var defaultContent = "5,0,5,0,true,2,true,35";
            if (raw) return Content(defaultContent, "text/plain");
            return Ok(new { exists = false, motorMax = 5, motorMin = 0, elementMax = 5, elementMin = 0, simultaneity = true, timeoutMinutes = 2, ambientTempAlarmEnabled = true, ambientTempThreshold = 35 });
        }

        var content = (await System.IO.File.ReadAllTextAsync(settingFile, Encoding.UTF8)).Trim();

        if (raw)
            return Content(content, "text/plain");

        var parts = content.Split(',', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 5)
        {
            if (raw) return Content("5,0,5,0,true,2,true,35", "text/plain");
            return Ok(new { exists = false, motorMax = 5, motorMin = 0, elementMax = 5, elementMin = 0, simultaneity = true, timeoutMinutes = 2, ambientTempAlarmEnabled = true, ambientTempThreshold = 35 });
        }

        int.TryParse(parts[0], out var motorMax);
        int.TryParse(parts[1], out var motorMin);
        int.TryParse(parts[2], out var elementMax);
        int.TryParse(parts[3], out var elementMin);
        var simultaneity = parts[4].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        var timeoutMinutes = 2;
        if (parts.Length >= 6) int.TryParse(parts[5], out timeoutMinutes);
        if (timeoutMinutes < 1) timeoutMinutes = 1;

        var ambientTempAlarmEnabled = true;
        if (parts.Length >= 7) bool.TryParse(parts[6].Trim(), out ambientTempAlarmEnabled);
        var ambientTempThreshold = 35;
        if (parts.Length >= 8) int.TryParse(parts[7], out ambientTempThreshold);
        if (ambientTempThreshold < 30) ambientTempThreshold = 30;
        if (ambientTempThreshold > 50) ambientTempThreshold = 50;

        return Ok(new { exists = true, motorMax, motorMin, elementMax, elementMin, simultaneity, timeoutMinutes, ambientTempAlarmEnabled, ambientTempThreshold });
    }

    [HttpGet("get_last_power")]
    public IActionResult GetLastPower([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("code is required");

        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitoring-data");
        var filePath = Path.Combine(baseDir, code, "server", $"device_{code}.txt");
        var priz = "off";
        var sensor = "1";
        var motor = "1";
        long unix = 0;

        var data = _cache.Get(code);
        if (data != null)
        {
            priz = data.Priz;
            sensor = data.CurrentType == "clamp" ? "2" : "1";
            motor = data.Inverter.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ? "2" : "1";
        }

        if (System.IO.File.Exists(filePath))
        {
            var lines = System.IO.File.ReadAllLines(filePath, Encoding.UTF8);
            if (data == null)
            {
                priz = ParseLine(lines, "priz") ?? "off";
                var currentType = ParseLine(lines, "current_type") ?? "";
                var inverter = ParseLine(lines, "inverter") ?? "";
                sensor = currentType == "clamp" ? "2" : "1";
                motor = inverter.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ? "2" : "1";
            }
            var ts = ParseLine(lines, "timestamp");
            if (!string.IsNullOrWhiteSpace(ts) &&
                DateTime.TryParseExact(ts, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            {
                unix = new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt)).ToUnixTimeSeconds();
            }

            // اگر دستگاه در بازه تایم‌اوت مجدداً متصل شده، مقدار priz رو off کن
            var timeout = GetTimeoutMinutes(code);
            var lastWrite = System.IO.File.GetLastWriteTimeUtc(filePath);
            if ((DateTime.UtcNow - lastWrite).TotalMinutes < timeout)
                priz = "off";
        }

        var lastTime = unix > 0 ? PersianDateHelper.ToPersianDateTimeString(DateTimeOffset.FromUnixTimeSeconds(unix), true) : "-";

        return Content($"{priz},{sensor},{motor},{lastTime}", "text/plain");
    }

    private int GetTimeoutMinutes(string code)
    {
        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitoring-data");
        var settingFile = Path.Combine(baseDir, code, "setting", $"device_{code}.txt");
        if (!System.IO.File.Exists(settingFile)) return 2;
        var content = System.IO.File.ReadAllText(settingFile, Encoding.UTF8).Trim();
        var parts = content.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 6) return 2;
        if (int.TryParse(parts[5], out var t) && t >= 1) return t;
        return 2;
    }

    private static string? ParseLine(string[] lines, string key)
    {
        var prefix = key + "=";
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return trimmed[prefix.Length..];
        }
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? code,
        [FromQuery] string? priz,
        [FromQuery] string? motor_state,
        [FromQuery] string? element1,
        [FromQuery] string? element2,
        [FromQuery] string? temp1,
        [FromQuery] string? temp2,
        [FromQuery] string? jaryan,
        [FromQuery] string? cnt_m,
        [FromQuery] string? cnt_e1,
        [FromQuery] string? cnt_e2,
        [FromQuery] string? temp3,
        [FromQuery] string? current_type,
        [FromQuery] string? inverter,
        [FromQuery] string? voltage,
        [FromQuery] string? tavan,
        [FromQuery] string? power,
        [FromQuery] string? freq,
        [FromQuery] string? dc1,
        [FromQuery] string? dc2,
        [FromQuery] string? ac1,
        [FromQuery] string? ac2,
        [FromQuery] string? device_type,
        [FromQuery] string? alarm,
        [FromQuery] string? type_alarm,
        [FromQuery] string? max_jaryan,
        [FromQuery] string? had)
    {
        var resolvedPower = tavan ?? power;
        try
        {
            var deviceCode = code ?? "0";
            var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitoring-data");

            // Resolve active monitoring connection
            int? monitoringId = null;
            int? customerReceiptId = null;
            int? workshopId = null;
            bool pendingOff = false;
            var deviceEntity = await _db.MonitoringDevices
                .FirstOrDefaultAsync(d => d.DeviceNumber == deviceCode);
            if (deviceEntity != null)
            {
                var connection = await _db.MonitoringReceiptConnections
                    .Include(c => c.MonitoringDevice)
                    .Where(c => c.MonitoringDeviceId == deviceEntity.Id && c.EndedAt == null)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();
                if (connection != null)
                {
                    var elapsed = DateTimeOffset.UtcNow - connection.CreatedAt;
                    if (elapsed.TotalHours >= 72 && connection.EndedAt == null)
                    {
                        var expiredCmdDir = Path.Combine(baseDir, deviceCode, "command");
                        Directory.CreateDirectory(expiredCmdDir);
                        var expiredCmdFile = Path.Combine(expiredCmdDir, $"device_{deviceCode}.txt");

                        var currentCmd = System.IO.File.Exists(expiredCmdFile)
                            ? (await System.IO.File.ReadAllTextAsync(expiredCmdFile)).Trim()
                            : "";

                        if (currentCmd != "off")
                        {
                            await System.IO.File.WriteAllTextAsync(expiredCmdFile, "off", Encoding.UTF8);
                            pendingOff = true;
                        }
                        else
                        {
                            var deviceOff = string.IsNullOrEmpty(priz) || !IsOn(priz);
                            var cmdWriteTime = System.IO.File.GetLastWriteTimeUtc(expiredCmdFile);
                            var forceEnd = (DateTime.UtcNow - cmdWriteTime).TotalMinutes >= 5;

                            if (deviceOff || forceEnd)
                            {
                                connection.EndedAt = DateTimeOffset.UtcNow;
                                connection.EndReason = deviceOff
                                    ? "پایان خودکار مانیتورینگ پس از ۷۲ ساعت"
                                    : "پایان خودکار مانیتورینگ پس از ۷۲ ساعت (قطع اجباری - دستگاه خاموش نشد)";
                                await _db.SaveChangesAsync();
                                await CreateArchiveForConnection(connection.Id, connection.WorkshopId, connection.CustomerReceiptId, deviceCode, connection.CreatedAt);
                            }
                            else
                            {
                                pendingOff = true;
                            }
                        }
                    }

                    // Grace period check: if assignment expired, give 24h then auto-off
                    var activeAssignment = await _db.MonitoringDeviceAssignments
                        .Where(a => a.MonitoringDeviceId == deviceEntity.Id
                                 && a.WorkshopId == connection.WorkshopId
                                 && a.StartAt <= DateTimeOffset.UtcNow
                                 && (a.EndAt == null || a.EndAt > DateTimeOffset.UtcNow))
                        .OrderByDescending(a => a.StartAt)
                        .FirstOrDefaultAsync();
                    if (activeAssignment == null)
                    {
                        activeAssignment = await _db.MonitoringDeviceAssignments
                            .Where(a => a.MonitoringDeviceId == deviceEntity.Id
                                     && a.WorkshopId == connection.WorkshopId
                                     && a.EndAt != null && a.EndAt <= DateTimeOffset.UtcNow)
                            .OrderByDescending(a => a.StartAt)
                            .FirstOrDefaultAsync();
                    }
                    if (activeAssignment?.EndAt != null && activeAssignment.EndAt < DateTimeOffset.UtcNow)
                    {
                        if (connection.GracePeriodEndAt == null)
                        {
                            connection.GracePeriodEndAt = DateTimeOffset.UtcNow.AddHours(24);
                            await _db.SaveChangesAsync();
                        }
                        else if (DateTimeOffset.UtcNow >= connection.GracePeriodEndAt)
                        {
                            var graceCmdDir = Path.Combine(baseDir, deviceCode, "command");
                            Directory.CreateDirectory(graceCmdDir);
                            var graceCmdFile = Path.Combine(graceCmdDir, $"device_{deviceCode}.txt");

                            var currentGraceCmd = System.IO.File.Exists(graceCmdFile)
                                ? (await System.IO.File.ReadAllTextAsync(graceCmdFile)).Trim()
                                : "";

                            if (currentGraceCmd != "off")
                            {
                                await System.IO.File.WriteAllTextAsync(graceCmdFile, "off", Encoding.UTF8);
                                pendingOff = true;
                            }
                            else
                            {
                                var deviceOff = string.IsNullOrEmpty(priz) || !IsOn(priz);
                                var cmdWriteTime = System.IO.File.GetLastWriteTimeUtc(graceCmdFile);
                                var forceEnd = (DateTime.UtcNow - cmdWriteTime).TotalMinutes >= 5;

                                if (deviceOff || forceEnd)
                                {
                                    connection.EndedAt = DateTimeOffset.UtcNow;
                                    connection.EndReason = deviceOff
                                        ? "پایان خودکار مانیتورینگ پس از اتمام مهلت ۲۴ ساعته (دستگاه خاموش شد)"
                                        : "پایان خودکار مانیتورینگ پس از اتمام مهلت ۲۴ ساعته (قطع اجباری - دستگاه خاموش نشد)";
                                    await _db.SaveChangesAsync();
                                    await CreateArchiveForConnection(connection.Id, connection.WorkshopId, connection.CustomerReceiptId, deviceCode, connection.CreatedAt);
                                }
                                else
                                {
                                    pendingOff = true;
                                }
                            }
                        }
                    }

                    monitoringId = connection.Id;
                    customerReceiptId = connection.CustomerReceiptId;
                    workshopId = connection.WorkshopId;
                }
            }

            if (monitoringId == null)
                return NotFound("no_active_monitoring");

            // Save to file
            var deviceDir = Path.Combine(baseDir, deviceCode, "server");
            var commandDir = Path.Combine(baseDir, deviceCode, "command");
            Directory.CreateDirectory(deviceDir);
            Directory.CreateDirectory(commandDir);

            var fileName = $"device_{deviceCode}.txt";
            var filePath = Path.Combine(deviceDir, fileName);

            var sb = new StringBuilder();
            sb.AppendLine($"code={code}");
            sb.AppendLine($"priz={priz}");
            sb.AppendLine($"motor_state={motor_state}");
            sb.AppendLine($"element1={element1}");
            sb.AppendLine($"element2={element2}");
            sb.AppendLine($"temp1={temp1}");
            sb.AppendLine($"temp2={temp2}");
            sb.AppendLine($"jaryan={jaryan}");
            sb.AppendLine($"cnt_m={cnt_m}");
            sb.AppendLine($"cnt_e1={cnt_e1}");
            sb.AppendLine($"cnt_e2={cnt_e2}");
            sb.AppendLine($"temp3={temp3}");
            sb.AppendLine($"current_type={current_type}");
            sb.AppendLine($"inverter={inverter}");
            sb.AppendLine($"voltage={voltage}");
            sb.AppendLine($"tavan={resolvedPower}");
            sb.AppendLine($"freq={freq}");
            sb.AppendLine($"dc1={dc1}");
            sb.AppendLine($"dc2={dc2}");
            sb.AppendLine($"ac1={ac1}");
            sb.AppendLine($"ac2={ac2}");
            sb.AppendLine($"device_type={device_type}");
            sb.AppendLine($"alarm={alarm}");
            sb.AppendLine($"type_alarm={type_alarm}");

            // Detect element on/off transitions and track start/stop temps
            var prevElement1 = "0";
            var prevElement2 = "0";
            var prevE1StartFridge = "";
            var prevE1StopFridge = "";
            var prevE1StartFreezer = "";
            var prevE1StopFreezer = "";
            var prevE2StartFridge = "";
            var prevE2StopFridge = "";
            var prevE2StartFreezer = "";
            var prevE2StopFreezer = "";
            if (System.IO.File.Exists(filePath))
            {
                var oldLines = await System.IO.File.ReadAllLinesAsync(filePath, Encoding.UTF8);
                foreach (var ol in oldLines)
                {
                    var ot = ol.Trim();
                    var oi = ot.IndexOf('=');
                    if (oi < 0) continue;
                    var ok = ot[..oi].Trim().ToLowerInvariant();
                    var ov = ot[(oi + 1)..].Trim();
                    if (ok == "element1") prevElement1 = ov;
                    else if (ok == "element2") prevElement2 = ov;
                    else if (ok == "e1_start_fridge") prevE1StartFridge = ov;
                    else if (ok == "e1_stop_fridge") prevE1StopFridge = ov;
                    else if (ok == "e1_start_freezer") prevE1StartFreezer = ov;
                    else if (ok == "e1_stop_freezer") prevE1StopFreezer = ov;
                    else if (ok == "e2_start_fridge") prevE2StartFridge = ov;
                    else if (ok == "e2_stop_fridge") prevE2StopFridge = ov;
                    else if (ok == "e2_start_freezer") prevE2StartFreezer = ov;
                    else if (ok == "e2_stop_freezer") prevE2StopFreezer = ov;
                }
            }

            var curE1 = element1 ?? "0";
            var curE2 = element2 ?? "0";

            var e1TurnedOn = IsOn(curE1) && !IsOn(prevElement1);
            var e1TurnedOff = !IsOn(curE1) && IsOn(prevElement1);
            var e2TurnedOn = IsOn(curE2) && !IsOn(prevElement2);
            var e2TurnedOff = !IsOn(curE2) && IsOn(prevElement2);

            var e1StartFridge = e1TurnedOn ? (temp1 ?? "") : prevE1StartFridge;
            var e1StopFridge = e1TurnedOff ? (temp1 ?? "") : e1TurnedOn ? "" : prevE1StopFridge;
            var e1StartFreezer = e1TurnedOn ? (temp2 ?? "") : prevE1StartFreezer;
            var e1StopFreezer = e1TurnedOff ? (temp2 ?? "") : e1TurnedOn ? "" : prevE1StopFreezer;

            var e2StartFridge = e2TurnedOn ? (temp1 ?? "") : prevE2StartFridge;
            var e2StopFridge = e2TurnedOff ? (temp1 ?? "") : e2TurnedOn ? "" : prevE2StopFridge;
            var e2StartFreezer = e2TurnedOn ? (temp2 ?? "") : prevE2StartFreezer;
            var e2StopFreezer = e2TurnedOff ? (temp2 ?? "") : e2TurnedOn ? "" : prevE2StopFreezer;

            sb.AppendLine($"e1_start_fridge={e1StartFridge}");
            sb.AppendLine($"e1_stop_fridge={e1StopFridge}");
            sb.AppendLine($"e1_start_freezer={e1StartFreezer}");
            sb.AppendLine($"e1_stop_freezer={e1StopFreezer}");
            sb.AppendLine($"e2_start_fridge={e2StartFridge}");
            sb.AppendLine($"e2_stop_fridge={e2StopFridge}");
            sb.AppendLine($"e2_start_freezer={e2StartFreezer}");
            sb.AppendLine($"e2_stop_freezer={e2StopFreezer}");

            sb.AppendLine($"timestamp={DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"timestamp_persian={PersianDateHelper.ToPersianDateTimeString(DateTime.Now, true)}");

            await System.IO.File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);

            // Save to cache
            var data = new MonitoringDeviceData
            {
                Code = deviceCode,
                Priz = priz ?? "",
                MotorState = motor_state ?? "",
                Element1 = element1 ?? "",
                Element2 = element2 ?? "",
                Temp1 = temp1 ?? "",
                Temp2 = temp2 ?? "",
                Jaryan = jaryan ?? "",
                CntM = cnt_m ?? "",
                CntE1 = cnt_e1 ?? "",
                CntE2 = cnt_e2 ?? "",
                Temp3 = temp3 ?? "",
                CurrentType = current_type ?? "",
                Inverter = inverter ?? "",
                Voltage = voltage ?? "",
                Tavan = resolvedPower ?? "",
                Freq = freq ?? "",
                Dc1 = dc1 ?? "",
                Dc2 = dc2 ?? "",
                Ac1 = ac1 ?? "",
                Ac2 = ac2 ?? "",
                DeviceType = device_type ?? "",
                E1StartFridge = e1StartFridge,
                E1StopFridge = e1StopFridge,
                E1StartFreezer = e1StartFreezer,
                E1StopFreezer = e1StopFreezer,
                E2StartFridge = e2StartFridge,
                E2StopFridge = e2StopFridge,
                E2StartFreezer = e2StartFreezer,
                E2StopFreezer = e2StopFreezer,
                Timestamp = DateTime.UtcNow,
                TimestampFa = PersianDateHelper.ToPersianDateTimeString(DateTime.Now, true)
            };
            var oldData = _cache.Exchange(deviceCode, data);
            await SaveChangeLogIfNeeded(deviceCode, data, oldData, monitoringId.Value, customerReceiptId, workshopId!.Value);

            await SaveDataRecordAsync(deviceCode, monitoringId.Value, customerReceiptId, priz, motor_state, element1, element2,
                temp1, temp2, temp3, jaryan, resolvedPower, dc1, ac1);

            if (!string.IsNullOrEmpty(alarm) && (alarm.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) || IsOn(alarm)) && !string.IsNullOrEmpty(type_alarm))
            {
                await SaveAlertAsync(deviceCode, type_alarm!, max_jaryan, had, monitoringId.Value, customerReceiptId);
            }

            // Ambient temperature auto-shutdown check
            var ambientSettingDir = Path.Combine(baseDir, deviceCode, "setting");
            var ambientSettingFile = Path.Combine(ambientSettingDir, $"device_{deviceCode}.txt");
            bool ambientEnabled = true;
            int ambientThreshold = 35;
            if (System.IO.File.Exists(ambientSettingFile))
            {
                var ambientContent = await System.IO.File.ReadAllTextAsync(ambientSettingFile, Encoding.UTF8);
                var ambientParts = ambientContent.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (ambientParts.Length >= 7)
                    bool.TryParse(ambientParts[6].Trim(), out ambientEnabled);
                if (ambientParts.Length >= 8)
                    int.TryParse(ambientParts[7], out ambientThreshold);
            }
            if (ambientThreshold < 30) ambientThreshold = 30;
            if (ambientThreshold > 50) ambientThreshold = 50;

            if (ambientEnabled && !string.IsNullOrEmpty(temp3) && float.TryParse(temp3, NumberStyles.Float, CultureInfo.InvariantCulture, out var ambientCelsius))
            {
                var isHot = ambientCelsius >= ambientThreshold;
                var deviceIsOn = !string.IsNullOrEmpty(priz) && IsOn(priz);

                if (isHot)
                {
                    // Alert only on crossing the threshold (from <threshold to >=threshold)
                    float? oldAmbient = null;
                    if (oldData != null && !string.IsNullOrEmpty(oldData.Temp3) &&
                        float.TryParse(oldData.Temp3, NumberStyles.Float, CultureInfo.InvariantCulture, out var oldTemp))
                    {
                        oldAmbient = oldTemp;
                    }
                    if (oldAmbient == null || oldAmbient < ambientThreshold)
                    {
                        await SaveAmbientTempAlertAsync(deviceCode, monitoringId.Value, customerReceiptId, ambientCelsius, ambientThreshold, priz);
                    }

                    // Send "off" while device is on (ensures it stays off)
                    if (deviceIsOn)
                    {
                        string curTypeNum = (current_type ?? "").ToLower() switch
                        {
                            "loop" or "circle" => "1",
                            "clamp" => "2",
                            _ => "1"
                        };
                        string motorTypeNum = (inverter ?? "").Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ? "2" : "1";
                        var now = DateTime.Now;
                        var cal = new PersianCalendar();
                        var persianNow = $"{cal.GetYear(now):0000}/{cal.GetMonth(now):00}/{cal.GetDayOfMonth(now):00} {now:HH:mm:ss}";
                        var ambientCmdContent = $"off,{curTypeNum},{motorTypeNum},{persianNow}";
                        var cmdFile = Path.Combine(commandDir, $"device_{deviceCode}.txt");
                        await System.IO.File.WriteAllTextAsync(cmdFile, ambientCmdContent, Encoding.UTF8);
                    }
                }
            }

            // Read command file
            var commandFile = Path.Combine(commandDir, $"device_{deviceCode}.txt");
            string commandContent;
            if (System.IO.File.Exists(commandFile))
            {
                commandContent = (await System.IO.File.ReadAllTextAsync(commandFile)).Trim();
                if (!pendingOff && commandContent != "stand")
                    await System.IO.File.WriteAllTextAsync(commandFile, "stand", Encoding.UTF8);
            }
            else
            {
                commandContent = pendingOff ? "off" : "stand";
            }

            return Content(commandContent, "text/plain");
        }
        catch (Exception ex)
        {
            return Content($"ERROR: {ex.Message}", "text/plain");
        }
    }

    private async Task SaveDataRecordAsync(
        string deviceCode,
        int monitoringId,
        int? customerReceiptId,
        string? priz, string? motor_state, string? element1, string? element2,
        string? temp1, string? temp2, string? temp3,
        string? jaryan, string? tavan, string? dc1, string? ac1)
    {
        static bool? ParseOnOff(string? val) => val?.ToLower() switch
        {
            "on" or "1" or "true" => true,
            "off" or "0" or "false" => false,
            _ => null
        };

        static float? ParseFloat(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;
            if (val.Trim().ToLower() == "nan") return null;
            return float.TryParse(val.Trim(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var r) ? r : null;
        }

        static float? ParseMaxFloat(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;
            var parts = val.Split('-', StringSplitOptions.RemoveEmptyEntries);
            float? max = null;
            foreach (var part in parts)
            {
                var f = ParseFloat(part.Trim());
                if (f.HasValue && (!max.HasValue || f.Value > max.Value))
                    max = f;
            }
            return max;
        }

        var record = new MonitoringDataRecord
        {
            DeviceCode = deviceCode,
            MonitoringId = monitoringId,
            CustomerReceiptId = customerReceiptId,
            TemperatureRef = ParseFloat(temp1),
            TemperatureFreez = ParseFloat(temp2),
            TemperatureEnv = ParseFloat(temp3),
            MotorState = ParseOnOff(motor_state) ?? false,
            Bargh = ParseOnOff(priz) ?? false,
            Element1 = ParseOnOff(element1) ?? false,
            Element2 = ParseOnOff(element2) ?? false,
            Fdc1 = ParseOnOff(dc1) ?? false,
            Fac1 = ParseOnOff(ac1) ?? false,
            Jaryan = ParseMaxFloat(jaryan),
            Power = ParseFloat(tavan),
            Timestamp = DateTime.UtcNow,
            TimestampFa = PersianDateHelper.ToPersianDateTimeString(DateTime.Now, true)
        };

        _db.MonitoringDataRecords.Add(record);
        await _db.SaveChangesAsync();
    }

    private async Task CreateArchiveForConnection(int monitoringConnectionId, int workshopId, int? customerReceiptId, string deviceCode, DateTimeOffset connectionCreatedAt)
    {
        var recordCount = await _db.MonitoringDataRecords
            .CountAsync(r => r.MonitoringId == monitoringConnectionId);

        var archive = new MonitoringDataArchive
        {
            WorkshopId = workshopId,
            MonitoringReceiptConnectionId = monitoringConnectionId,
            CustomerReceiptId = customerReceiptId,
            DeviceCode = deviceCode,
            StartedAt = connectionCreatedAt.DateTime,
            EndedAt = DateTime.UtcNow,
            RecordCount = recordCount,
            EstimatedBytes = recordCount * 300L,
            Downloaded = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.MonitoringDataArchives.Add(archive);
        await _db.SaveChangesAsync();
    }

    private async Task SaveChangeLogIfNeeded(string deviceCode, MonitoringDeviceData newData, MonitoringDeviceData? oldData, int monitoringId, int? customerReceiptId, int workshopId)
    {
        try
        {
            if (oldData == null) return;

            var changes = new List<(string type, string? oldVal, string? newVal, string desc)>();

            if (oldData.Priz != newData.Priz)
            {
                var oldP = IsOn(oldData.Priz) ? "روشن" : "خاموش";
                var newP = IsOn(newData.Priz) ? "روشن" : "خاموش";
                changes.Add(("Priz", oldData.Priz, newData.Priz, $"برق اصلی {oldP} → {newP}"));
            }
            if (oldData.MotorState != newData.MotorState)
            {
                var oldM = IsOn(oldData.MotorState) ? "روشن" : "خاموش";
                var newM = IsOn(newData.MotorState) ? "روشن" : "خاموش";
                changes.Add(("MotorState", oldData.MotorState, newData.MotorState, $"موتور {oldM} → {newM}"));
            }
            if (oldData.Element1 != newData.Element1)
            {
                var oldE1 = IsOn(oldData.Element1) ? "روشن" : "خاموش";
                var newE1 = IsOn(newData.Element1) ? "روشن" : "خاموش";
                changes.Add(("Element1", oldData.Element1, newData.Element1, $"المنت ۱ {oldE1} → {newE1}"));
            }
            if (oldData.Element2 != newData.Element2)
            {
                var oldE2 = IsOn(oldData.Element2) ? "روشن" : "خاموش";
                var newE2 = IsOn(newData.Element2) ? "روشن" : "خاموش";
                changes.Add(("Element2", oldData.Element2, newData.Element2, $"المنت ۲ {oldE2} → {newE2}"));
            }

            if (changes.Count == 0) return;

            var snapshotObj = new
            {
                newData.Code, newData.Priz, newData.MotorState, newData.Element1, newData.Element2,
                newData.Temp1, newData.Temp2, newData.Jaryan, newData.CntM, newData.CntE1, newData.CntE2,
                newData.Temp3, newData.CurrentType, newData.Inverter, newData.Voltage, newData.Tavan,
                newData.Freq, newData.Dc1, newData.Dc2, newData.Ac1, newData.Ac2, newData.DeviceType,
                Timestamp = DateTime.UtcNow,
                TimestampFa = PersianDateHelper.ToPersianDateTimeString(DateTime.Now, true)
            };
            var snapshotJson = JsonSerializer.Serialize(snapshotObj);

            var nowFa = PersianDateHelper.ToPersianDateTimeString(DateTime.Now, true);

            foreach (var (type, oldVal, newVal, desc) in changes)
            {
                _db.MonitoringDeviceChangeLogs.Add(new MonitoringDeviceChangeLog
                {
                    DeviceCode = deviceCode,
                    ChangeType = type,
                    OldValue = oldVal,
                    NewValue = newVal,
                    ChangeDescription = desc,
                    DataSnapshot = snapshotJson,
                    MonitoringId = monitoringId,
                    CustomerReceiptId = customerReceiptId,
                    WorkshopId = workshopId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedAtFa = nowFa
                });
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save change log for device {Code}", deviceCode);
        }
    }

    private static bool IsOn(string? val) =>
        val != null && val.Trim().Equals("on", StringComparison.OrdinalIgnoreCase);

    private async Task SaveAlertAsync(string deviceCode, string typeAlarm, string? maxJaryan, string? had, int monitoringId, int? customerReceiptId)
    {
        try
        {
            var lastAlert = await _db.MonitoringAlerts
                .Where(a => a.DeviceCode == deviceCode && a.Type == typeAlarm)
                .OrderByDescending(a => a.Num)
                .FirstOrDefaultAsync();

            var num = (lastAlert?.Num ?? 0) + 1;

            var now = DateTime.Now;
            var persianDateTime = PersianDateHelper.ToPersianDateTimeString(now, true);
            var parts = persianDateTime.Split(' ');
            var persianDate = parts.Length > 0 ? parts[0] : "";
            var persianTime = parts.Length > 1 ? parts[1] : now.ToString("HH:mm");

            string message;
            string command;

            if (typeAlarm.Equals("motor", StringComparison.OrdinalIgnoreCase))
            {
                var j = maxJaryan ?? "0";
                var h = had ?? "0";
                if (num == 1)
                {
                    message = $"آمپر موتور دستگاه در نقطه {j} آمپر بالاتر از حد مجاز {h} می باشد تاریخ: {persianDate} زمان: {persianTime}";
                    command = "اخطار اول";
                }
                else
                {
                    message = $"قطع برق دستگاه به دلیل بالا بودن آمپر موتور در نقطه {j} آمپر بالاتر از حد مجاز {h} آمپر می باشد تاریخ: {persianDate} زمان: {persianTime}";
                    command = "قطع برق دستگاه";
                }
            }
            else
            {
                message = $"همزمانی در مدار بودن موتور و هیتر شماره {maxJaryan ?? "0"} غیر مجاز می باشد و باعث قطع برق دستگاه شده است تاریخ: {persianDate} زمان: {persianTime}";
                command = "قطع برق دستگاه";
            }

            _db.MonitoringAlerts.Add(new MonitoringAlert
            {
                DeviceCode = deviceCode,
                MonitoringId = monitoringId,
                CustomerReceiptId = customerReceiptId,
                Type = typeAlarm,
                Num = num,
                Message = message,
                Command = command,
                Time = now,
                State = true,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save alert for device {Code}", deviceCode);
        }
    }

    private async Task SaveAmbientTempAlertAsync(string deviceCode, int monitoringId, int? customerReceiptId, double currentTemp, int threshold, string? priz)
    {
        try
        {
            var lastAlert = await _db.MonitoringAlerts
                .Where(a => a.DeviceCode == deviceCode && a.Type == "ambient_temp")
                .OrderByDescending(a => a.Num)
                .FirstOrDefaultAsync();
            var num = (lastAlert?.Num ?? 0) + 1;

            var now = DateTime.Now;
            var persianDateTime = PersianDateHelper.ToPersianDateTimeString(now, true);
            var parts = persianDateTime.Split(' ');
            var persianDate = parts.Length > 0 ? parts[0] : "";
            var persianTime = parts.Length > 1 ? parts[1] : now.ToString("HH:mm");
            var powerState = IsOn(priz) ? "روشن" : "خاموش";

            var message = $"دمای محیط به {currentTemp:F1} درجه سانتیگراد رسیده است (حد مجاز {threshold} درجه). دستگاه خاموش شد. وضعیت پریز: {powerState} تاریخ: {persianDate} زمان: {persianTime}";
            var command = "قطع برق دستگاه";

            _db.MonitoringAlerts.Add(new MonitoringAlert
            {
                DeviceCode = deviceCode,
                MonitoringId = monitoringId,
                CustomerReceiptId = customerReceiptId,
                Type = "ambient_temp",
                Num = num,
                Message = message,
                Command = command,
                Time = now,
                State = true,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save ambient temp alert for device {Code}", deviceCode);
        }
    }

    [HttpGet("cycles")]
    public async Task<IActionResult> GetCycles([FromQuery] string? code, [FromQuery] int? monitoringId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest("code is required");

            var query = _db.MonitoringDeviceChangeLogs
                .Where(c => c.DeviceCode == code);
            if (monitoringId.HasValue)
                query = query.Where(c => c.MonitoringId == monitoringId.Value);

            var logs = await query
                .Where(c => c.ChangeType == "MotorState" || c.ChangeType == "Element1" || c.ChangeType == "Element2")
                .Select(c => new { c.ChangeType, c.OldValue, c.NewValue })
                .ToListAsync();

            var cycles = new { motor = 0, element1 = 0, element2 = 0 };
            int motorCnt = 0, e1Cnt = 0, e2Cnt = 0;

            foreach (var log in logs)
            {
                if (IsOnState(log.OldValue) && IsOffState(log.NewValue))
                {
                    switch (log.ChangeType)
                    {
                        case "MotorState": motorCnt++; break;
                        case "Element1": e1Cnt++; break;
                        case "Element2": e2Cnt++; break;
                    }
                }
            }

            return Ok(new { motor = motorCnt, element1 = e1Cnt, element2 = e2Cnt });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cycles for device {Code}", code);
            return Ok(new { motor = 0, element1 = 0, element2 = 0 });
        }
    }

    private static bool IsOnState(string? val) =>
        val != null && (val.Trim().Equals("on", StringComparison.OrdinalIgnoreCase)
            || val.Trim() == "1" || val.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));

    private static bool IsOffState(string? val) =>
        val != null && (val.Trim().Equals("off", StringComparison.OrdinalIgnoreCase)
            || val.Trim() == "0" || val.Trim().Equals("false", StringComparison.OrdinalIgnoreCase));
}

public sealed class SendCommandRequest
{
    public string Code { get; set; } = "";
    public string Value { get; set; } = "";
    public string? SensorType { get; set; }
    public string? MotorType { get; set; }
}
