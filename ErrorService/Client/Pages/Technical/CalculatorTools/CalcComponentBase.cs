using System.Text.Json;
using ErrorService.Client.Services;
using Microsoft.AspNetCore.Components;

namespace ErrorService.Client.Pages.Technical.CalculatorTools;

/// <summary>
/// پایه مشترک ابزارهای صفحه محاسبات: ورودی‌ها در localStorage همان مرورگر
/// ذخیره می‌شوند تا تعمیرکار با رفرش صفحه یا مراجعه بعدی، مقادیر را از دست ندهد.
/// </summary>
public abstract class CalcComponentBase : ComponentBase
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Inject] protected LocalStorageService Store { get; set; } = default!;

    protected abstract string StorageKey { get; }

    private bool _restored;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _restored) return;
        _restored = true;

        try
        {
            var json = await Store.GetAsync("es.calc." + StorageKey);
            if (!string.IsNullOrWhiteSpace(json) && ApplyState(json))
                StateHasChanged();
        }
        catch
        {
            // localStorage ممکن است در حالت‌های خصوصی مرورگر در دسترس نباشد.
        }
    }

    protected abstract bool ApplyState(string json);

    protected abstract string CaptureState();

    protected async Task PersistAsync()
    {
        try { await Store.SetAsync("es.calc." + StorageKey, CaptureState()); }
        catch { }
    }
}
