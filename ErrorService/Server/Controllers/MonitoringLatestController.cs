using System.Text;
using ErrorService.Server.Services;
using ErrorService.Shared;
using ErrorService.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/monitoring/latest")]
[Authorize]
public class MonitoringLatestController : ControllerBase
{
    public MonitoringLatestController()
    {
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("code is required");

        var data = ReadFromFile(code);
        return Ok(data);
    }

    private static MonitoringDeviceData ReadFromFile(string code)
    {
        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitoring-data");
        var filePath = Path.Combine(baseDir, code, "server", $"device_{code}.txt");

        var result = new MonitoringDeviceData { Code = code };

        if (!System.IO.File.Exists(filePath))
        {
            result.Priz = "off";
            result.MotorState = "0";
            result.Element1 = "0";
            result.Element2 = "0";
            result.Temp1 = "0";
            result.Temp2 = "0";
            result.Temp3 = "0";
            result.Jaryan = "0";
            result.Voltage = "0";
            result.Tavan = "0";
            result.Timestamp = DateTime.MinValue;
            result.TimestampFa = "";
            return result;
        }

        var lines = System.IO.File.ReadAllLines(filePath, Encoding.UTF8);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var eqIdx = trimmed.IndexOf('=');
            if (eqIdx < 0) continue;
            var key = trimmed[..eqIdx].Trim().ToLowerInvariant();
            var val = trimmed[(eqIdx + 1)..].Trim();

            switch (key)
            {
                case "priz": result.Priz = val; break;
                case "motor_state": result.MotorState = val; break;
                case "element1": result.Element1 = val; break;
                case "element2": result.Element2 = val; break;
                case "temp1": result.Temp1 = val; break;
                case "temp2": result.Temp2 = val; break;
                case "temp3": result.Temp3 = val; break;
                case "jaryan": result.Jaryan = val; break;
                case "cnt_m": result.CntM = val; break;
                case "cnt_e1": result.CntE1 = val; break;
                case "cnt_e2": result.CntE2 = val; break;
                case "current_type": result.CurrentType = val; break;
                case "inverter": result.Inverter = val; break;
                case "voltage": result.Voltage = val; break;
                case "tavan": result.Tavan = val; break;
                case "freq": result.Freq = val; break;
                case "dc1": result.Dc1 = val; break;
                case "dc2": result.Dc2 = val; break;
                case "ac1": result.Ac1 = val; break;
                case "ac2": result.Ac2 = val; break;
                case "device_type": result.DeviceType = val; break;
                case "e1_start_fridge": result.E1StartFridge = val; break;
                case "e1_stop_fridge": result.E1StopFridge = val; break;
                case "e1_start_freezer": result.E1StartFreezer = val; break;
                case "e1_stop_freezer": result.E1StopFreezer = val; break;
                case "e2_start_fridge": result.E2StartFridge = val; break;
                case "e2_stop_fridge": result.E2StopFridge = val; break;
                case "e2_start_freezer": result.E2StartFreezer = val; break;
                case "e2_stop_freezer": result.E2StopFreezer = val; break;
                case "timestamp":
                    if (DateTime.TryParse(val, out var ts))
                    {
                        result.Timestamp = ts;
                        result.TimestampFa = PersianDateHelper.ToPersianDateTimeString(ts, true);
                    }
                    break;
            }
        }

        result.FileWriteTime = System.IO.File.GetLastWriteTimeUtc(filePath);
        result.FileWriteTimeFa = PersianDateHelper.ToPersianDateTimeString(result.FileWriteTime.ToLocalTime(), true);

        return result;
    }
}
