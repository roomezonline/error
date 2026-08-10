using ErrorService.Shared;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace ErrorService.Client.Services;

public class CartItem
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public class CartService
{
    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private readonly AuthService _auth;
    private const string LocalStorageKey = "shopping_cart";
    private List<CartItem> _items = new();

    public event Action? OnChange;

    public CartService(IJSRuntime js, HttpClient http, AuthService auth)
    {
        _js = js;
        _http = http;
        _auth = auth;
    }

    public async Task InitializeAsync()
    {
        var json = await _js.InvokeAsync<string?>("localStorage.getItem", LocalStorageKey);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _items = System.Text.Json.JsonSerializer.Deserialize<List<CartItem>>(json) ?? new();
            }
            catch { _items = new(); }
        }

        try
        {
            var token = await _auth.GetTokenAsync() ?? await _auth.GetWorkshopTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                var user = await _auth.GetMeAsync();
                if (user != null)
                {
                    var serverItems = await _http.GetFromJsonAsync<List<CartItemDto>>("api/cart");
                    if (serverItems != null && serverItems.Any())
                    {
                        foreach (var si in serverItems)
                        {
                            var existing = _items.FirstOrDefault(x => x.ProductId == si.ProductId);
                            if (existing != null)
                                existing.Quantity = Math.Max(existing.Quantity, si.Quantity);
                            else
                                _items.Add(new CartItem
                                {
                                    ProductId = si.ProductId,
                                    Name = si.ProductName,
                                    ImageUrl = si.ImageUrl,
                                    Price = si.Price,
                                    Quantity = si.Quantity
                                });
                        }
                        await SyncToServerAsync();
                        await SaveAsync();
                    }
                    else if (_items.Any())
                    {
                        await SyncToServerAsync();
                    }
                }
            }
        }
        catch { }

        NotifyChange();
    }

    public List<CartItem> GetItems() => _items;

    public int GetQuantity(int productId)
    {
        var item = _items.FirstOrDefault(x => x.ProductId == productId);
        return item?.Quantity ?? 0;
    }

    public bool HasItem(int productId) => GetQuantity(productId) > 0;

    public int GetTotalCount() => _items.Sum(x => x.Quantity);

    public decimal GetTotalAmount() => _items.Sum(x => x.Price * x.Quantity);

    public async Task AddToCartAsync(ProductDto product, int quantity = 1)
    {
        var existing = _items.FirstOrDefault(x => x.ProductId == product.Id);
        if (existing != null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            _items.Add(new CartItem
            {
                ProductId = product.Id,
                Name = product.Name,
                ImageUrl = product.MainImageUrl,
                Price = product.DiscountPrice ?? product.Price,
                Quantity = quantity
            });
        }
        await SaveAsync();
        await TryAddServerAsync(product.Id, quantity);
    }

    public async Task UpdateQuantityAsync(int productId, int quantity)
    {
        var item = _items.FirstOrDefault(x => x.ProductId == productId);
        if (item != null)
        {
            item.Quantity = quantity;
            if (item.Quantity <= 0) _items.Remove(item);
            await SaveAsync();
            await TryUpdateServerAsync(productId, quantity);
        }
    }

    public async Task RemoveItemAsync(int productId)
    {
        var item = _items.FirstOrDefault(x => x.ProductId == productId);
        if (item != null)
        {
            _items.Remove(item);
            await SaveAsync();
            await TryRemoveServerAsync(productId);
        }
    }

    public async Task ClearCartAsync()
    {
        _items.Clear();
        await SaveAsync();
        await TryClearServerAsync();
    }

    private async Task SyncToServerAsync()
    {
        try
        {
            var user = await _auth.GetMeAsync();
            if (user == null) return;

            var dto = _items.Select(i => new CartItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.Name,
                ImageUrl = i.ImageUrl,
                Price = i.Price,
                Quantity = i.Quantity
            }).ToList();

            await _http.PostAsJsonAsync("api/cart/sync", dto);
        }
        catch { }
    }

    private async Task TryAddServerAsync(int productId, int quantity)
    {
        try
        {
            var user = await _auth.GetMeAsync();
            if (user == null) return;
            await _http.PostAsJsonAsync("api/cart", new CartItemDto { ProductId = productId, Quantity = quantity });
        }
        catch { }
    }

    private async Task TryUpdateServerAsync(int productId, int quantity)
    {
        try
        {
            var user = await _auth.GetMeAsync();
            if (user == null) return;
            await _http.PutAsJsonAsync($"api/cart/{productId}", new CartItemUpdateDto { Quantity = quantity });
        }
        catch { }
    }

    private async Task TryRemoveServerAsync(int productId)
    {
        try
        {
            var user = await _auth.GetMeAsync();
            if (user == null) return;
            await _http.DeleteAsync($"api/cart/{productId}");
        }
        catch { }
    }

    private async Task TryClearServerAsync()
    {
        try
        {
            var user = await _auth.GetMeAsync();
            if (user == null) return;
            await _http.DeleteAsync("api/cart");
        }
        catch { }
    }

    private async Task SaveAsync()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(_items);
        await _js.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, json);
        NotifyChange();
    }

    private void NotifyChange() => OnChange?.Invoke();
}
