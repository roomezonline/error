using Microsoft.JSInterop;

namespace ErrorService.Client.Services;

public sealed class WishlistItem
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountPrice { get; set; }
}

public sealed class WishlistService
{
    private readonly IJSRuntime _js;
    private const string StorageKey = "wishlist";
    private List<WishlistItem>? _items;
    private bool _initialized;

    public event Action? OnChange;

    public WishlistService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<List<WishlistItem>> GetItemsAsync()
    {
        if (!_initialized)
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            _items = !string.IsNullOrEmpty(json)
                ? System.Text.Json.JsonSerializer.Deserialize<List<WishlistItem>>(json) ?? new()
                : new();
            _initialized = true;
        }
        return _items ??= new();
    }

    public async Task<bool> HasItemAsync(int productId)
    {
        var items = await GetItemsAsync();
        return items.Any(x => x.ProductId == productId);
    }

    public async Task<int> GetCountAsync()
    {
        var items = await GetItemsAsync();
        return items.Count;
    }

    public async Task ToggleAsync(int productId, string name, string? imageUrl, decimal price, decimal? discountPrice)
    {
        var items = await GetItemsAsync();
        var existing = items.FirstOrDefault(x => x.ProductId == productId);
        if (existing != null)
        {
            items.Remove(existing);
        }
        else
        {
            items.Add(new WishlistItem
            {
                ProductId = productId,
                Name = name,
                ImageUrl = imageUrl,
                Price = price,
                DiscountPrice = discountPrice
            });
        }
        await PersistAsync(items);
        OnChange?.Invoke();
    }

    public async Task RemoveAsync(int productId)
    {
        var items = await GetItemsAsync();
        items.RemoveAll(x => x.ProductId == productId);
        await PersistAsync(items);
        OnChange?.Invoke();
    }

    public async Task ClearAsync()
    {
        _items = new();
        await PersistAsync(_items);
        OnChange?.Invoke();
    }

    private async Task PersistAsync(List<WishlistItem> items)
    {
        _items = items;
        var json = System.Text.Json.JsonSerializer.Serialize(items);
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }
}
