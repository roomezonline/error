using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

public class SystemEventService
{
    private readonly ErrorServiceDbContext _db;
    private readonly ILogger<SystemEventService> _logger;

    public SystemEventService(ErrorServiceDbContext db, ILogger<SystemEventService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogEventAsync(SystemEventLog log)
    {
        try
        {
            // بررسی تکراری بودن خطا در ۱ ساعت اخیر برای جلوگیری از شلوغی دیتابیس
            if (log.Severity >= EventSeverity.Error && !string.IsNullOrEmpty(log.ErrorCode))
            {
                var oneHourAgo = DateTimeOffset.UtcNow.AddHours(-1);
                var existing = await _db.SystemEventLogs
                    .Where(x => x.ErrorCode == log.ErrorCode && x.OccurredAt > oneHourAgo && !x.IsResolved)
                    .OrderByDescending(x => x.OccurredAt)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    existing.OccurrenceCount++;
                    existing.LastOccurrenceAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync();
                    return;
                }
            }

            _db.SystemEventLogs.Add(log);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // اگر خود سیستم لاگ به مشکل بخورد، حداقل در کنسول چاپ شود
            _logger.LogError(ex, "Failed to save SystemEventLog to database.");
        }
    }

    public async Task<List<SystemEventLogDto>> GetLogsAsync(EventLogFilterDto filter)
    {
        var query = _db.SystemEventLogs.AsQueryable();

        if (filter.FromDate.HasValue) query = query.Where(x => x.OccurredAt >= filter.FromDate.Value);
        if (filter.ToDate.HasValue) query = query.Where(x => x.OccurredAt <= filter.ToDate.Value);
        if (filter.Severities?.Any() == true) query = query.Where(x => filter.Severities.Contains(x.Severity));
        if (filter.Categories?.Any() == true) query = query.Where(x => filter.Categories.Contains(x.Category));
        if (filter.Statuses?.Any() == true) query = query.Where(x => filter.Statuses.Contains(x.Status));
        if (filter.OnlyUnresolved == true) query = query.Where(x => !x.IsResolved);
        if (!string.IsNullOrEmpty(filter.SearchTerm))
        {
            var search = filter.SearchTerm.ToLower();
            query = query.Where(x => x.PersianTitle.ToLower().Contains(search) || 
                                   x.TechnicalTitle.ToLower().Contains(search) || 
                                   x.ErrorCode.ToLower().Contains(search));
        }

        query = filter.SortBy switch
        {
            "Severity" => filter.SortDescending ? query.OrderByDescending(x => x.Severity) : query.OrderBy(x => x.Severity),
            _ => filter.SortDescending ? query.OrderByDescending(x => x.OccurredAt) : query.OrderBy(x => x.OccurredAt)
        };

        var logs = await query.Skip(filter.Skip).Take(filter.Take).ToListAsync();

        return logs.Select(x => new SystemEventLogDto
        {
            Id = x.Id,
            OccurredAt = x.OccurredAt,
            Severity = x.Severity.ToString(),
            Category = x.Category.ToString(),
            PersianTitle = x.PersianTitle,
            PersianDescription = x.PersianDescription,
            TechnicalTitle = x.TechnicalTitle,
            TechnicalDetails = x.TechnicalDetails,
            StackTrace = x.StackTrace,
            ClientIp = x.ClientIp,
            UserName = x.UserName,
            RequestPath = x.RequestPath,
            Status = x.Status.ToString(),
            OccurrenceCount = x.OccurrenceCount,
            IsResolved = x.IsResolved,
            TimeSinceOccurrence = DateTimeOffset.UtcNow - x.OccurredAt
        }).ToList();
    }

    public async Task<SystemHealthSummaryDto> GetHealthSummaryAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var dayAgo = now.AddDays(-1);

        var logs24h = await _db.SystemEventLogs
            .Where(x => x.OccurredAt >= dayAgo)
            .ToListAsync();

        var summary = new SystemHealthSummaryDto
        {
            TotalEvents24h = logs24h.Count,
            CriticalErrors24h = logs24h.Count(x => x.Severity >= EventSeverity.Critical),
            UnresolvedErrors = await _db.SystemEventLogs.CountAsync(x => !x.IsResolved && x.Severity >= EventSeverity.Error),
            LastCheckAt = now
        };

        // محاسبه میانگین زمان پاسخ‌دهی (در صورت وجود لاگ‌های API)
        var apiLogs = logs24h.Where(x => x.ResponseTimeMs.HasValue).ToList();
        if (apiLogs.Any())
        {
            summary.AvgResponseTime = apiLogs.Average(x => x.ResponseTimeMs!.Value);
            var errorCount = apiLogs.Count(x => x.ResponseStatusCode >= 400);
            summary.ErrorRate = (int)((double)errorCount / apiLogs.Count * 100);
        }

        // ترند ۲۴ ساعت اخیر
        for (int i = 23; i >= 0; i--)
        {
            var start = dayAgo.AddHours(24 - i - 1);
            var end = start.AddHours(1);
            summary.Last24HoursTrend.Add(new ErrorTrendDto
            {
                Hour = start,
                ErrorCount = logs24h.Count(x => x.OccurredAt >= start && x.OccurredAt < end && x.Severity >= EventSeverity.Error),
                WarningCount = logs24h.Count(x => x.OccurredAt >= start && x.OccurredAt < end && x.Severity == EventSeverity.Warning)
            });
        }

        // برترین خطاها
        summary.TopErrors = await _db.SystemEventLogs
            .Where(x => x.Severity >= EventSeverity.Error && x.OccurredAt >= dayAgo)
            .GroupBy(x => new { x.ErrorCode, x.PersianTitle })
            .Select(g => new TopErrorDto
            {
                ErrorCode = g.Key.ErrorCode ?? "Unknown",
                PersianTitle = g.Key.PersianTitle,
                Count = g.Sum(x => x.OccurrenceCount),
                LastOccurrence = g.Max(x => x.OccurredAt)
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync();

        return summary;
    }

    public async Task ResolveEventAsync(int id, int userId)
    {
        var log = await _db.SystemEventLogs.FindAsync(id);
        if (log != null)
        {
            log.IsResolved = true;
            log.ResolvedAt = DateTimeOffset.UtcNow;
            log.ResolvedByUserId = userId;
            log.Status = InvestigationStatus.Resolved;
            await _db.SaveChangesAsync();
        }
    }
}
