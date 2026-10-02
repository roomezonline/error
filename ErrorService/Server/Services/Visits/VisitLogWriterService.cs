using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services.Visits;

/// <summary>
/// نوشتن دسته‌ای بازدیدها در پس‌زمینه — از Channel می‌خواند و هر چند صدم ثانیه
/// یک‌جا در دیتابیس insert می‌کند تا هیچ کندی به درخواست کاربر برنگردد.
///
/// علاوه بر لاگ خام صفحه‌بازدیدها (VisitLog)، «جلسه/بازدید» را نیز بر اساس
/// معیار جهانی می‌سازد: همه صفحاتی که یک بازدیدکننده در بازه ۳۰ دقیقه بدون وقفه
/// ببیند = یک بازدید (Session) در جدول VisitSessions.
/// </summary>
public sealed class VisitLogWriterService : BackgroundService
{
    private const int BatchSize = 256;
    private const int FlushDelayMs = 500;

    /// <summary>پنجره استاندارد جلسه (معیار Google Analytics): ۳۰ دقیقه وقفه = پایان بازدید.</summary>
    public static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(30);

    private readonly VisitTrackingService _tracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VisitLogWriterService> _logger;

    /// <summary>جلسه‌های باز در حافظه — کلید: VisitorId (مرورگر/دستگاه). تک‌نخ (فقط حلقه ExecuteAsync).</summary>
    private readonly Dictionary<string, SessionState> _open = new(StringComparer.Ordinal);

    private long _lastPruneTicks;

    private sealed class SessionState
    {
        public long Id; // صفر = هنوز در دیتابیس ثبت نشده
        public string VisitorId = "";
        public DateTime StartedAtUtc;
        public DateTime EndedAtUtc;
        public DateOnly VisitDate;
        public string IdentityKey = "";
        public string UserName = "";
        public string Ip = "";
        public string UserAgent = "";
        public string Referrer = "";
        public string EntryPath = "";
        public string LastPath = "";
        public int PageViews;
        public bool IsBot;
        public VisitSession? Pending; // موجودیت در انتظار INSERT در همین دسته
    }

