using ErrorService.Shared;
using System.Net.Http.Json;
using System.Text.Json;

namespace ErrorService.Client.Services;

/// <summary>فرم تکمیل سفارش که کاربر وسط راه رها کرده و باید از همان‌جا ادامه دهد.</summary>
public sealed class CheckoutDraft
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public int? ShippingMethodId { get; set; }
    public int? GatewayId { get; set; }
    public string? CouponCode { get; set; }
    public DateTimeOffset SavedAt { get; set; }
}

/// <summary>علامت سفارشی که ثبت شده ولی پرداخت نشده (برای مهمان‌ها؛ کاربران از سرور خوانده می‌شود).</summary>
public sealed class PendingOrderMarker
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>چیزی که باید به کاربر یادآوری شود تا سفارش را تمام کند.</summary>
public sealed class CheckoutResumeInfo
{
    /// <summary>سفارش ثبت‌شده و پرداخت‌نشده (مهم‌ترین مورد).</summary>
    public UnfinishedOrderDto? Order { get; set; }

    /// <summary>تعداد کالاهای سبد خریدِ رهاشده (سفارش هنوز ثبت نشده).</summary>
    public int CartItemCount { get; set; }

    /// <summary>اطلاعات فرم تکمیل‌نشدهٔ سبد/تسویه.</summary>
    public CheckoutDraft? Draft { get; set; }

    public bool HasUnfinishedOrder => Order != null;
    public bool HasAbandonedCart => Order == null && CartItemCount > 0;
    public bool HasAny => Order != null || CartItemCount > 0;
}

/// <summary>
/// بازیابی سفارش نیمه‌تمام: فرم تسویه را در localStorage ذخیره می‌کند، سفارشِ ثبت‌شده
/// ولی پرداخت‌نشده را (از سرور برای کاربران و از localStorage برای مهمان‌ها) پیدا می‌کند
/// و به بنرهای سربرگ/پروفایل اجازه می‌دهد کاربر را از همان نقطه ادامه دهند.
/// </summary>
public sealed class CheckoutRecoveryService
{
    public const string DraftKey = "checkout_draft";
    public const string PendingOrderKey = "checkout_pending_order";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly LocalStorageService _storage;
    private readonly AuthService _auth;
    private readonly CartService _cart;

    public CheckoutRecoveryService(
        HttpClient http,
        LocalStorageService storage,
        AuthService auth,
        CartService cart)
    {
        _http = http;
        _storage = storage;
        _auth = auth;
        _cart = cart;
    }

    /// <summary>اطلاعات فعلی بازیابی؛ بعد از هر تغییر <see cref="OnChange"/> اجرا می‌شود.</summary>
    public CheckoutResumeInfo Resume { get; private set; } = new();

    public event Action? OnChange;

    /// <summary>
    /// خواندن وضعیت: سبد خرید + فرم ذخیره‌شده + سفارش پرداخت‌نشده.
    /// برای سرور فقط وقتی کاربر وارد شده و سفارشِ معلقی ممکن است داشته باشد، تماس می‌گیرد.
    /// </summary>
    public async Task RefreshAsync()
    {
        try
        {
            var cartCount = _cart.GetTotalCount();
            var draft = await LoadDraftAsync();
            UnfinishedOrderDto? order = null;

            var token = await _auth.GetTokenAsync() ?? await _auth.GetWorkshopTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                order = await LoadServerUnfinishedAsync();
                if (order != null) await _storage.RemoveAsync(PendingOrderKey);
            }

            order ??= await LoadLocalPendingAsync();

            Resume = new CheckoutResumeInfo
            {
                Order = order,
                CartItemCount = cartCount,
                Draft = draft
            };
        }
        catch
        {
            // سرویس بازیابی نباید خطای خودش را به صفحه تحمیل کند.
        }

        OnChange?.Invoke();
    }

    private async Task<UnfinishedOrderDto?> LoadServerUnfinishedAsync()
    {
        try
        {
            var rows = await _http.GetFromJsonAsync<List<UnfinishedOrderDto>>(
                "api/orders/my/unfinished", JsonOptions);
            return rows?.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>سفارش مهمانی که بعد از ثبت، پرداخت نشده و هنوز از بین نرفته است.</summary>
    private async Task<UnfinishedOrderDto?> LoadLocalPendingAsync()
    {
        var marker = await ReadPendingMarkerAsync();
        if (marker == null) return null;

        try
        {
            var order = await _http.GetFromJsonAsync<OrderDto>(
                $"api/orders/{marker.Id}?token={Uri.EscapeDataString(marker.AccessToken)}", JsonOptions);
            if (order == null) return null;

            if (order.Status is OrderStatus.Approved or OrderStatus.Completed
                or OrderStatus.Cancelled or OrderStatus.Rejected)
            {
                await ClearPendingOrderAsync();
                return null;
            }

            return new UnfinishedOrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Status = order.Status.ToString(),
                StatusLabel = StatusLabel(order.Status),
                CreatedAt = order.CreatedAt,
                TotalAmount = order.TotalAmount,
                ItemsCount = order.Items?.Count ?? 0,
                ResumeUrl = $"/payment/offline/{order.Id}?token={Uri.EscapeDataString(marker.AccessToken)}",
                NeedsPayment = order.Status == OrderStatus.PendingPayment
            };
        }
        catch
        {
            return null;
        }
    }

    private static string StatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => "در انتظار پرداخت",
        OrderStatus.ReceiptUploaded => "رسید آپلود شده",
        OrderStatus.Approved => "تایید شده",
        OrderStatus.Completed => "تکمیل شده",
        OrderStatus.Rejected => "رد شده",
        OrderStatus.Cancelled => "لغو شده",
        _ => status.ToString()
    };

    // ────────────── پیش‌نویس فرم تسویه ──────────────

    public async Task<CheckoutDraft?> LoadDraftAsync()
    {
        try
        {
            var json = await _storage.GetAsync(DraftKey);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<CheckoutDraft>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveDraftAsync(CheckoutDraft draft)
    {
        draft.SavedAt = DateTimeOffset.UtcNow;
        try
        {
            await _storage.SetAsync(DraftKey, JsonSerializer.Serialize(draft, JsonOptions));
        }
        catch { }
        OnChange?.Invoke();
    }

    public async Task ClearDraftAsync()
    {
        try { await _storage.RemoveAsync(DraftKey); } catch { }
        OnChange?.Invoke();
    }

    // ────────────── نشان سفارش پرداخت‌نشده (مهمان‌ها) ──────────────

    public async Task RememberPendingOrderAsync(int id, string orderNumber, string accessToken)
    {
        var marker = new PendingOrderMarker
        {
            Id = id,
            OrderNumber = orderNumber,
            AccessToken = accessToken,
            CreatedAt = DateTimeOffset.UtcNow
        };
        try
        {
            await _storage.SetAsync(PendingOrderKey, JsonSerializer.Serialize(marker, JsonOptions));
        }
        catch { }
        OnChange?.Invoke();
    }

    public async Task ClearPendingOrderAsync()
    {
        try { await _storage.RemoveAsync(PendingOrderKey); } catch { }
        OnChange?.Invoke();
    }

    private async Task<PendingOrderMarker?> ReadPendingMarkerAsync()
    {
        try
        {
            var json = await _storage.GetAsync(PendingOrderKey);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<PendingOrderMarker>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
