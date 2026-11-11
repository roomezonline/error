using System.Collections.Concurrent;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

/// <summary>
/// پل رویداد «تغییر تجهیزات پایش» به «آوای دستگاه»:
/// - جلوگیری از پخش تکراری هنگام نوسان سریع یک تجهیز (cooldown)
/// - ساخت رویداد فقط برای اکشن‌هایی که واقعاً برای همان دستگاه ضبط شده‌اند
/// - انقضای کوتاه رویدادها تا صدای کهنه بعد از قطعی دستگاه پخش نشود
/// </summary>
public sealed class DeviceVoiceEventService
{
    /// <summary>در این بازه، تغییر مجدد همان تجهیز رویداد جدید نمی‌سازد.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(3);

    /// <summary>رویداد بعد از این مدت کهنه می‌شود و پخش نمی‌شود.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(120);

    private readonly ConcurrentDictionary<string, long> _lastEventAt = new(StringComparer.Ordinal);

    public bool TryAcquire(int deviceId, string actionKey)
    {
        var key = deviceId.ToString() + ":" + actionKey;
        var now = Environment.TickCount64;
        var window = (long)Cooldown.TotalMilliseconds;

        while (true)
        {
            if (!_lastEventAt.TryGetValue(key, out var last))
            {
                if (_lastEventAt.TryAdd(key, now)) return true;
                continue;
            }

            if (now - last < window) return false;
            if (_lastEventAt.TryUpdate(key, now, last)) return true;
        }
    }

    /// <summary>
    /// رویدادهای متناظر با تغییرات را به DbContext اضافه می‌کند (SaveChanges با فراخواننده است).
    /// فقط اکشن فعال و دارای فایل MP3 برای همان دستگاه رویداد می‌سازد.
    /// </summary>
    public async Task<int> AddEventsAsync(
        ErrorServiceDbContext db,
        MonitoringDevice device,
        IReadOnlyList<(string Type, string? OldValue, string? NewValue, string Description)> changes,
        DateTimeOffset now,
        bool isTest,
        CancellationToken ct)
    {
        if (changes.Count == 0) return 0;

        var actions = await db.ServiceVoiceActions.AsNoTracking()
            .Where(a => a.MonitoringDeviceId == device.Id &&
                        a.IsActive &&
                        a.AudioUrl != null &&
                        a.AudioUrl != "")
            .Select(a => new { a.Key, a.Priority })
            .ToListAsync(ct);

        if (actions.Count == 0) return 0;

        var priorityByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in actions.OrderBy(a => a.Priority).ThenBy(a => a.Key, StringComparer.Ordinal))
            priorityByKey.TryAdd(action.Key, action.Priority);

        // اگر چند تجهیز در یک گزارش هم‌زمان عوض شوند، پخش به ترتیب اولویت اکشن‌ها
        // انجام می‌شود تا مهم‌ترین صدا اول شنیده شود و صف قطع نشود.
        var matched = new List<(int Priority, string ActionKey, (string Type, string? OldValue, string? NewValue, string Description) Change)>();
        foreach (var change in changes)
        {
            var actionKey = ServiceVoiceEventKeys.Resolve(change.Type, MonitoringRuntimeResolver.IsOnValue(change.NewValue));
            if (actionKey == null) continue;
            if (!priorityByKey.TryGetValue(actionKey, out var priority)) continue;
            matched.Add((priority, actionKey, change));
        }

        var added = 0;
        foreach (var item in matched.OrderBy(x => x.Priority).ThenBy(x => x.ActionKey, StringComparer.Ordinal))
        {
            if (!TryAcquire(device.Id, item.ActionKey)) continue;

            db.DeviceVoiceEvents.Add(new DeviceVoiceEvent
            {
                MonitoringDeviceId = device.Id,
                DeviceCode = device.DeviceNumber,
                ActionKey = item.ActionKey,
                SourceType = item.Change.Type,
                OldValue = Truncate(item.Change.OldValue, 50),
                NewValue = Truncate(item.Change.NewValue, 50),
                DescriptionFa = Truncate(item.Change.Description, 200),
                IsTest = isTest,
                CreatedAt = now,
                ExpiresAt = now.Add(Lifetime)
            });
            added++;
        }

        return added;
    }

    private static string? Truncate(string? value, int max) =>
        value == null || value.Length <= max ? value : value[..max];
}
