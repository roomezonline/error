using System.Globalization;
using System.Net;
using System.Security.Claims;
using ErrorService.Server.Services.Visits;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ErrorService.Server.Data;

namespace ErrorService.Server.Controllers;

/// <summary>
/// ثبت و گزارش بازدید.
///
/// سیاست معیار جهانی (Google Analytics):
/// • صفحه‌بازدید (Page View) = هر بار دیدن یک صفحه → VisitLog
/// • بازدید (Visit/Session)  = همه صفحاتی که یک بازدیدکننده در بازه ۳۰ دقیقه بدون وقفه ببیند → VisitSessions
/// • بازدیدکننده یکتا       = شمارش یکتای هویت (کاربر واردشده یا شناسه مرورگر مهمان)
/// هر سه عدد داشبورد و صفحه آمار از همین منابع خوانده می‌شوند تا همیشه یکسان باشند.
/// </summary>
[ApiController]
[Route("api/visits")]
public sealed class VisitsController : ControllerBase
{
    private static readonly string[] ExcludedPrefixes =
    {
        "/admin", "/api", "/hubs", "/_framework", "/_blazor", "/favicon",
        "/service-worker", "/css/", "/js/", "/vendor/", "/images/", "/uploads/",
        "/monitoring-data/", "/bale-debug/", "/_vs/"
    };

    private static readonly string[] ExcludedExtensions =
    {
        ".js", ".css", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".ico",
        ".woff", ".woff2", ".ttf", ".map", ".json", ".txt", ".xml", ".wasm",
        ".zip", ".pdf", ".csv", ".mp4", ".webm"
    };

    private readonly VisitTrackingService _tracker;
    private readonly ErrorServiceDbContext _db;

    /// <summary>ثبت بازدید از localhost (فقط برای توسعه) — در محیط اصلی خاموش است.</summary>
    private readonly bool _trackLoopback;

    public VisitsController(VisitTrackingService tracker, ErrorServiceDbContext db, IConfiguration configuration)
    {
        _tracker = tracker;
        _db = db;
        _trackLoopback = configuration.GetValue("Visits:TrackLoopback", false);
    }

    // ───────────────── ثبت بازدید (عمومی، بدون I/O دیتابیس در مسیر درخواست) ─────────────────

    [AllowAnonymous]
    [HttpPost("track")]
    public IActionResult Track([FromBody] VisitTrackRequest request)
    {
        var path = NormalizePath(request.Path);
        if (string.IsNullOrEmpty(path) || path.Length > 300)
            return NoContent();

        if (IsExcluded(path))
            return NoContent();

        // ترافیک لوکالِ بدون پراکسی (محیط توسعه/خود سرور) جزو بازدید واقعی سایت نیست
        // (مگر اینکه Visits:TrackLoopback در توسعه فعال شده باشد)
        if (IsLocalWithoutProxy())
            return NoContent();

        var visitorId = (request.VisitorId ?? "").Trim();
        if (visitorId.Length > 36) visitorId = visitorId[..36];
        if (string.IsNullOrEmpty(visitorId)) visitorId = "anon";

        var identity = ResolveIdentity(visitorId);

        var capture = new VisitCapture(
            Ip: GetClientIp(),
            Path: path,
            Referrer: Truncate((request.Referrer ?? "").Trim(), 500),
            UserAgent: Truncate(Request.Headers.UserAgent.ToString(), 400),
            VisitorId: visitorId,
            VisitedAtUtc: DateTime.UtcNow,
            IdentityKey: identity.Key,
            UserName: identity.Name,
            HasUserIdentity: identity.HasUser);

        _tracker.TryEnqueue(capture);
        return NoContent();
    }

    /// <summary>
    /// هویت بازدیدکننده: کاربر واردشده با نامش ثبت می‌شود، مهمان با شناسه مرورگر.
    /// نام از توکن JWT (سمت سرور) خوانده می‌شود؛ هیچ اطلاعاتی از سمت کلاینت قابل جعل نیست.
    /// </summary>
    private (string Key, string Name, bool HasUser) ResolveIdentity(string visitorId)
    {
        if (User?.Identity?.IsAuthenticated != true)
            return ($"v{visitorId}", "", false);

        var name = User.FindFirstValue(ClaimTypes.Name) ?? "";

        var appId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(appId) && int.TryParse(appId, out _))
            return ($"a{appId}", name, true);

