using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services.Visits;

/// <summary>
/// سرویس پس‌زمینه نگهداری آمار بازدید:
/// ۰) در اولین اجرا، جلسه‌های (بازدیدهای) تاریخی را از لاگ خام بازسازی می‌کند.
/// ۱) هر ساعت آمار روزهای «دیروز و امروز» و روزهای فاقد آمار را از جدول جلسه‌ها بازمحاسبه می‌کند.
/// ۲) لاگ خام قدیمی‌تر از ۹۰ روز و جلسه‌های قدیمی‌تر از یک سال را حذف می‌کند (آمار تجمیعی همیشه می‌ماند).
/// </summary>
public sealed class VisitMaintenanceService : BackgroundService
{
    public const int RawRetentionDays = 90;
    public const int SessionRetentionDays = 365;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VisitMaintenanceService> _logger;

    public VisitMaintenanceService(IServiceScopeFactory scopeFactory, ILogger<VisitMaintenanceService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VisitMaintenanceService is running (raw: {Raw}d, sessions: {Sess}d)", RawRetentionDays, SessionRetentionDays);

        // اجرای اولیه با کمی تأخیر تا اپ بالا آمده باشد
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VisitMaintenanceService run failed");
            }

            try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330));
        var yesterday = today.AddDays(-1);

        // ۰) اولین اجرا: بازسازی جلسه‌ها از لاگ خام (داده‌های قبل از این قابلیت)
        var backfilled = false;
        if (!await db.VisitSessions.AnyAsync(ct))
        {
            backfilled = await BackfillSessionsAsync(db, ct);
        }

        // ۰۱) آمار مسیرها (پربحترین صفحات): اولین اجرا از کل لاگ خام + بازمحاسبه روزهای اخیر
        if (!await db.DailyPagePathStats.AnyAsync(ct))
        {
            await BackfillPathStatsAsync(db, ct);
        }
        await RecomputePathStatsAsync(db, today, ct);
        await RecomputePathStatsAsync(db, yesterday, ct);

        // ۱) بازمحاسبه آمار روزها از روی جلسه‌ها
        var sessionDates = await db.VisitSessions
            .Where(x => !x.IsBot)
            .Select(x => x.VisitDate)
            .Distinct()
            .ToListAsync(ct);
        var statDates = (await db.DailyVisitStats.Select(x => x.Date).ToListAsync(ct)).ToHashSet();

        var datesToWrite = new HashSet<DateOnly> { today, yesterday };
        foreach (var date in sessionDates)
        {
            if (backfilled || !statDates.Contains(date)) datesToWrite.Add(date);
        }

        foreach (var date in datesToWrite)
        {
            var visits = await db.VisitSessions
                .CountAsync(x => x.VisitDate == date && !x.IsBot, ct);
            var uniques = await db.VisitSessions
                .Where(x => x.VisitDate == date && !x.IsBot)
                .Select(x => x.IdentityKey)
                .Distinct()
                .CountAsync(ct);

            var row = await db.DailyVisitStats.FirstOrDefaultAsync(x => x.Date == date, ct);
            if (row == null)
            {
                db.DailyVisitStats.Add(new DailyVisitStat { Date = date, Visits = visits, Uniques = uniques });
            }
            else
            {
                row.Visits = visits;
                row.Uniques = uniques;
            }
        }

        await db.SaveChangesAsync(ct);

        // ۲) حذف لاگ خام قدیمی‌تر از ۹۰ روز
        var rawCutoff = today.AddDays(-RawRetentionDays);
        var purgedRaw = await db.VisitLogs
            .Where(x => x.VisitDate < rawCutoff)
            .ExecuteDeleteAsync(ct);

        // ۳) حذف جلسه‌های قدیمی‌تر از یک سال (آمار تجمیعی DailyVisitStats همیشه می‌ماند)
        var sessionCutoff = today.AddDays(-SessionRetentionDays);
        var purgedSessions = await db.VisitSessions
            .Where(x => x.VisitDate < sessionCutoff)
            .ExecuteDeleteAsync(ct);

        if (purgedRaw > 0 || purgedSessions > 0)
        {
            _logger.LogInformation("VisitMaintenanceService purged {Raw} raw logs (> {RawCutoff}) and {Sess} sessions (> {SessCutoff})",
                purgedRaw, rawCutoff, purgedSessions, sessionCutoff);
        }
    }

    /// <summary>
    /// بازسازی جلسه‌ها از روی لاگ خام — برای داده‌هایی که پیش از سیاست جلسه‌محور ثبت شده‌اند.
    /// قانون: همان بازدیدکننده، وقفه بیش از ۳۰ دقیقه = بازدید جدید.
    /// </summary>
    private async Task<bool> BackfillSessionsAsync(ErrorServiceDbContext db, CancellationToken ct)
    {
        var logs = await db.VisitLogs.AsNoTracking()
            .Where(x => !x.IsBot)
            .OrderBy(x => x.VisitedAtUtc)
            .ToListAsync(ct);

        if (logs.Count == 0) return false;

        var sessions = new List<VisitSession>();
        var open = new Dictionary<string, VisitSession>(StringComparer.Ordinal);

        foreach (var hit in logs)
        {
            if (!open.TryGetValue(hit.VisitorId, out var current) ||
                hit.VisitedAtUtc - current.EndedAtUtc > VisitLogWriterService.SessionTimeout)
            {
                current = new VisitSession
                {
                    StartedAtUtc = hit.VisitedAtUtc,
                    EndedAtUtc = hit.VisitedAtUtc,
                    VisitDate = hit.VisitDate,
                    VisitorId = hit.VisitorId,
                    IdentityKey = "v" + hit.VisitorId,
                    UserName = "",
                    Ip = hit.Ip,
                    UserAgent = hit.UserAgent,
                    Referrer = hit.Referrer,
                    EntryPath = hit.Path,
                    LastPath = hit.Path,
                    PageViews = 1,
                    IsBot = false
                };
                open[hit.VisitorId] = current;
                sessions.Add(current);
            }
            else
            {
                current.EndedAtUtc = hit.VisitedAtUtc;
                current.LastPath = hit.Path;
                current.PageViews++;
            }
        }

        db.VisitSessions.AddRange(sessions);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("VisitMaintenanceService backfilled {Sessions} session(s) from {Hits} raw hits", sessions.Count, logs.Count);
        return true;
    }

    /// <summary>بازسازی یک‌بارهٔ آمار مسیرها از کل لاگ خام (بدون ربات‌ها) — برای داده‌های قبلی.</summary>
    private async Task BackfillPathStatsAsync(ErrorServiceDbContext db, CancellationToken ct)
    {
        var rows = await db.VisitLogs.AsNoTracking()
            .Where(x => !x.IsBot)
            .GroupBy(x => new { x.VisitDate, x.Path })
            .Select(g => new { g.Key.VisitDate, g.Key.Path, PageViews = g.Count() })
            .ToListAsync(ct);

        if (rows.Count == 0) return;

        db.DailyPagePathStats.AddRange(rows.Select(r => new DailyPagePathStat
        {
            Date = r.VisitDate,
            Path = r.Path,
            PageViews = r.PageViews
        }));
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("VisitMaintenanceService backfilled {Rows} path stat row(s)", rows.Count);
    }

    /// <summary>
    /// بازمحاسبه آمار مسیرهای یک روز از روی لاگ خام (حذف + درج تازه) تا همیشه با لاگ‌ها هم‌خوان بماند.
    /// لاگ خام حداکثر ۹۰ روز می‌ماند ولی آمار مسیرها برای همیشه نگه داشته می‌شود.
    /// </summary>
    private async Task RecomputePathStatsAsync(ErrorServiceDbContext db, DateOnly date, CancellationToken ct)
    {
        var counts = await db.VisitLogs.AsNoTracking()
            .Where(x => x.VisitDate == date && !x.IsBot)
            .GroupBy(x => x.Path)
            .Select(g => new { Path = g.Key, PageViews = g.Count() })
            .ToListAsync(ct);

        await db.DailyPagePathStats.Where(x => x.Date == date).ExecuteDeleteAsync(ct);

        if (counts.Count == 0) return;

        db.DailyPagePathStats.AddRange(counts.Select(c => new DailyPagePathStat
        {
            Date = date,
            Path = c.Path,
            PageViews = c.PageViews
        }));
        await db.SaveChangesAsync(ct);
    }
}
