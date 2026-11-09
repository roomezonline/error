using System.Collections.Concurrent;
using ErrorService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

/// <summary>
/// مدت روشن‌بودن هر تجهیز تا لحظهٔ خاموش‌شدن. هنگام ثبت رویداد و هم هنگام
/// خواندن تاریخچه استفاده می‌شود تا رکوردهای قدیمی هم کارکرد داشته باشند.
/// </summary>
public static class MonitoringRuntimeResolver
{
    // تاریخچه تغییرات بعد از ثبت تغییری نمی‌کند، بنابراین کارکرد هر رکورد
    // یک‌بار محاسبه و کش می‌شود؛ پنل جزئیات هر چند ثانیه تاریخچه را دوباره می‌خواند.
    private static readonly ConcurrentDictionary<int, (long Seconds, string? StartedAtFa)> RuntimeCache = new();
    private const int RuntimeCacheLimit = 20000;

    public static bool IsOnValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();
        return trimmed.Equals("on", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("1", StringComparison.Ordinal)
            || trimmed.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// تشخیص مقدار معتبر روشن/خاموش. مقدار خالی یا ناشناخته (مثل «nan» یا رشتهٔ
    /// ناقص) معتبر نیست تا قطعیِ موقتِ ارتباط، تغییر کاذب و پخش صدای اشتباه نسازد.
    /// </summary>
    public static bool TryIsOnValue(string? value, out bool isOn)
    {
        isOn = false;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var trimmed = value.Trim();
        if (trimmed.Equals("on", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("1", StringComparison.Ordinal)
            || trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            isOn = true;
            return true;
        }

        if (trimmed.Equals("off", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("0", StringComparison.Ordinal)
            || trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public static bool IsStopRecord(string? oldValue, string? newValue) =>
        IsOnValue(oldValue) && !IsOnValue(newValue);

    public static async Task<(long Seconds, string? StartedAtFa)?> ResolveAsync(
        ErrorServiceDbContext db, int? monitoringId, string changeType, DateTime stoppedAt)
    {
        if (!monitoringId.HasValue) return null;

        var previous = await db.MonitoringDeviceChangeLogs
            .AsNoTracking()
            .Where(x => x.MonitoringId == monitoringId
                        && x.ChangeType == changeType
                        && x.CreatedAt < stoppedAt)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new { x.CreatedAt, x.CreatedAtFa, x.NewValue })
            .FirstOrDefaultAsync();

        if (previous is null || !IsOnValue(previous.NewValue))
            return null;

        var seconds = (long)(stoppedAt - previous.CreatedAt).TotalSeconds;
        return seconds > 0 ? (seconds, previous.CreatedAtFa) : null;
    }

    public static async Task<Dictionary<int, (long Seconds, string? StartedAtFa)>> ResolveStopsAsync(
        ErrorServiceDbContext db,
        IEnumerable<(int Id, int? MonitoringId, string ChangeType, string? OldValue, string? NewValue, DateTime CreatedAt)> rows)
    {
        var result = new Dictionary<int, (long Seconds, string? StartedAtFa)>();

        foreach (var row in rows)
        {
            if (!IsStopRecord(row.OldValue, row.NewValue)) continue;

            if (RuntimeCache.TryGetValue(row.Id, out var cached))
            {
                if (cached.Seconds > 0) result[row.Id] = cached;
                continue;
            }

            var runtime = await ResolveAsync(db, row.MonitoringId, row.ChangeType, row.CreatedAt);

            if (RuntimeCache.Count < RuntimeCacheLimit)
                RuntimeCache[row.Id] = runtime ?? (0, null);

            if (runtime.HasValue) result[row.Id] = runtime.Value;
        }

        return result;
    }
}
