using System.Net;
using System.Net.Http.Json;
using ErrorService.Shared;

namespace ErrorService.Client.Services;

public enum StockAlertToggleResult
{
    Added,
    Removed,
    Unauthorized,
    Failed
}

/// <summary>
/// وضعیت «خبرم کن» مشتری برای کالاهای ناموجود؛ یک‌بار از سرور خوانده می‌شود
/// و همهٔ کارت‌های محصول از همین نمونه استفاده می‌کنند.
/// </summary>
public sealed class StockAlertService
{
    private readonly HttpClient _http;
    private readonly HashSet<int> _activeIds = new();
    private bool _loaded;
    private Task? _loadingTask;

    public event Action? OnChange;

    public StockAlertService(HttpClient http)
    {
        _http = http;
    }

    public bool Contains(int productId) => _activeIds.Contains(productId);

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;

        if (_loadingTask == null)
            _loadingTask = LoadAsync();

        await _loadingTask;
    }

    private async Task LoadAsync()
    {
        try
        {
            var mine = await _http.GetFromJsonAsync<StockAlertMineDto>("api/stock-alerts/mine");
            _activeIds.Clear();
            foreach (var id in mine?.ActiveProductIds ?? new List<int>())
                _activeIds.Add(id);
        }
        catch
        {
            // مهمان یا قطع ارتباط: لیست خالی می‌ماند و کاربر هنگام کلیک به ورود هدایت می‌شود.
        }
        finally
        {
            _loaded = true;
            _loadingTask = null;
        }
    }

    public async Task<StockAlertToggleResult> ToggleAsync(int productId)
    {
        try
        {
            var isOn = _activeIds.Contains(productId);
            using var resp = isOn
                ? await _http.DeleteAsync($"api/stock-alerts/{productId}")
                : await _http.PostAsync($"api/stock-alerts/{productId}", null);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return StockAlertToggleResult.Unauthorized;

            if (!resp.IsSuccessStatusCode)
                return StockAlertToggleResult.Failed;

            if (isOn) _activeIds.Remove(productId);
            else _activeIds.Add(productId);

            OnChange?.Invoke();
            return isOn ? StockAlertToggleResult.Removed : StockAlertToggleResult.Added;
        }
        catch
        {
            return StockAlertToggleResult.Failed;
        }
    }
}