        var workshopUserId = User.FindFirstValue("workshop_user_id");
        if (!string.IsNullOrEmpty(workshopUserId) && int.TryParse(workshopUserId, out _))
            return ($"w{workshopUserId}", name, true);

        return ($"v{visitorId}", "", false);
    }

    /// <summary>مسیر بدون پارامتر کوئری/هش — جلوگیری از شمردن یک صفحه چند بار با query متفاوت.</summary>
    private static string NormalizePath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        var p = raw.Trim();
        var cut = p.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0) p = p[..cut];
        if (string.IsNullOrEmpty(p)) return "/";
        if (p[0] != '/') p = "/" + p;
        return p;
    }

    private bool IsLocalWithoutProxy()
    {
        if (_trackLoopback) return false;
        if (!string.IsNullOrWhiteSpace(Request.Headers["X-Forwarded-For"])) return false;
        var remote = HttpContext.Connection.RemoteIpAddress;
        return remote != null && IPAddress.IsLoopback(remote);
    }

    private bool IsExcluded(string path)
    {
        foreach (var prefix in ExcludedPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }

        var q = path.IndexOfAny(new[] { '?', '#' });
        var clean = q >= 0 ? path[..q] : path;
        foreach (var ext in ExcludedExtensions)
        {
            if (clean.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private string GetClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first)) return first;
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Length <= max ? value : value[..max];
    }

    // ───────────────── گزارش‌های ادمین ─────────────────

    [Authorize(Policy = "perm:admin.analytics.view")]
    [HttpGet("summary")]
    public async Task<ActionResult<VisitSummaryDto>> GetSummary()
    {
        var (today, weekStart, monthStart, yearStart) = GetPeriodStarts();

        // امروز: همیشه زنده از جدول جلسه‌ها (ایندکس‌دار)
        var todayVisits = await _db.VisitSessions
            .CountAsync(x => x.VisitDate == today && !x.IsBot);
        var todayUniques = await _db.VisitSessions
            .Where(x => x.VisitDate == today && !x.IsBot)
            .Select(x => x.IdentityKey)
            .Distinct()
            .CountAsync();
        var todayPageViews = await _db.VisitLogs
            .CountAsync(x => x.VisitDate == today && !x.IsBot);

        // هفته و ماه: کاملاً از جلسه‌ها (زنده و دقیق، شامل امروز)
        var weekVisits = await _db.VisitSessions
            .CountAsync(x => x.VisitDate >= weekStart && x.VisitDate <= today && !x.IsBot);
        var weekUniques = await _db.VisitSessions
            .Where(x => x.VisitDate >= weekStart && x.VisitDate <= today && !x.IsBot)
            .Select(x => x.IdentityKey)
            .Distinct()
            .CountAsync();

        var monthVisits = await _db.VisitSessions
            .CountAsync(x => x.VisitDate >= monthStart && x.VisitDate <= today && !x.IsBot);
        var monthUniques = await _db.VisitSessions
            .Where(x => x.VisitDate >= monthStart && x.VisitDate <= today && !x.IsBot)
            .Select(x => x.IdentityKey)
            .Distinct()
            .CountAsync();

        // سال و کل: از آمار تجمیعی (روزهای قبل از امروز) + امروز زنده
        var yearBefore = await _db.DailyVisitStats
            .Where(x => x.Date >= yearStart && x.Date < today)
            .SumAsync(x => (int?)x.Visits) ?? 0;
        var yearVisits = yearBefore + todayVisits;

        var totalBefore = await _db.DailyVisitStats
            .Where(x => x.Date < today)
            .SumAsync(x => (int?)x.Visits) ?? 0;
        var totalVisits = totalBefore + todayVisits;

        var activeDays = await _db.VisitSessions
            .Where(x => !x.IsBot)
            .Select(x => x.VisitDate)
            .Distinct()
            .CountAsync();

        return Ok(new VisitSummaryDto
        {
            TodayVisits = todayVisits,
            TodayUniques = todayUniques,
            TodayPageViews = todayPageViews,
            WeekVisits = weekVisits,
            WeekUniques = weekUniques,
            MonthVisits = monthVisits,
            MonthUniques = monthUniques,
            YearVisits = yearVisits,
            TotalVisits = totalVisits,
            ActiveDays = activeDays
        });
    }

    [Authorize(Policy = "perm:admin.analytics.view")]
    [HttpGet("list")]
    public async Task<ActionResult<VisitListResponse>> GetList(
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330));
        var fromDate = DateOnly.TryParse(from, out var f) ? f : today.AddDays(-29);
        var toDate = DateOnly.TryParse(to, out var t) ? t : today;
        if (toDate < fromDate) (fromDate, toDate) = (toDate, fromDate);

        var query = _db.VisitSessions.AsNoTracking()
            .Where(x => x.VisitDate >= fromDate && x.VisitDate <= toDate && !x.IsBot);

        var searchTrimmed = (search ?? "").Trim();
        if (!string.IsNullOrEmpty(searchTrimmed))
        {
            query = query.Where(x =>
                x.Ip.Contains(searchTrimmed) ||
                x.EntryPath.Contains(searchTrimmed) ||
                x.LastPath.Contains(searchTrimmed) ||
                x.UserName.Contains(searchTrimmed) ||
                x.IdentityKey.Contains(searchTrimmed) ||
                x.VisitorId.Contains(searchTrimmed));
        }

        var total = await query.CountAsync();
        var totalPages = await query.SumAsync(x => (int?)x.PageViews) ?? 0;

        var rows = await query
            .OrderByDescending(x => x.StartedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.StartedAtUtc,
                x.EndedAtUtc,
                x.Ip,
                x.EntryPath,
                x.LastPath,
                x.Referrer,
                x.UserAgent,
                x.VisitorId,
                x.UserName,
                x.IdentityKey,
                x.PageViews
            })
            .ToListAsync();

        // زمان تهران و مدت جلسه در حافظه محاسبه می‌شود — ترجمهٔ DateTime±/TotalMinutes در پروجکشن EF قابل ترجمه نیست
        var items = rows.Select(x => new VisitListItemDto
        {
            Id = x.Id,
            VisitedAtUtc = x.StartedAtUtc,
            VisitedAtLocal = x.StartedAtUtc.AddMinutes(330),
            EndedAtLocal = x.EndedAtUtc.AddMinutes(330),
            DurationMinutes = (int)(x.EndedAtUtc - x.StartedAtUtc).TotalMinutes,
            Ip = x.Ip,
            Path = x.EntryPath,
            LastPath = x.LastPath,
            Referrer = x.Referrer,
            UserAgent = x.UserAgent,
            VisitorId = x.VisitorId,
            UserName = x.UserName,
            IdentityKey = x.IdentityKey,
            PageViews = x.PageViews
        }).ToList();

        // نام فارسی صفحات (ورودی و آخرین) از نقشهٔ مسیرهای ثابت + نام‌های دیتابیس
        var titles = await VisitPathNamer.ResolveAsync(_db,
            items.Select(x => x.Path).Concat(items.Select(x => x.LastPath)));

        foreach (var item in items)
        {
            if (!string.IsNullOrEmpty(item.Path) && titles.TryGetValue(item.Path, out var entryTitle))
                item.PathTitle = entryTitle;
            if (!string.IsNullOrEmpty(item.LastPath) && titles.TryGetValue(item.LastPath, out var lastTitle))
                item.LastPathTitle = lastTitle;
        }

        return Ok(new VisitListResponse { Total = total, TotalPageViews = totalPages, Items = items });
    }

    [Authorize(Policy = "perm:admin.analytics.view")]
    [HttpGet("trend")]
    public async Task<ActionResult<VisitTrendResponse>> GetTrend([FromQuery] int days = 30)
    {
        if (days < 7) days = 7;
        if (days > 180) days = 180;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330));
        var start = today.AddDays(-(days - 1));

        var stats = await _db.DailyVisitStats.AsNoTracking()
            .Where(x => x.Date >= start && x.Date < today)
            .ToListAsync();
        var statsMap = stats.ToDictionary(x => x.Date, x => x);

        var todayVisits = await _db.VisitSessions
            .CountAsync(x => x.VisitDate == today && !x.IsBot);
        var todayUniques = await _db.VisitSessions
            .Where(x => x.VisitDate == today && !x.IsBot)
            .Select(x => x.IdentityKey)
            .Distinct()
            .CountAsync();

        var points = new List<VisitTrendPointDto>(days);
        for (var d = start; d <= today; d = d.AddDays(1))
        {
            int visits, uniques;
            if (d == today)
            {
                visits = todayVisits;
                uniques = todayUniques;
            }
            else if (statsMap.TryGetValue(d, out var row))
            {
                visits = row.Visits;
                uniques = row.Uniques;
            }
            else
            {
                visits = 0;
                uniques = 0;
            }

            points.Add(new VisitTrendPointDto
            {
                Date = d.ToString("yyyy-MM-dd"),
                Visits = visits,
                Uniques = uniques
            });
        }

        return Ok(new VisitTrendResponse { Points = points });
    }

    /// <summary>
    /// پربحترین صفحات (بیشترین «پاخور») — از آمار تجمیعی مسیرها (همیشگی) خوانده می‌شود.
    /// هر ردیف یک مسیر است؛ درصد سهم از کل صفحه‌بازدیدهای همان بازه محاسبه می‌شود.
    /// </summary>
    [Authorize(Policy = "perm:admin.analytics.view")]
    [HttpGet("paths")]
    public async Task<ActionResult<VisitPathsResponse>> GetPaths([FromQuery] int days = 30, [FromQuery] int take = 15)
    {
        if (days < 1) days = 30;
        if (days > 365) days = 365;
        if (take < 1) take = 15;
        if (take > 100) take = 100;

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330));
        var start = today.AddDays(-(days - 1));

        var rows = await _db.DailyPagePathStats.AsNoTracking()
            .Where(x => x.Date >= start && x.Date <= today)
            .GroupBy(x => x.Path)
            .Select(g => new { Path = g.Key, PageViews = g.Sum(x => x.PageViews) })
            .OrderByDescending(x => x.PageViews)
            .ToListAsync();

        var total = rows.Sum(x => x.PageViews);

        var items = rows.Take(take).Select(x => new VisitPathStatDto
        {
            Path = x.Path,
            PageViews = x.PageViews,
            Share = total > 0 ? Math.Round(x.PageViews * 100.0 / total, 1) : 0
        }).ToList();

        // نام فارسی هر صفحه برای نمایش در گزارش (کلیک روی ردیف، خود آدرس را در تب جدید باز می‌کند)
        var titles = await VisitPathNamer.ResolveAsync(_db, items.Select(x => x.Path));
        foreach (var item in items)
        {
            if (titles.TryGetValue(item.Path, out var title))
                item.Title = title;
        }

        return Ok(new VisitPathsResponse { Days = days, TotalPageViews = total, Items = items });
    }

    /// <summary>شروع امروز/هفته (شنبه)/ماه شمسی/سال شمسی — بر مبنای وقت تهران.</summary>
    private static (DateOnly Today, DateOnly WeekStart, DateOnly MonthStart, DateOnly YearStart) GetPeriodStarts()
    {
        var nowLocal = DateTime.UtcNow.AddMinutes(330); // UTC+3:30
        var today = DateOnly.FromDateTime(nowLocal);

        // هفته ایرانی: شنبه‌تاپنجشنبه
        var daysSinceSaturday = ((int)nowLocal.DayOfWeek + 1) % 7;
        var weekStart = today.AddDays(-daysSinceSaturday);

        var pc = new PersianCalendar();
        var monthStart = DateOnly.FromDateTime(pc.ToDateTime(pc.GetYear(nowLocal), pc.GetMonth(nowLocal), 1, 0, 0, 0, 0));
        var yearStart = DateOnly.FromDateTime(pc.ToDateTime(pc.GetYear(nowLocal), 1, 1, 0, 0, 0, 0));

        return (today, weekStart, monthStart, yearStart);
    }
}
