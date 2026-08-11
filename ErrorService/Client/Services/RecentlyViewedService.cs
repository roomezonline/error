using Microsoft.JSInterop;
using System.Text.Json;
using ErrorService.Shared;

namespace ErrorService.Client.Services;

public sealed class RecentlyViewedService
{
    private readonly IJSRuntime _js;
    private const string StorageKey = "recently_viewed";
    private const int MaxItems = 10;
    private List<RecentlyViewedItem>? _items;
    private bool _initialized;

    public RecentlyViewedService(IJSRuntime js) => _js = js;

    public async Task<List<RecentlyViewedItem>> GetItemsAsync()
    {
        await InitAsync();
        return _items?.ToList() ?? new();
    }

    public async Task AddAsync(RecentlyViewedItem item)
    {
        await InitAsync();

        _items!.RemoveAll(x => x.Id == item.Id);
        _items.Insert(0, item);
        if (_items.Count > MaxItems)
            _items.RemoveAt(_items.Count - 1);

        await SaveAsync();
    }

    private async Task InitAsync()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            _items = string.IsNullOrEmpty(json)
                ? new()
                : JsonSerializer.Deserialize<List<RecentlyViewedItem>>(json) ?? new();
        }
        catch
        {
            _items = new();
        }
    }

    private async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_items);
        try { await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json); } catch { }
    }
}

public sealed class RecentlyViewedItem : IDiscountInfo
{
    public int Id { get; set; }
    public string? Slug { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
    public DateTimeOffset? DiscountStartDate { get; set; }
    public DateTimeOffset? DiscountExpiryDate { get; set; }
}
