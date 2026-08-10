namespace ErrorService.Shared;

public class CycleRecord
{
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public string Duration { get; set; } = "";
    public double DurationSeconds { get; set; }
    public bool IsOpen { get; set; }
}

public class EnergyReportResponse
{
    public string Period { get; set; } = "";
    public string FromLabel { get; set; } = "";
    public string ToLabel { get; set; } = "";
    public double TotalKwh { get; set; }
    public double TotalOnHours { get; set; }
    public int TotalCycles { get; set; }
    public double AveragePower { get; set; }
    public double AverageCurrent { get; set; }
    public List<EnergyBreakdownItem> Breakdown { get; set; } = new();
    public List<HourlyConsumption> HourlyTrend { get; set; } = new();
}

public class EnergyBreakdownItem
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Color { get; set; } = "";
    public double Kwh { get; set; }
    public double Percent { get; set; }
    public double OnHours { get; set; }
    public double OnPercent { get; set; }
    public int CycleCount { get; set; }
    public double AvgCycleMinutes { get; set; }
}

public class HourlyConsumption
{
    public string Label { get; set; } = "";
    public double Kwh { get; set; }
    public double AvgTemp { get; set; }
}

public class ChartConnectionInfo
{
    public string CustomerName { get; set; } = "";
    public string CustomerMobile { get; set; } = "";
    public string DeviceSerial { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string? DeviceTypeName { get; set; }
    public string? DeviceBrandName { get; set; }
}

public class MonitoringChartResponse
{
    public ChartConnectionInfo? ConnectionInfo { get; set; }
    public List<string> Labels { get; set; } = new();
    public List<string> LabelsFa { get; set; } = new();
    public List<string> Timestamps { get; set; } = new();
    public List<float?> TemperatureRef { get; set; } = new();
    public List<float?> TemperatureFreez { get; set; } = new();
    public List<int> Motor { get; set; } = new();
    public List<int> Heater1 { get; set; } = new();
    public List<int> Heater2 { get; set; } = new();
    public List<int> Power { get; set; } = new();
    public List<float?> Tavan { get; set; } = new();
    public List<float?> Jaryan { get; set; } = new();
    public Dictionary<string, List<CycleRecord>> Cycles { get; set; } = new();
    public List<PointMeta> PointMetadata { get; set; } = new();
}

public class PointMeta
{
    public int Index { get; set; }
    public bool IsEquipmentTransition { get; set; }
    public string TransitionType { get; set; } = "";
    public bool HasNote { get; set; }
    public float? TempChange { get; set; }
    public float? CurrentChange { get; set; }
}
