using ErrorService.Shared;
using Microsoft.JSInterop;

namespace ErrorService.Client.Services;

public enum CompareToggleResult
{
    Added,
    Removed,
    LimitReached
}

public sealed class CompareItem
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ImageUrl { get; set; }
}

/// <summary>
/// لیست مقایسه محصولات؛ سقف اقلام از تنظیمات فروشگاه می‌آید (پیش‌فرض ۴، حداکثر ۶)،
/// ذخیره‌شده در localStorage. هیچ داده‌ای به سرور ارسال نمی‌شود.
/// </summary>
public sealed class CompareService
{
    public const int DefaultMaxItems = 4;
    public const int HardMaxItems = 6;

    private const string StorageKey = "compare_products";

    private readonly IJSRuntime _js;
    private List<CompareItem>? _items;
    private bool _initialized;
    private Task<List<CompareItem>>? _loadingTask;

    public event Action? OnChange;

    public CompareService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<List<CompareItem>> GetItemsAsync()
    {
        if (_initialized) return _items ??= new();

        // فقط یک‌بار خواندن از localStorage، حتی با رندر هم‌زمان چند کارت.
        if (_loadingTask != null) return await _loadingTask;

        _loadingTask = LoadAsync();
        try { return await _loadingTask; }
        finally { _loadingTask = null; }
    }

    public async Task<bool> ContainsAsync(int productId)
    {
        var items = await GetItemsAsync();
        return items.Any(x => x.ProductId == productId);
    }

    public async Task<CompareToggleResult> ToggleAsync(ProductDto product, int maxItems = DefaultMaxItems)
    {
        var limit = Math.Clamp(maxItems, 2, HardMaxItems);
        var items = await GetItemsAsync();
        var existing = items.FirstOrDefault(x => x.ProductId == product.Id);

        if (existing != null)
        {
            items.Remove(existing);
            await PersistAsync(items);
            return CompareToggleResult.Removed;
        }

        if (items.Count >= limit)
            return CompareToggleResult.LimitReached;

        items.Add(new CompareItem
        {
            ProductId = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            ImageUrl = product.MainImageUrl
        });
        await PersistAsync(items);
        return CompareToggleResult.Added;
    }

    public async Task RemoveAsync(int productId)
    {
        var items = await GetItemsAsync();
        if (items.RemoveAll(x => x.ProductId == productId) == 0) return;
        await PersistAsync(items);
    }

    public async Task ClearAsync()
    {
        var items = await GetItemsAsync();
        if (items.Count == 0) return;
        items.Clear();
        await PersistAsync(items);
    }

    private async Task<List<CompareItem>> LoadAsync()
    {
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        _items = !string.IsNullOrEmpty(json)
            ? System.Text.Json.JsonSerializer.Deserialize<List<CompareItem>>(json) ?? new()
            : new();
        _initialized = true;
        return _items;
    }

    private async Task PersistAsync(List<CompareItem> items)
    {
        _items = items;
        var json = System.Text.Json.JsonSerializer.Serialize(items);
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        OnChange?.Invoke();
    }
}
