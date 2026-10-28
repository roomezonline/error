using ErrorService.Shared;
using Microsoft.JSInterop;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

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
    private const string RevisionKey = "cart_revision";
    private const string RevisionOwnerKey = "cart_revision_owner";
    private static readonly TimeSpan TombstoneTtl = TimeSpan.FromDays(7);

    private readonly List<CartItem> _items = new();
    private readonly List<RemovedCartEntry> _removed = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    // نسخهٔ سبد سروری که کلاینت دیده است؛ تغییر ادمین/دستگاه دیگر نسخه را
    // عوض می‌کند و باعث می‌شود سبد محلیِ قدیمی روی آن نوشته نشود.
    private long _revision;
    private string? _revisionOwner;

    public event Action? OnChange;

    /// <summary>بلافاصله بعد از افزودن موفق یک کالا اجرا می‌شود (برای بازکردن سبد کوچک).</summary>
    public event Action<CartItem>? ItemAdded;

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
            var token = await _auth.GetTokenAsync() ?? await _auth.GetWorkshopTokenAsync();
            var owner = GetTokenOwner(token);

            await LoadLocalAsync();
            await LoadRevisionAsync(owner);
            await ReconcileWithServerAsync();
            NotifyChange();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string? GetTokenOwner(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        try
        {
            var identity = JwtParser.ParseClaimsFromJwt(token);
            return identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
        catch
        {
            return null;
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

    private async Task LoadRevisionAsync(string? currentOwner)
    {
        _revision = 0;
        _revisionOwner = null;

        var ownerJson = await _js.InvokeAsync<string?>("localStorage.getItem", RevisionOwnerKey);
        var revisionJson = await _js.InvokeAsync<string?>("localStorage.getItem", RevisionKey);

        if (long.TryParse(revisionJson, out var storedRevision) && storedRevision > 0)
        {
            _revision = storedRevision;
            _revisionOwner = string.IsNullOrWhiteSpace(ownerJson) ? null : ownerJson;
        }

        // حساب عوض شده (کاربر A خارج و B وارد شده): سبد محلی متعلق به دیگری است.
        // برای جلوگیری از دیدن/ادغام سبد حساب قبلی، محلی پاک و نسخه صفر می‌شود.
        var accountSwitched = !string.IsNullOrEmpty(_revisionOwner)
            && !string.IsNullOrEmpty(currentOwner)
            && !string.Equals(_revisionOwner, currentOwner, StringComparison.Ordinal);

        if (accountSwitched)
        {
            _items.Clear();
            _removed.Clear();
            _revision = 0;
            _revisionOwner = null;
            await SaveAsync();
            await SaveRemovedAsync();
            await SaveRevisionAsync();
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

            var getResp = await _http.GetAsync("api/cart/state");
            if (getResp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                return;
            if (!getResp.IsSuccessStatusCode) return;

            var state = await getResp.Content.ReadFromJsonAsync<CartStateDto>();
            if (state == null) return;

            // نسخهٔ محلی قدیمی است (ادمین یا دستگاه دیگر سبد را تغییر داده):
            // سبد سرور برنده است.
            if (_revision > 0 && _revision != state.Revision)
            {
                await AdoptStateAsync(state);
                return;
            }

            // سبد محلی خالی و بدون حذف‌های محلی: احتمالاً حافظهٔ مرورگر پاک شده؛
            // سبد سرور را برمی‌گردانیم و آن را با «پاک‌سازی» از بین نمی‌بریم.
            if (_items.Count == 0 && _removed.Count == 0)
            {
                await AdoptStateAsync(state);
                return;
            }

            var result = await SyncAsync(_revision);
            if (result == null) return;

            if (!result.Accepted)
            {
                await AdoptStateAsync(result.State);
                return;
            }

            await AdoptStateAsync(result.State);
        }
        catch
        {
            // Network/401/etc. — keep local cart as-is.
        }
    }

    private async Task<CartSyncResult?> SyncAsync(long baseRevision)
    {
        try
        {
            var dto = new CartSyncRequest
            {
                BaseRevision = baseRevision,
                Items = _items.Select(i => new CartItemDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.Name,
                    ImageUrl = i.ImageUrl,
                    Price = i.Price,
                    Quantity = i.Quantity
                }).ToList()
            };

            using var resp = await _http.PostAsJsonAsync("api/cart/sync-v2", dto);

            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return null;

            if (resp.StatusCode == HttpStatusCode.Conflict)
                return await resp.Content.ReadFromJsonAsync<CartSyncResult>();

            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<CartSyncResult>();
        }
        catch
        {
            return null;
        }
    }

    private async Task AdoptStateAsync(CartStateDto state)
    {
        _items.Clear();
        foreach (var si in state.Items)
        {
            if (si.ProductId <= 0 || si.Quantity <= 0) continue;
            _items.Add(new CartItem
            {
                ProductId = si.ProductId,
                Name = si.ProductName,
                ImageUrl = si.ImageUrl,
                Price = si.Price,
                Quantity = si.Quantity
            });
        }

        // سبد سرور اکنون مرجع است؛ حذف‌های محلیِ قدیمی دیگر معنا ندارند.
        _removed.Clear();
        _revision = state.Revision;

        await SaveAsync();
        await SaveRemovedAsync();
        await SaveRevisionAsync();
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

        // سقف موجودی: تعداد نباید از موجودی انبار بیشتر شود.
        if (product.StockQuantity > 0)
            quantity = Math.Min(quantity, product.StockQuantity);

        _removed.RemoveAll(r => r.ProductId == product.Id);

        var existing = _items.FirstOrDefault(x => x.ProductId == product.Id);
        if (existing != null)
        {
            var target = existing.Quantity + quantity;
            if (product.StockQuantity > 0) target = Math.Min(target, product.StockQuantity);
            existing.Quantity = target;
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

        var added = _items.FirstOrDefault(x => x.ProductId == product.Id);
        if (added != null)
        {
            try { ItemAdded?.Invoke(added); } catch { }
        }
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

    /// <summary>پس از پرداخت/ثبت سفارش ادمین، نسخهٔ سبد سروری عوض شده است؛ وضعیت را تازه می‌کنیم.</summary>
    public async Task RefreshFromServerAsync()
    {
        try
        {
            var getResp = await _http.GetAsync("api/cart/state");
            if (!getResp.IsSuccessStatusCode) return;
            var state = await getResp.Content.ReadFromJsonAsync<CartStateDto>();
            if (state == null) return;
            await AdoptStateAsync(state);
            NotifyChange();
        }
        catch { }
    }

    private static bool CanUseServerCart(HttpResponseMessage resp)
        => resp.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);

    private async Task CaptureRevisionAsync(HttpResponseMessage resp)
    {
        if (resp.Headers.TryGetValues("X-Cart-Revision", out var values)
            && long.TryParse(values.FirstOrDefault(), out var revision)
            && revision > 0)
        {
            _revision = revision;
            await SaveRevisionAsync();
        }
    }

    private async Task TryAddServerAsync(int productId, int quantity)
    {
        try
        {
            var token = await _auth.GetTokenAsync() ?? await _auth.GetWorkshopTokenAsync();
            if (string.IsNullOrEmpty(token)) return;
            using var resp = await _http.PostAsJsonAsync("api/cart", new CartItemDto { ProductId = productId, Quantity = quantity });
            await CaptureRevisionAsync(resp);
        }
        catch { }
    }

    private async Task TryUpdateServerAsync(int productId, int quantity)
    {
        try
        {
            var token = await _auth.GetTokenAsync() ?? await _auth.GetWorkshopTokenAsync();
            if (string.IsNullOrEmpty(token)) return;
            using var resp = await _http.PutAsJsonAsync($"api/cart/{productId}", new CartItemUpdateDto { Quantity = quantity });
            await CaptureRevisionAsync(resp);
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
            await CaptureRevisionAsync(resp);
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
            await CaptureRevisionAsync(resp);
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

    private async Task SaveRevisionAsync()
    {
        if (_revision > 0 && string.IsNullOrEmpty(_revisionOwner))
        {
            // مالک نسخه را از توکن فعلی نگه می‌داریم تا تشخیص تغییر حساب ممکن باشد.
            var token = await _auth.GetTokenAsync();
            _revisionOwner = GetTokenOwner(token);
        }

        await _js.InvokeVoidAsync("localStorage.setItem", RevisionKey, _revision.ToString());
        if (string.IsNullOrEmpty(_revisionOwner))
            await _js.InvokeVoidAsync("localStorage.removeItem", RevisionOwnerKey);
        else
            await _js.InvokeVoidAsync("localStorage.setItem", RevisionOwnerKey, _revisionOwner);
    }

    private void NotifyChange() => OnChange?.Invoke();
}
