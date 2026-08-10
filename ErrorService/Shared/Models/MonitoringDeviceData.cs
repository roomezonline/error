namespace ErrorService.Shared.Models;

public class MonitoringDeviceData
{
    public string Code { get; set; } = "";
    public string Priz { get; set; } = "";
    public string MotorState { get; set; } = "";
    public string Element1 { get; set; } = "";
    public string Element2 { get; set; } = "";
    public string Temp1 { get; set; } = "";
    public string Temp2 { get; set; } = "";
    public string Jaryan { get; set; } = "";
    public string CntM { get; set; } = "";
    public string CntE1 { get; set; } = "";
    public string CntE2 { get; set; } = "";
    public string Temp3 { get; set; } = "";
    public string CurrentType { get; set; } = "";
    public string Inverter { get; set; } = "";
    public string Voltage { get; set; } = "";
    public string Tavan { get; set; } = "";
    public string Freq { get; set; } = "";
    public string Dc1 { get; set; } = "";
    public string Dc2 { get; set; } = "";
    public string Ac1 { get; set; } = "";
    public string Ac2 { get; set; } = "";
    public string DeviceType { get; set; } = "";
    public string E1StartFridge { get; set; } = "";
    public string E1StopFridge { get; set; } = "";
    public string E1StartFreezer { get; set; } = "";
    public string E1StopFreezer { get; set; } = "";
    public string E2StartFridge { get; set; } = "";
    public string E2StopFridge { get; set; } = "";
    public string E2StartFreezer { get; set; } = "";
    public string E2StopFreezer { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string TimestampFa { get; set; } = "";
    public DateTime FileWriteTime { get; set; }
    public string FileWriteTimeFa { get; set; } = "";
}
