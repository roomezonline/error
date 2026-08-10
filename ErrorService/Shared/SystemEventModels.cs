using System;
using System.Collections.Generic;

namespace ErrorService.Shared;

public enum EventSeverity
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4,
    Fatal = 5
}

public enum EventCategory
{
    System = 0,
    Authentication = 1,
    Authorization = 2,
    Database = 3,
    Api = 4,
    Frontend = 5,
    Payment = 6,
    UserAction = 7,
    BackgroundJob = 8,
    ExternalService = 9,
    Security = 10,
    Performance = 11
}

public enum InvestigationStatus
{
    New = 0,
    Investigating = 1,
    Pending = 2,
    Resolved = 3,
    Ignored = 4,
    Escalated = 5
}

public class SystemEventLogDto
{
    public int Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string PersianTitle { get; set; } = string.Empty;
    public string PersianDescription { get; set; } = string.Empty;
    public string TechnicalTitle { get; set; } = string.Empty;
    public string TechnicalDetails { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? ClientIp { get; set; }
    public string? UserName { get; set; }
    public string? RequestPath { get; set; }
    public string Status { get; set; } = string.Empty;
    public int OccurrenceCount { get; set; }
    public bool IsResolved { get; set; }
    public TimeSpan? TimeSinceOccurrence { get; set; }
    
    public string SeverityColor => Severity.ToLower() switch
    {
        "fatal" => "#dc2626",
        "critical" => "#ef4444",
        "error" => "#f97316",
        "warning" => "#f59e0b",
        "info" => "#3b82f6",
        _ => "#6b7280"
    };
    
    public string CategoryIcon => Category.ToLower() switch
    {
        "security" => "shield",
        "payment" => "credit-card",
        "database" => "database",
        "api" => "cloud",
        "authentication" => "key",
        "frontend" => "monitor",
        _ => "info"
    };
}

public class EventLogFilterDto
{
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
    public List<EventSeverity>? Severities { get; set; }
    public List<EventCategory>? Categories { get; set; }
    public List<InvestigationStatus>? Statuses { get; set; }
    public string? SearchTerm { get; set; }
    public string? UserName { get; set; }
    public string? Component { get; set; }
    public bool? OnlyUnresolved { get; set; }
    public int? UserId { get; set; }
    public string SortBy { get; set; } = "OccurredAt";
    public bool SortDescending { get; set; } = true;
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 50;
}

public class SystemHealthSummaryDto
{
    public int TotalEvents24h { get; set; }
    public int CriticalErrors24h { get; set; }
    public int UnresolvedErrors { get; set; }
    public double AvgResponseTime { get; set; }
    public int ErrorRate { get; set; }
    public List<ErrorTrendDto> Last24HoursTrend { get; set; } = new();
    public List<TopErrorDto> TopErrors { get; set; } = new();
    public DateTimeOffset LastCheckAt { get; set; }
}

public class ErrorTrendDto
{
    public DateTimeOffset Hour { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}

public class TopErrorDto
{
    public string ErrorCode { get; set; } = string.Empty;
    public string PersianTitle { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTimeOffset LastOccurrence { get; set; }
}