    public VisitLogWriterService(
        VisitTrackingService tracker,
        IServiceScopeFactory scopeFactory,
        ILogger<VisitLogWriterService> logger)
    {
        _tracker = tracker;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VisitLogWriterService is running");

        try
        {
            await LoadOpenSessionsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resume open visit sessions");
        }

        var reader = _tracker.Reader;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // انتظار تا اولین بازدید برسد (بدون مصرف CPU)
                while (!await reader.WaitToReadAsync(stoppingToken))
                {
                }

                var batch = new List<VisitCapture>(BatchSize);
                while (batch.Count < BatchSize && reader.TryRead(out var item))
                {
                    batch.Add(item);
                }

                if (batch.Count < BatchSize)
                {
                    // کمی صبر برای پر شدن دسته، سپس نهایی‌سازی
                    await Task.Delay(FlushDelayMs, stoppingToken);
                    while (batch.Count < BatchSize && reader.TryRead(out var item))
                    {
                        batch.Add(item);
                    }
                }

                await SaveBatchAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // توقف سرویس — طبیعی
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VisitLogWriterService stopped unexpectedly");
        }
    }

    /// <summary>ادامه جلسه‌هایی که هنگام ری‌استارت باز بودند (تا ۳۰ دقیقه قبل).</summary>
    private async Task LoadOpenSessionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();

        var cutoff = DateTime.UtcNow - SessionTimeout;
        var rows = await db.VisitSessions.AsNoTracking()
            .Where(x => x.EndedAtUtc >= cutoff)
            .ToListAsync(ct);

        foreach (var r in rows)
        {
            _open[r.VisitorId] = new SessionState
            {
                Id = r.Id,
                VisitorId = r.VisitorId,
                StartedAtUtc = r.StartedAtUtc,
                EndedAtUtc = r.EndedAtUtc,
                VisitDate = r.VisitDate,
                IdentityKey = r.IdentityKey,
                UserName = r.UserName,
                Ip = r.Ip,
                UserAgent = r.UserAgent,
                Referrer = r.Referrer,
                EntryPath = r.EntryPath,
                LastPath = r.LastPath,
                PageViews = r.PageViews,
                IsBot = r.IsBot
            };
        }

        if (rows.Count > 0)
        {
            _logger.LogInformation("VisitLogWriterService resumed {Count} open session(s)", rows.Count);
        }
    }

    private async Task SaveBatchAsync(List<VisitCapture> batch, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();

            var logRows = new List<VisitLog>(batch.Count);
            var fresh = new List<SessionState>();
            var freshSet = new HashSet<SessionState>();
            var touched = new HashSet<SessionState>();

            foreach (var c in batch)
            {
                SessionState st;

                if (!_open.TryGetValue(c.VisitorId, out var existing) ||
                    c.VisitedAtUtc - existing.EndedAtUtc > SessionTimeout)
                {
                    st = new SessionState
                    {
                        VisitorId = Truncate(c.VisitorId, 36),
                        StartedAtUtc = c.VisitedAtUtc,
                        EndedAtUtc = c.VisitedAtUtc,
                        VisitDate = DateOnly.FromDateTime(c.VisitedAtUtc.AddMinutes(330)), // وقت تهران
                        IdentityKey = c.HasUserIdentity ? Truncate(c.IdentityKey, 64) : "v" + c.VisitorId,
                        UserName = c.HasUserIdentity ? Truncate(c.UserName, 200) : "",
                        Ip = Truncate(c.Ip, 45),
                        UserAgent = Truncate(c.UserAgent, 400),
                        Referrer = Truncate(c.Referrer, 500),
                        EntryPath = Truncate(c.Path, 300),
                        LastPath = Truncate(c.Path, 300),
                        PageViews = 1,
                        IsBot = VisitBotDetector.IsBot(c.UserAgent)
                    };

                    _open[c.VisitorId] = st;
                    fresh.Add(st);
                    freshSet.Add(st);
                }
                else
                {
                    st = existing;

                    // کاربر وسط جلسه وارد شده (لاگین) → هویت همان جلسه ارتقا می‌یابد
                    if (c.HasUserIdentity)
                    {
                        st.IdentityKey = Truncate(c.IdentityKey, 64);
                        st.UserName = Truncate(c.UserName, 200);
                    }

                    st.EndedAtUtc = c.VisitedAtUtc;
                    st.LastPath = Truncate(c.Path, 300);
                    st.PageViews++;
                }

                touched.Add(st);

                logRows.Add(new VisitLog
                {
                    VisitedAtUtc = c.VisitedAtUtc,
                    VisitDate = DateOnly.FromDateTime(c.VisitedAtUtc.AddMinutes(330)),
                    Ip = Truncate(c.Ip, 45),
                    Path = Truncate(c.Path, 300),
                    Referrer = Truncate(c.Referrer, 500),
                    UserAgent = Truncate(c.UserAgent, 400),
                    VisitorId = Truncate(c.VisitorId, 36),
                    IsBot = VisitBotDetector.IsBot(c.UserAgent)
                });
            }

            db.VisitLogs.AddRange(logRows);

            // جلسه‌های تازه → INSERT
            foreach (var st in fresh)
            {
                var entity = new VisitSession
                {
                    StartedAtUtc = st.StartedAtUtc,
                    EndedAtUtc = st.EndedAtUtc,
                    VisitDate = st.VisitDate,
                    VisitorId = st.VisitorId,
                    IdentityKey = st.IdentityKey,
                    UserName = st.UserName,
                    Ip = st.Ip,
                    UserAgent = st.UserAgent,
                    Referrer = st.Referrer,
                    EntryPath = st.EntryPath,
                    LastPath = st.LastPath,
                    PageViews = st.PageViews,
                    IsBot = st.IsBot
                };
                db.VisitSessions.Add(entity);
                st.Pending = entity;
            }

            // جلسه‌های بازِ قبلی → بروزرسانی
            var persisted = touched.Where(x => !freshSet.Contains(x)).ToList();
            if (persisted.Count > 0)
            {
                var ids = persisted.Select(x => x.Id).Distinct().ToList();
                var rows = await db.VisitSessions
                    .Where(x => ids.Contains(x.Id))
                    .ToListAsync(ct);

                var map = new Dictionary<long, VisitSession>(rows.Count);
                foreach (var row in rows) map[row.Id] = row;

                foreach (var st in persisted)
                {
                    if (!map.TryGetValue(st.Id, out var row)) continue;
                    row.EndedAtUtc = st.EndedAtUtc;
                    row.LastPath = st.LastPath;
                    row.PageViews = st.PageViews;
                    row.IdentityKey = st.IdentityKey;
                    row.UserName = st.UserName;
                }
            }

            await db.SaveChangesAsync(ct);

            foreach (var st in fresh)
            {
                st.Id = st.Pending!.Id;
                st.Pending = null;
            }

            PruneOpenSessions(DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist {Count} visit logs", batch.Count);
        }
    }

    private void PruneOpenSessions(DateTime now)
    {
        var last = Interlocked.Read(ref _lastPruneTicks);
        if (now.Ticks - last < TimeSpan.FromMinutes(5).Ticks) return;
        if (Interlocked.CompareExchange(ref _lastPruneTicks, now.Ticks, last) != last) return;

        var cutoff = now - SessionTimeout;
        var stale = _open.Where(x => x.Value.EndedAtUtc < cutoff).Select(x => x.Key).ToList();
        foreach (var key in stale)
        {
            _open.Remove(key);
        }
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= max ? value : value[..max];
    }
}
