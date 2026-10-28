using System.Net.Http.Json;
using ErrorService.Shared;

namespace ErrorService.Client.Services;

/// <summary>
/// کش مشترک تنظیمات سایت در سمت کلاینت. کامپوننت‌های صفحه اصلی به‌جای
/// فراخوانی مستقل api/sitesettings از این سرویس استفاده می‌کنند تا هم تعداد
/// درخواست‌ها کم شود و هم محتوا یک‌بار و هم‌زمان رندر شود.
/// </summary>
public sealed class SiteSettingsService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private SiteSettingsDto? _cached;
    private DateTime _cachedAt = DateTime.MinValue;
    private DateTime _lastAttemptAt = DateTime.MinValue;
    private Task<SiteSettingsDto?>? _loading;

    public SiteSettingsService(HttpClient http)
    {
        _http = http;
    }

    public SiteSettingsDto? Current => _cached;

    public async Task<SiteSettingsDto?> GetAsync(bool force = false)
    {
        if (!force && _cached != null && DateTime.UtcNow - _cachedAt < CacheDuration)
            return _cached;

        // Join the request that is already running before applying the negative
        // cache: LoadAsync stamps _lastAttemptAt immediately, so without this
        // order every component initialized during the first fetch got null and
        // stayed hidden until the next navigation (e.g. the home error search).
        if (_loading is { } inflight)
            return await inflight;

        // Negative cache: avoid a burst of retries when the API is unavailable.
        if (!force && _cached == null && DateTime.UtcNow - _lastAttemptAt < RetryDelay)
            return null;

        var task = LoadAsync();
        _loading = task;
        try
        {
            return await task;
        }
        finally
        {
            if (ReferenceEquals(_loading, task))
                _loading = null;
        }
    }

    public void Invalidate()
    {
        _cached = null;
        _cachedAt = DateTime.MinValue;
        _lastAttemptAt = DateTime.MinValue;
    }

    private async Task<SiteSettingsDto?> LoadAsync()
    {
        _lastAttemptAt = DateTime.UtcNow;
        try
        {
            var settings = await _http.GetFromJsonAsync<SiteSettingsDto>("api/sitesettings");
            if (settings != null)
            {
                _cached = settings;
                _cachedAt = DateTime.UtcNow;
            }

            return settings ?? _cached;
        }
        catch
        {
            // Keep the previous snapshot (if any) so sections do not flicker.
            return _cached;
        }
    }
}
