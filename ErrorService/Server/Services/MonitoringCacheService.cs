using System.Collections.Concurrent;
using ErrorService.Shared.Models;

namespace ErrorService.Server.Services;

public class MonitoringCacheService
{
    private readonly ConcurrentDictionary<string, MonitoringDeviceData> _cache = new();
    private readonly ConcurrentDictionary<string, object> _locks = new();

    public void Set(string code, MonitoringDeviceData data) =>
        _cache[code] = data;

    public MonitoringDeviceData? Get(string code) =>
        _cache.TryGetValue(code, out var data) ? data : null;

    public MonitoringDeviceData? Exchange(string code, MonitoringDeviceData newData)
    {
        lock (_locks.GetOrAdd(code, _ => new object()))
        {
            _cache.TryGetValue(code, out var old);
            _cache[code] = newData;
            return old;
        }
    }

    public void Remove(string code)
    {
        _cache.TryRemove(code, out _);
        _locks.TryRemove(code, out _);
    }
}
