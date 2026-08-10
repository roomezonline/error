namespace ErrorService.Shared;

public sealed class MonitoringTimelineResponse
{
    public string DeviceTitle { get; set; } = string.Empty;
    public string DeviceCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string WorkshopName { get; set; } = string.Empty;
    public DateTime ConnectionStartTime { get; set; }
    public DateTime? ConnectionEndTime { get; set; }
    public List<EquipmentTimeline> EquipmentTimelines { get; set; } = new();
    public List<TempSnapshot> TemperatureSnapshots { get; set; } = new();
    public List<EquipmentSummary> Summary { get; set; } = new();
    public long TotalDurationTicks { get; set; }
    public int TotalChangeLogs { get; set; }
}

public sealed class EquipmentTimeline
{
    public string ChangeType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public List<TimeSegment> Segments { get; set; } = new();
}

public sealed class TimeSegment
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long DurationTicks { get; set; }
}

public sealed class TempSnapshot
{
    public DateTime Timestamp { get; set; }
    public double? FridgeTemp { get; set; }
    public double? FreezerTemp { get; set; }
}

public sealed class EquipmentSummary
{
    public string ChangeType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int StartCount { get; set; }
    public long TotalDurationTicks { get; set; }
    public long AvgDurationTicks { get; set; }
    public double Percentage { get; set; }
    public int StateChangeCount { get; set; }
}

public static class EquipmentColors
{
    public const string Priz = "#ef4444";
    public const string MotorState = "#22c55e";
    public const string Element1 = "#3b82f6";
    public const string Element2 = "#f59e0b";

    public static string GetColor(string changeType) => changeType switch
    {
        "Priz" => Priz,
        "MotorState" => MotorState,
        "Element1" => Element1,
        "Element2" => Element2,
        _ => "#6b7280"
    };

    public static string GetDisplayName(string changeType) => changeType switch
    {
        "Priz" => "برق",
        "MotorState" => "موتور",
        "Element1" => "المنت ۱",
        "Element2" => "المنت ۲",
        _ => changeType
    };
}
