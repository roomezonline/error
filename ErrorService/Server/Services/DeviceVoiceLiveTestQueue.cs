using System.Collections.Concurrent;

namespace ErrorService.Server.Services;

/// <summary>
/// صف درون‌حافظه‌ای «پخش آزمایشی زنده» برای دستگاه‌های ESP32.
/// برخلاف رویدادهای واقعی پایش، این موارد هیچ ردی در تاریخچهٔ صدا ثبت نمی‌کنند؛
/// فقط چند ثانیه در حافظه می‌مانند تا دستگاه در اولین long-poll آن‌ها را بگیرد.
/// </summary>
public sealed class DeviceVoiceLiveTestQueue
{
    /// <summary>عمر مفید هر درخواست آزمایشی؛ بعد از این مدت هرگز پخش نمی‌شود.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<int, ConcurrentQueue<DeviceVoiceLiveTestItem>> _queues = new();

    /// <summary>یک درخواست آزمایشی را برای دستگاه در صف می‌گذارد و تعداد صف را برمی‌گرداند.</summary>
    public int Enqueue(int deviceId, DeviceVoiceLiveTestItem item)
    {
        var queue = _queues.GetOrAdd(deviceId, _ => new ConcurrentQueue<DeviceVoiceLiveTestItem>());
        queue.Enqueue(item);
        return queue.Count;
    }

    /// <summary>
    /// موارد معتبر همان دستگاه را برمی‌گرداند و از صف خارج می‌کند؛
    /// موارد منقضی‌شده بدون برگشت حذف می‌شوند تا صدای کهنه پخش نشود.
    /// </summary>
    public List<DeviceVoiceLiveTestItem> Drain(int deviceId, DateTimeOffset now)
    {
        var result = new List<DeviceVoiceLiveTestItem>();
        if (!_queues.TryGetValue(deviceId, out var queue)) return result;

        while (queue.TryDequeue(out var item))
        {
            if (now - item.EnqueuedAt <= Lifetime)
                result.Add(item);
        }

        // حذف اتمیک فقط وقتی صف همچنان خالی و همان نمونه است؛
        // تا درخواست هم‌زمانی که همین حالا Enqueue کرده گم نشود.
        if (queue.IsEmpty)
            _queues.TryRemove(new KeyValuePair<int, ConcurrentQueue<DeviceVoiceLiveTestItem>>(deviceId, queue));

        return result;
    }
}

/// <summary>یک درخواست پخش آزمایشی که فقط در حافظهٔ سرور زندگی می‌کند.</summary>
public sealed record DeviceVoiceLiveTestItem(
    string ActionKey,
    string TitleFa,
    int RepeatCount,
    int Priority,
    string AudioUrl,
    string Format,
    long Version,
    DateTimeOffset EnqueuedAt);
