using ErrorService.Shared;
using System.Net.Http.Json;
using System.Text;

namespace ErrorService.Client.Services;

public sealed class NotificationCenterService
{
    private readonly HttpClient _http;

    public NotificationCenterService(HttpClient http) => _http = http;

    public int UnreadCount { get; private set; }
    public List<NotificationDto> Recent { get; private set; } = new();
    public event Action? OnChange;

    public async Task RefreshAsync()
    {
        try
        {
            var summary = await _http.GetFromJsonAsync<NotificationSummaryDto>("api/notifications/summary");
            var recent = await _http.GetFromJsonAsync<List<NotificationDto>>("api/notifications?take=8");
            UnreadCount = summary?.UnreadCount ?? 0;
            Recent = recent ?? new();
        }
        catch
        {
            UnreadCount = 0;
            Recent = new();
        }
        OnChange?.Invoke();
    }

    public async Task<List<NotificationDto>> LoadAsync(int take = 30, bool unreadOnly = false)
    {
        try
        {
            var url = $"api/notifications?take={take}" + (unreadOnly ? "&unreadOnly=true" : string.Empty);
            return await _http.GetFromJsonAsync<List<NotificationDto>>(url) ?? new();
        }
        catch
        {
            return new();
        }
    }

    public async Task MarkReadAsync(long id)
    {
        try { await _http.PutAsync($"api/notifications/{id}/read", null); } catch { }
        await RefreshAsync();
    }

    public async Task<int> MarkAllReadAsync()
    {
        var count = 0;
        try
        {
            var resp = await _http.PutAsync("api/notifications/read-all", null);
            if (resp.IsSuccessStatusCode)
                count = await resp.Content.ReadFromJsonAsync<int>();
        }
        catch { }
        await RefreshAsync();
        return count;
    }

    public async Task HideAsync(long id)
    {
        try { await _http.DeleteAsync($"api/notifications/{id}"); } catch { }
        await RefreshAsync();
    }

    public static string RelativeTime(DateTimeOffset dateTime)
    {
        var diff = DateTimeOffset.UtcNow - dateTime.ToUniversalTime();
        if (diff < TimeSpan.Zero) diff = TimeSpan.Zero;
        if (diff.TotalMinutes < 1) return "همین حالا";
        if (diff.TotalMinutes < 60) return $"{PersianDigits((int)diff.TotalMinutes)} دقیقه پیش";
        if (diff.TotalHours < 24) return $"{PersianDigits((int)diff.TotalHours)} ساعت پیش";
        if (diff.TotalDays < 7) return $"{PersianDigits((int)diff.TotalDays)} روز پیش";
        return ErrorService.Client.Utils.PersianDate.ToJalaliDate(dateTime);
    }

    private static string PersianDigits(int value)
    {
        var s = value.ToString();
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(ch >= '0' && ch <= '9' ? (char)('۰' + (ch - '0')) : ch);
        return sb.ToString();
    }
}
