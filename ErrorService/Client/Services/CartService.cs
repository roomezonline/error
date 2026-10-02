using ErrorService.Shared;
using Microsoft.JSInterop;
using System.Net;
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

public class RemovedCartEntry
{
    public int ProductId { get; set; }
    public DateTimeOffset RemovedAt { get; set; }
}

public class CartService
{
    private readonly IJSRuntime _js;
    private readonly HttpClient _http;
    private readonly AuthService _auth;
    private const string LocalStorageKey = "shopping_cart";
    private const string RemovedKey = "shopping_cart_removed";
    private static readonly TimeSpan TombstoneTtl = TimeSpan.FromDays(7);

    private readonly List<CartItem> _items = new();
    private readonly List<RemovedCartEntry> _removed = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public event Action? OnChange;

    public CartService(IJSRuntime js, HttpClient http, AuthService auth)
    {
        _js = js;
        _http = http;
        _auth = auth;
    }

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await LoadLocalAsync();
            await ReconcileWithServerAsync();
            NotifyChange();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task LoadLocalAsync()
    {
        _items.Clear();
        _removed.Clear();

        var json = await _js.InvokeAsync<string?>("localStorage.getItem", LocalStorageKey);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<CartItem>>(json);
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        if (item.ProductId > 0 && item.Quantity > 0)
                            _items.Add(item);
                    }
                }
            }
            catch { }
        }

        var removedJson = await _js.InvokeAsync<string?>("localStorage.getItem", RemovedKey);
        if (!string.IsNullOrEmpty(removedJson))
        {
            try
            {
                var list = System.Text.Json.JsonSerializer.Deserialize<List<RemovedCartEntry>>(removedJson);
                if (list != null)
                {
                    var cutoff = DateTimeOffset.UtcNow - TombstoneTtl;
                    foreach (var entry in list)
                    {
                        if (entry.ProductId > 0 && entry.RemovedAt >= cutoff)
                            _removed.Add(entry);
                    }
                }
            }
            catch { }
        }
    }

    private HashSet<int> RemovedIds()
        => _removed.Select(r => r.ProductId).ToHashSet();

    private async Task ReconcileWithServerAsync()
    {
        try
        {
            var token = await _auth.GetTokenAsync() ?? await _auth.GetWorkshopTokenAsync();
            if (string.IsNullOrEmpty(token)) return;

            var getResp = await _http.GetAsync("api/cart");
            if (getResp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                return;

            if (!getResp.IsSuccessStatusCode) return;

            var serverItems = await getResp.Content.ReadFromJsonAsync<List<CartItemDto>>();
            if (serverItems == null) return;

            var removedIds = RemovedIds();

            // Rebuild cart in place so any component holding GetItems() reference stays valid.
            var merged = new List<CartItem>();

            // 1) Local items survive unless tombstoned (recent local removes win).
            foreach (var local in _items)
            {
                if (removedIds.Contains(local.ProductId)) continue;
                if (merged.Any(m => m.ProductId == local.ProductId)) continue;
                merged.Add(local);
            }

            // 2) Server items: skip tombstoned (deleted locally, DELETE may have failed);
            //    merge quantity with local when both exist; otherwise take server item.
            foreach (var si in serverItems)
            {
                if (si.ProductId <= 0 || si.Quantity <= 0) continue;
                if (removedIds.Contains(si.ProductId)) continue;

                var existing = merged.FirstOrDefault(m => m.ProductId == si.ProductId);
                if (existing != null)
                {
                    existing.Quantity = Math.Max(existing.Quantity, si.Quantity);
                    if (string.IsNullOrEmpty(existing.ImageUrl)) existing.ImageUrl = si.ImageUrl;
                    if (existing.Price <= 0) existing.Price = si.Price;
                }
                else
                {
                    merged.Add(new CartItem
                    {
                        ProductId = si.ProductId,
                        Name = si.ProductName,
                        ImageUrl = si.ImageUrl,
                        Price = si.Price,
                        Quantity = si.Quantity
                    });
                }
            }

            _items.Clear();
            _items.AddRange(merged);

            // 3) Full replace on server so extras (including failed deletes) are removed.
            await ReplaceServerAsync();

            await SaveAsync();
            await SaveRemovedAsync();
        }
        catch
        {
            // Network/401/etc. — keep local cart as-is.
        }
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
        if (quantity <= 0) return;

        _removed.RemoveAll(r => r.ProductId == product.Id);

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
                Price = DiscountHelper.GetEffectivePrice(product),
                Quantity = quantity
            });
        }

        await SaveAsync();
        await SaveRemovedAsync();
        await TryAddServerAsync(product.Id, quantity);
        NotifyChange();
    }

    public async Task UpdateQuantityAsync(int productId, int quantity)
    {
        var item = _items.FirstOrDefault(x => x.ProductId == productId);
        if (item == null) return;

        if (quantity <= 0)
        {
            await RemoveItemAsync(productId);
            return;
        }

        item.Quantity = quantity;
        _removed.RemoveAll(r => r.ProductId == productId);
        await SaveAsync();
        await SaveRemovedAsync();
        await TryUpdateServerAsync(productId, quantity);
        NotifyChange();
    }

    public async Task RemoveItemAsync(int productId)
    {
        var item = _items.FirstOrDefault(x => x.ProductId == productId);
        if (item != null)
            _items.Remove(item);

        // Tombstone first so a failed DELETE cannot resurrect the item on next load.
        if (!_removed.Any(r => r.ProductId == productId))
        {
            _removed.Add(new RemovedCartEntry
            {
                ProductId = productId,
                RemovedAt = DateTimeOffset.UtcNow
            });
        }

        await SaveAsync();
        await SaveRemovedAsync();
        await TryRemoveServerAsync(productId);
        NotifyChange();
    }

    public async Task ClearCartAsync()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in _items)
        {
            if (!_removed.Any(r => r.ProductId == item.ProductId))
                _removed.Add(new RemovedCartEntry { ProductId = item.ProductId, RemovedAt = now });
        }

        _items.Clear();
        await SaveAsync();
        await SaveRemovedAsync();
        await TryClearServerAsync();
        NotifyChange();
    }

    private async Task ReplaceServerAsync()
    {
        try
        {
            var token = await _auth.GetTokenAsync() ?? await _auth.GetWorkshopTokenAsync();
            if (string.IsNullOrEmpty(token)) return;

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

    private static bool CanUseServerCart(HttpResponseMessage resp)
        => resp.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);

    private async Task TryAddServerAsync(int productId, int quantity)
    {
        try
        {
            var token = await _auth.GetTokenAsync() ?? await _auth.GetWorkshopTokenAsync();
            if (string.IsNullOrEmpty(token)) return;
            await _http.PostAsJsonAsync("api/cart", new CartItemDto { ProductId = productId, Quantity = quantity });
        }
        catch { }
    }

    private async Task TryUpdateServerAsync(int productId, int quantity)
    {
        try
        {
            var token = await _auth.GetTokenAsync() ?? await _auth.GetWorkshopTokenAsync();
            if (string.IsNullOrEmpty(token)) return;
            await _http.PutAsJsonAsync($"api/cart/{productId}", new CartItemUpdateDto { Quantity = quantity });
        }
        catch { }
    }

    private async Task TryRemoveServerAsync(int productId)
    {
        try
        {
            var token = await _auth.GetTokenAsync() ?? await _auth.GetWorkshopTokenAsync();
            if (string.IsNullOrEmpty(token)) return;

            // 404 = already gone — fine. 401 = cannot use server cart — tombstone covers us.
            using var resp = await _http.DeleteAsync($"api/cart/{productId}");
            _ = CanUseServerCart(resp);
        }
        catch { }
    }

    private async Task TryClearServerAsync()
    {
        try
        {
            var token = await _auth.GetTokenAsync() ?? await _auth.GetWorkshopTokenAsync();
            if (string.IsNullOrEmpty(token)) return;
            using var resp = await _http.DeleteAsync("api/cart");
            _ = CanUseServerCart(resp);
        }
        catch { }
    }

    private async Task SaveAsync()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(_items);
        await _js.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, json);
    }

    private async Task SaveRemovedAsync()
    {
        // Keep a bounded list so localStorage does not grow forever.
        var cutoff = DateTimeOffset.UtcNow - TombstoneTtl;
        _removed.RemoveAll(r => r.RemovedAt < cutoff);
        if (_removed.Count > 200)
            _removed.RemoveRange(0, _removed.Count - 200);

        var json = System.Text.Json.JsonSerializer.Serialize(_removed);
        await _js.InvokeVoidAsync("localStorage.setItem", RemovedKey, json);
    }

    private void NotifyChange() => OnChange?.Invoke();
}
