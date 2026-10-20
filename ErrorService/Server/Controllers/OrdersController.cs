using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Server.Services.Payment;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;
    private readonly NotificationEventService _notificationEvents;
    private readonly StockAlertNotifier _stockAlerts;
    private readonly IOutputCacheStore _outputCache;

    private static readonly HashSet<(OrderStatus, OrderStatus)> AllowedTransitions = new()
    {
        (OrderStatus.PendingPayment, OrderStatus.ReceiptUploaded),
        (OrderStatus.PendingPayment, OrderStatus.Approved),
        (OrderStatus.PendingPayment, OrderStatus.Rejected),
        (OrderStatus.PendingPayment, OrderStatus.Cancelled),
        (OrderStatus.ReceiptUploaded, OrderStatus.Approved),
        (OrderStatus.ReceiptUploaded, OrderStatus.Rejected),
        (OrderStatus.ReceiptUploaded, OrderStatus.Cancelled),
        (OrderStatus.Approved, OrderStatus.Completed),
        (OrderStatus.Approved, OrderStatus.Rejected),
        (OrderStatus.Approved, OrderStatus.Cancelled),
    };

    public OrdersController(ErrorServiceDbContext db, IWebHostEnvironment env, IConfiguration config, IAuthorizationService authorizationService, IPaymentGatewayFactory paymentGatewayFactory, NotificationEventService notificationEvents, StockAlertNotifier stockAlerts, IOutputCacheStore outputCache)
    {
        _db = db;
        _env = env;
        _config = config;
        _authorizationService = authorizationService;
        _paymentGatewayFactory = paymentGatewayFactory;
        _notificationEvents = notificationEvents;
        _stockAlerts = stockAlerts;
        _outputCache = outputCache;
    }

    private async Task<List<OrderItem>> LoadOrderItemsAsync(int orderId)
        => await _db.OrderItems.Where(i => i.OrderId == orderId).ToListAsync();

    private async Task EvictProductsCacheAsync()
    {
        try { await _outputCache.EvictByTagAsync(ProductsController.CacheTag, CancellationToken.None); } catch { }
    }

    private async Task RestoreStockAsync(List<OrderItem> items)
    {
        foreach (var item in items)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Products SET StockQuantity = StockQuantity + {item.Quantity}, IsAvailable = 1 WHERE Id = {item.ProductId}");
        }

        if (items.Count > 0)
            await EvictProductsCacheAsync();
    }

    /// <summary>
    /// Products of this order that are currently out-of-stock (candidates for
    /// restock notifications once stock is restored). Call BEFORE restoring.
    /// </summary>
    private async Task<List<int>> GetOutOfStockProductIdsAsync(List<OrderItem> items)
    {
        var ids = items.Select(i => i.ProductId).Distinct().ToList();
        return await _db.Products
            .Where(p => ids.Contains(p.Id) && (!p.IsAvailable || p.StockQuantity <= 0))
            .Select(p => p.Id)
            .ToListAsync();
    }

    private async Task<bool> ReleaseCouponUsageAsync(int? couponId)
    {
        if (!couponId.HasValue) return false;
        return await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Coupons SET CurrentUsageCount = CASE WHEN CurrentUsageCount > 0 THEN CurrentUsageCount - 1 ELSE 0 END WHERE Id = {couponId.Value}") > 0;
    }

    private async Task FinalizeOrderAsync(Order order)
    {
        if (order.StockDeducted)
            return;

        var items = await LoadOrderItemsAsync(order.Id);
        if (items.Count > 0)
        {
            // بارگذاری گروهی محصولات: به‌ازای هر قلم یک کوئری N+1 نمی‌زنیم.
            var productIds = items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _db.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            foreach (var item in items)
            {
                if (!products.TryGetValue(item.ProductId, out var product)) continue;
                product.StockQuantity = Math.Max(0, product.StockQuantity - item.Quantity);
                if (product.StockQuantity == 0)
                    product.IsAvailable = false;
            }

            if (items.Count > 0)
                await EvictProductsCacheAsync();
        }

        order.StockDeducted = true;

        var userCartItems = await _db.CartItems.Where(c => c.UserId == order.UserId).ToListAsync();
        if (userCartItems.Any())
        {
            _db.CartItems.RemoveRange(userCartItems);
        }
    }

    private async Task RestoreStockForOrderAsync(Order order)
    {
        if (!order.StockDeducted)
            return;

        var items = await LoadOrderItemsAsync(order.Id);
        await RestoreStockAsync(items);
        order.StockDeducted = false;
    }

    private void DeleteReceiptFile(string? receiptImageUrl)
    {
        if (string.IsNullOrWhiteSpace(receiptImageUrl) || !receiptImageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return;

        var relative = receiptImageUrl.TrimStart('/');
        var filePath = Path.Combine(_env.WebRootPath, relative);
        if (filePath.StartsWith(_env.WebRootPath, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);
    }

    private void DeleteReceiptFiles(IEnumerable<string>? receiptImageUrls)
    {
        if (receiptImageUrls == null) return;
        foreach (var url in receiptImageUrls)
            DeleteReceiptFile(url);
    }

    private static List<string> GetReceiptUrls(Order order)
    {
        if (!string.IsNullOrWhiteSpace(order.ReceiptImageUrlsJson))
        {
            try
            {
                var urls = JsonSerializer.Deserialize<List<string>>(order.ReceiptImageUrlsJson);
                if (urls != null && urls.Count > 0)
                    return urls;
            }
            catch (JsonException) { }
        }

        if (!string.IsNullOrWhiteSpace(order.ReceiptImageUrl))
            return new List<string> { order.ReceiptImageUrl };

        return new List<string>();
    }

    private static string SerializeReceiptUrls(List<string> urls)
        => urls.Count == 0 ? string.Empty : JsonSerializer.Serialize(urls);

    private async Task<string> GenerateOrderNumberAsync()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = Random.Shared.Next(100000, 1000000).ToString();
            if (!await _db.Orders.AnyAsync(o => o.OrderNumber == candidate))
                return candidate;
        }

        // Extremely unlikely fallback: keep incrementing from 999999 downward until free.
        for (var n = 999999; n >= 100000; n--)
        {
            var candidate = n.ToString();
            if (!await _db.Orders.AnyAsync(o => o.OrderNumber == candidate))
                return candidate;
        }

        throw new InvalidOperationException("هیچ شماره سفارش آزادی یافت نشد.");
    }

    private static bool IsIdempotencyKeyConflict(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e.Message.Contains("IX_Orders_IdempotencyKey", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private Task<OrderDto?> FindOrderIdempotencyAsync(string key)
        => _db.Orders.AsNoTracking()
            .Where(o => o.IdempotencyKey == key)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                Status = o.Status,
                TotalAmount = o.TotalAmount,
                DiscountAmount = o.DiscountAmount,
                CouponCode = o.CouponCode,
                AccessToken = o.AccessToken,
                ShippingMethodId = o.ShippingMethodId,
                ShippingMethodName = o.ShippingMethodName,
                ShippingCost = o.ShippingCost,                
                ShippingIsFree = o.ShippingIsFree
            })
            .FirstOrDefaultAsync();

    private bool IsCallerAuthenticated() => User.Identity?.IsAuthenticated == true;

    private async Task<bool> IsGuestCheckoutAllowedAsync()
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        return settings?.AllowGuestCheckout ?? true;
    }

    private async Task<int?> ResolveSiteUserIdAsync()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(idStr) && int.TryParse(idStr, out var id))
            return id;

        var phone = User.FindFirst(ClaimTypes.MobilePhone)?.Value;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
            if (user != null) return user.Id;
        }
        return null;
    }

    private string GetAdminDisplayName()
    {
        var name = User.FindFirst(ClaimTypes.Name)?.Value;
        if (!string.IsNullOrWhiteSpace(name)) return name;
        var phone = User.FindFirst(ClaimTypes.MobilePhone)?.Value;
        if (!string.IsNullOrWhiteSpace(phone)) return phone;
        return User.Identity?.Name ?? "مدیر";
    }

    private async Task AddOrderEventAsync(int orderId, string eventType, OrderStatus? from, OrderStatus? to, string? message = null)
    {
        _db.OrderEvents.Add(new OrderEvent
        {
            OrderId = orderId,
            EventType = eventType,
            FromStatus = from,
            ToStatus = to,
            Message = message,
            CreatedBy = GetAdminDisplayName(),
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task<bool> CanAccessOrderAsync(Order order, string? token)
    {
        // توکن دسترسی سفارش، مستقل از ورود کاربر، یک مجوز معتبر برای همان سفارش است.
        if (HasValidOrderToken(order, token)) return true;

        if ((await _authorizationService.AuthorizeAsync(User, null, "perm:admin.orders.view")).Succeeded)
            return true;
        if ((await _authorizationService.AuthorizeAsync(User, null, "perm:admin.orders.manage")).Succeeded)
            return true;

        int? userId = await ResolveSiteUserIdAsync();

        if (order.UserId.HasValue)
            return userId.HasValue && userId.Value == order.UserId.Value;

        if (userId.HasValue)
        {
            var user = await _db.Users.FindAsync(userId.Value);
            return user != null
                && !string.IsNullOrEmpty(user.PhoneNumber)
                && string.Equals(user.PhoneNumber, order.PhoneNumber, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool HasValidOrderToken(Order order, string? token)
        => !string.IsNullOrEmpty(order.AccessToken)
           && !string.IsNullOrEmpty(token)
           && order.AccessToken.Length == token.Length
           && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
               System.Text.Encoding.UTF8.GetBytes(order.AccessToken),
               System.Text.Encoding.UTF8.GetBytes(token));

    private static OrderDto MapOrderToDto(Order order, List<OrderItem> items, List<OrderShipment> shipments, List<OrderEvent> events, List<OrderReturn>? returns = null)
    {
        var shipmentCount = shipments.Count;
        var latestTracking = shipments
            .Where(s => !string.IsNullOrEmpty(s.TrackingNumber))
            .OrderByDescending(s => s.AttemptNumber)
            .Select(s => s.TrackingNumber)
            .FirstOrDefault();

        var failedShipments = shipments.Count(s => s.Status == OrderShipmentStatus.Failed || s.Status == OrderShipmentStatus.Returned);
        string? nextAction = order.Status switch
        {
            OrderStatus.ReceiptUploaded => "بررسی رسید پرداخت",
            OrderStatus.Approved when failedShipments > 0 => "ارسال مجدد مورد نیاز است",
            OrderStatus.Approved when shipmentCount == 0 => "ایجاد مرسوله و ارسال",
            OrderStatus.Approved => "پیگیری ارسال",
            OrderStatus.Completed => null,
            OrderStatus.Rejected => null,
            OrderStatus.Cancelled => null,
            _ => null
        };

        var stageDates = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        foreach (var e in events)
        {
            if (e.EventType != "stage_changed" || string.IsNullOrEmpty(e.Message)) continue;
            var parts = e.Message.Split('|');
            if (parts.Length < 3) continue;
            if (Enum.TryParse<OrderStage>(parts[^2], out var stageKey) && DateTimeOffset.TryParse(parts[^1], out var stageAt))
                stageDates[stageKey.ToString()] = stageAt;
        }

        if (order.Status is OrderStatus.Approved or OrderStatus.Completed)
        {
            if (!stageDates.ContainsKey(nameof(OrderStage.PaymentConfirmed)))
            {
                stageDates[nameof(OrderStage.PaymentConfirmed)] = order.PaymentDate ?? order.UpdatedAt;
            }

            var effectiveStage = order.Stage
                ?? (order.Status == OrderStatus.Completed ? OrderStage.Resolved : OrderStage.PaymentConfirmed);
            if (!stageDates.ContainsKey(effectiveStage.ToString()))
                stageDates[effectiveStage.ToString()] = order.UpdatedAt;
        }

        return new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            Status = order.Status,
            Stage = order.Stage,
            StageDates = stageDates,
            TotalAmount = order.TotalAmount,
            DiscountAmount = order.DiscountAmount,
            CouponCode = order.CouponCode,
            FullName = order.FullName,
            PhoneNumber = order.PhoneNumber,
            Email = order.Email,
            Address = order.Address,
            ProvinceId = order.ProvinceId,
            CityId = order.CityId,
            ProvinceName = order.ProvinceName,
            CityName = order.CityName,
            PostalCode = order.PostalCode,
            ReceiptImageUrl = order.ReceiptImageUrl,
            ReceiptImageUrls = GetReceiptUrls(order),
            TrackingNumber = order.TrackingNumber,
            PaymentDate = order.PaymentDate,
            AdminNotes = order.AdminNotes,
            CustomerNotes = order.CustomerNotes,
            AccessToken = order.AccessToken,
            PaymentProvider = order.PaymentProvider,
            PaymentReference = order.PaymentReference,
            ShipmentCount = shipmentCount,
            LatestTrackingNumber = latestTracking,
            NextAction = nextAction,
            ShippingMethodId = order.ShippingMethodId,
            ShippingMethodName = order.ShippingMethodName,
            ShippingCost = order.ShippingCost,            
            ShippingIsFree = order.ShippingIsFree,
            ReturnCount = (returns ?? new()).Count,
            Items = items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Price = i.Price,
                Quantity = i.Quantity
            }).ToList(),
            Shipments = shipments.Select(s => new OrderShipmentDto
            {
                Id = s.Id,
                AttemptNumber = s.AttemptNumber,
                Carrier = s.Carrier,
                TrackingNumber = s.TrackingNumber,
                Status = s.Status.ToString(),
                FailureReason = s.FailureReason,
                Notes = s.Notes,
                ShippedAt = s.ShippedAt,
                DeliveredAt = s.DeliveredAt,
                CreatedAt = s.CreatedAt,
                CreatedBy = s.CreatedBy
            }).ToList(),
            Events = events.Select(e => new OrderEventDto
            {
                Id = e.Id,
                EventType = e.EventType,
                FromStatus = e.FromStatus,
                ToStatus = e.ToStatus,
                Message = e.Message,
                CreatedBy = e.CreatedBy,
                CreatedAt = e.CreatedAt
            }).ToList(),
            Returns = (returns ?? new()).Select(r => new OrderReturnDto
            {
                Id = r.Id,
                OrderId = r.OrderId,
                RefundAmount = r.RefundAmount,
                RefundCardNumber = r.RefundCardNumber,
                RefundBankName = r.RefundBankName,
                RefundDate = r.RefundDate,
                RefundReceiptImageUrl = r.RefundReceiptImageUrl,
                ReturnReason = r.ReturnReason,
                AdminNotes = r.AdminNotes,
                IsRefunded = r.IsRefunded,
                CreatedAt = r.CreatedAt,
                CreatedBy = r.CreatedBy,
                IsCustomerRequest = (r.CreatedBy ?? "").StartsWith("customer:")
            }).ToList()
        };
    }

    // ──────────── PUBLIC ENDPOINTS ────────────

    [HttpPost]
    [EnableRateLimiting("commerce")]
    public async Task<ActionResult<OrderDto>> CreateOrder(OrderCreateRequest request)
    {
        if (!await IsGuestCheckoutAllowedAsync() && !IsCallerAuthenticated())
            return Unauthorized("ثبت سفارش مهمان غیرفعال است. لطفاً ابتدا وارد شوید.");

        if (request.Items == null || !request.Items.Any())
            return BadRequest("سبد خرید خالی است.");

        // ── Idempotency: همان کلید دوباره = همان سفارش قبلی، نه سفارش جدید ──
        var idemKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? null
            : request.IdempotencyKey.Trim();
        if (idemKey != null)
        {
            var replay = await FindOrderIdempotencyAsync(idemKey);
            if (replay != null)
                return Ok(replay);
        }

        int? userId = await ResolveSiteUserIdAsync();

        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        // AsNoTracking: رزرو موجودی با SQL اتمیک انجام می‌شود و نباید نسخهٔ کش‌شدهٔ
        // ردیابی‌شده بعداً روی آن UPDATE بنویسد.
        var products = await _db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var now = DateTimeOffset.UtcNow;
        var orderItems = new List<OrderItem>();
        var requestedQty = new Dictionary<int, int>();
        decimal totalAmount = 0;
        string? provinceName = null;
        string? cityName = null;
        if (request.ProvinceId.HasValue)
            provinceName = await _db.Provinces.Where(x => x.Id == request.ProvinceId.Value).Select(x => x.Name).FirstOrDefaultAsync();
        if (request.CityId.HasValue)
            cityName = await _db.Cities.Where(x => x.Id == request.CityId.Value).Select(x => x.Name).FirstOrDefaultAsync();

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                return BadRequest("تعداد سفارش هر محصول باید بزرگ‌تر از صفر باشد.");

            if (!products.TryGetValue(item.ProductId, out var product))
                return BadRequest($"محصول با شناسه {item.ProductId} یافت نشد.");

            if (product.IsDraft)
                return BadRequest($"محصول «{product.Name}» در حال حاضر در فروشگاه موجود نیست.");

            // جمع تعداد هر محصول در کل سبد (آیتم‌های تکراریِ یک محصول) ملاک است.
            var wanted = requestedQty.GetValueOrDefault(item.ProductId) + item.Quantity;
            requestedQty[item.ProductId] = wanted;

            if (!product.IsAvailable || product.StockQuantity < wanted)
                return BadRequest($"موجودی «{product.Name}» کافی نیست. موجودی فعلی: {product.StockQuantity}");

            var effectivePrice = DiscountHelper.IsActive(product)
                ? product.DiscountPrice!.Value
                : product.Price;

            totalAmount += effectivePrice * item.Quantity;

            orderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Price = effectivePrice,
                Quantity = item.Quantity
            });
        }

        var order = new Order
        {
            UserId = userId,
            OrderNumber = await GenerateOrderNumberAsync(),
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Address = request.Address,
            ProvinceId = request.ProvinceId,
            CityId = request.CityId,
            ProvinceName = provinceName,
            CityName = cityName,
            PostalCode = request.PostalCode,
            Status = OrderStatus.PendingPayment,
            CreatedAt = now,
            UpdatedAt = now,
            TotalAmount = totalAmount,
            AccessToken = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idemKey
        };

        // Resolve shipping method
        if (request.ShippingMethodId.HasValue)
        {
            var shippingMethod = await _db.ShippingMethods.FindAsync(request.ShippingMethodId.Value);
            if (shippingMethod == null || !shippingMethod.IsActive)
                return BadRequest("روش ارسال انتخاب شده معتبر نیست.");

            order.ShippingMethodId = shippingMethod.Id;
            order.ShippingMethodName = shippingMethod.Name;
            order.ShippingCost = shippingMethod.Price;
            order.ShippingIsFree = shippingMethod.IsFree;

            // ارسال رایگان: وقتی مجموع کالاها از آستانهٔ تعریف‌شده در تنظیمات فروشگاه عبور کند
            var storeSettings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            if (storeSettings != null
                && storeSettings.EnableFreeShipping
                && storeSettings.FreeShippingThreshold.HasValue
                && storeSettings.FreeShippingThreshold.Value > 0
                && totalAmount >= storeSettings.FreeShippingThreshold.Value)
            {
                order.ShippingCost = 0;
                order.ShippingIsFree = true;
            }
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        // ── رزرو موجودی به‌صورت اتمیک ──
        // کاهشِ شرطی در خودِ پایگاه‌داده، هم lost-update بین دو تایید همزمان را
        // حذف می‌کند و هم اجازهٔ oversell نمی‌دهد. شکستِ هر ردیف = rollback کامل.
        foreach (var item in request.Items)
        {
            var affected = await _db.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE Products
                   SET StockQuantity = StockQuantity - {item.Quantity},
                       IsAvailable = CASE WHEN StockQuantity <= {item.Quantity} THEN 0 ELSE IsAvailable END
                   WHERE Id = {item.ProductId} AND IsAvailable = 1 AND StockQuantity >= {item.Quantity}");

            if (affected == 0)
                return BadRequest("موجودی محصول در این لحظه کافی نیست. لطفاً سبد خرید را به‌روزرسانی کنید.");
        }

        // از این لحظه موجودی برای این سفارش رزرو شده است؛ در تایید/پرداخت دوباره کم نمی‌شود.
        order.StockDeducted = true;

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == request.CouponCode);
            if (coupon != null)
            {
                if (!coupon.IsActive)
                    return BadRequest("کد تخفیف غیرفعال است.");
                if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate < now)
                    return BadRequest("کد تخفیف منقضی شده است.");
                if (coupon.MaxUsageCount.HasValue && coupon.CurrentUsageCount >= coupon.MaxUsageCount.Value)
                    return BadRequest("تعداد استفاده از این کد تخفیف به پایان رسیده است.");
                if (coupon.AssignedToUserId.HasValue)
                {
                    int? callerUserId = null;
                    try { callerUserId = await ResolveSiteUserIdAsync(); } catch { }
                    if (callerUserId == null || callerUserId.Value != coupon.AssignedToUserId.Value)
                        return BadRequest("این کد تخفیف متعلق به شما نیست.");
                }
                if (coupon.MinOrderAmount.HasValue && order.TotalAmount < coupon.MinOrderAmount.Value)
                    return BadRequest($"حداقل مبلغ سفارش برای این کد تخفیف {coupon.MinOrderAmount.Value:N0} تومان است.");

                decimal discount = 0;
                if (coupon.DiscountPercent.HasValue)
                    discount = order.TotalAmount * coupon.DiscountPercent.Value / 100m;
                else if (coupon.DiscountAmount.HasValue)
                    discount = coupon.DiscountAmount.Value;

                discount = Math.Min(discount, order.TotalAmount);
                order.DiscountAmount = discount;
                order.CouponId = coupon.Id;
                order.CouponCode = coupon.Code;

                // شمارندهٔ شرطی و اتمیک: دو سفارش همزمان نمی‌توانند از سقف مجاز
                // عبور کنند و شمارنده گم نمی‌شود (Update(coupon) حذف شد تا
                // مقدار کش‌شدهٔ EF روی افزایشِ اتمی سوار نشود).
                var counted = await _db.Database.ExecuteSqlInterpolatedAsync(
                    $@"UPDATE Coupons
                       SET CurrentUsageCount = CurrentUsageCount + 1
                       WHERE Id = {coupon.Id}
                         AND IsActive = 1
                         AND (ExpiryDate IS NULL OR ExpiryDate >= {now})
                         AND (MaxUsageCount IS NULL OR CurrentUsageCount < MaxUsageCount)");
                if (counted == 0)
                    return BadRequest("تعداد استفاده از این کد تخفیف به پایان رسیده است.");
            }
        }

        foreach (var oi in orderItems)
        {
            order.Items.Add(oi);
        }

        order.TotalAmount += order.ShippingCost;

        _db.Orders.Add(order);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (idemKey != null && IsIdempotencyKeyConflict(ex))
        {
            // دو درخواست موازی با یک کلید: ایندکس یکتا برنده را مشخص کرده؛
            // تراکنش برمی‌گردد (موجودی/کوپن برگردانده می‌شود) و همان سفارش برمی‌گردد.
            var existing = await FindOrderIdempotencyAsync(idemKey);
            if (existing != null)
                return Ok(existing);
            throw;
        }

        await AddOrderEventAsync(order.Id, "created", null, OrderStatus.PendingPayment, "سفارش ثبت شد");
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        await EvictProductsCacheAsync();

        await _notificationEvents.NotifyOrderCreatedAsync(order);
        await _db.SaveChangesAsync();

        return Ok(new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            DiscountAmount = order.DiscountAmount,
            CouponCode = order.CouponCode,
            AccessToken = order.AccessToken,
            ShippingMethodId = order.ShippingMethodId,
            ShippingMethodName = order.ShippingMethodName,
            ShippingCost = order.ShippingCost,            
            ShippingIsFree = order.ShippingIsFree
        });
    }

    // پیگیری عمومی سفارش با شماره سفارش + شماره موبایل (بدون نیاز به ورود).
    // برای جلوگیری از افشای وجود سفارش، در صورت عدم تطابق، ۴۰۴ برگردانده می‌شود.
    [AllowAnonymous]
    [EnableRateLimiting("commerce")]
    [HttpGet("track")]
    public async Task<ActionResult<OrderDto>> TrackOrder([FromQuery] string? orderNumber, [FromQuery] string? phone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(orderNumber) || string.IsNullOrWhiteSpace(phone))
            return BadRequest("شماره سفارش و شماره موبایل الزامی است.");

        var normalizedOrderNumber = orderNumber.Trim();
        var order = await _db.Orders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderNumber == normalizedOrderNumber, ct);
        if (order == null) return NotFound();

        if (!PhoneMatches(order.PhoneNumber, phone))
        {
            // صاحب سفارش (کاربر وارد‌شده) بدون شماره موبایل هم دسترسی دارد.
            var userId = await ResolveSiteUserIdAsync();
            var ownsOrder = userId.HasValue && order.UserId.HasValue && order.UserId.Value == userId.Value;
            if (!ownsOrder) return NotFound();
        }

        var items = await _db.OrderItems.AsNoTracking().Where(i => i.OrderId == order.Id).ToListAsync(ct);
        var shipments = await _db.OrderShipments.AsNoTracking().Where(s => s.OrderId == order.Id).OrderBy(s => s.AttemptNumber).ToListAsync(ct);
        var events = await _db.OrderEvents.AsNoTracking().Where(e => e.OrderId == order.Id).OrderBy(e => e.CreatedAt).ToListAsync(ct);
        var returns = await _db.OrderReturns.AsNoTracking().Where(r => r.OrderId == order.Id).OrderBy(r => r.CreatedAt).ToListAsync(ct);

        var dto = MapOrderToDto(order, items, shipments, events, returns);
        await ApplyProductImagesAsync(new[] { dto });
        return Ok(dto);
    }

    private static bool PhoneMatches(string? stored, string? provided)
    {
        var a = CanonicalPhone(stored);
        var b = CanonicalPhone(provided);
        return a.Length > 0 && a.Equals(b, StringComparison.Ordinal);
    }

    private static string CanonicalPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch >= '0' && ch <= '9') sb.Append(ch);
            else if (ch >= '\u06F0' && ch <= '\u06F9') sb.Append((char)('0' + (ch - '\u06F0')));  // ارقام فارسی
            else if (ch >= '\u0660' && ch <= '\u0669') sb.Append((char)('0' + (ch - '\u0660')));  // ارقام عربی
        }

        var digits = sb.ToString();
        if (digits.Length == 0) return string.Empty;

        if (digits.StartsWith("0098", StringComparison.Ordinal)) digits = "0" + digits.Substring(4);
        else if (digits.StartsWith("98", StringComparison.Ordinal) && digits.Length == 12) digits = "0" + digits.Substring(2);

        // مقایسه بر اساس ۱۰ رقم آخر تا حالت‌های ۰۹.../۹۸... یکسان دیده شوند.
        return digits.Length > 10 ? digits.Substring(digits.Length - 10) : digits;
    }

    // فاکتور قابل چاپ سفارش (HTML مستقل، مناسب چاپ یا ذخیره به‌صورت PDF).
    // دسترسی با همان قاعدهٔ سفارش: توکن، مالکیت، یا دسترسی ادمین.
    [AllowAnonymous]
    [HttpGet("{id:int}/invoice")]
    public async Task<IActionResult> GetOrderInvoice(int id, [FromQuery] string? token, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order == null) return NotFound();
        if (!await CanAccessOrderAsync(order, token)) return NotFound();

        var items = await _db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == id)
            .OrderBy(i => i.Id)
            .ToListAsync(ct);

        var latestTracking = order.TrackingNumber;
        if (string.IsNullOrWhiteSpace(latestTracking))
        {
            latestTracking = await _db.OrderShipments.AsNoTracking()
                .Where(s => s.OrderId == id && s.TrackingNumber != null && s.TrackingNumber != "")
                .OrderByDescending(s => s.AttemptNumber)
                .Select(s => s.TrackingNumber)
                .FirstOrDefaultAsync(ct);
        }

        var rows = new System.Text.StringBuilder();
        if (items.Count == 0)
        {
            rows.Append("<tr><td colspan=\"5\" class=\"c muted\">—</td></tr>");
        }
        else
        {
            var index = 0;
            foreach (var item in items)
            {
                index++;
                rows.Append("<tr>")
                    .Append("<td class=\"c\">").Append(index).Append("</td>")
                    .Append("<td>").Append(E(item.ProductName)).Append("</td>")
                    .Append("<td class=\"c\">").Append(item.Quantity.ToString("N0")).Append("</td>")
                    .Append("<td>").Append(item.Price.ToString("N0")).Append("</td>")
                    .Append("<td>").Append((item.Price * item.Quantity).ToString("N0")).Append("</td>")
                    .Append("</tr>");
            }
        }

        var subtotal = order.TotalAmount - order.ShippingCost;
        var discount = order.DiscountAmount ?? 0;
        var net = order.TotalAmount - discount;

        var shippingText = order.ShippingCost > 0
            ? $"{order.ShippingCost:N0} تومان"
            : (order.ShippingIsFree ? "رایگان" : "—");

        var discountRow = discount > 0
            ? $"<div class=\"row discount\"><span>تخفیف{(string.IsNullOrWhiteSpace(order.CouponCode) ? "" : $" ({E(order.CouponCode)})")}</span><b>-{discount:N0} تومان</b></div>"
            : "";

        var trackingRow = string.IsNullOrWhiteSpace(latestTracking)
            ? ""
            : $"<div class=\"muted\">کد رهگیری: <b dir=\"ltr\">{E(latestTracking)}</b></div>";

        var address = string.Join("، ", new[] { order.ProvinceName, order.CityName, order.Address }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        var postal = string.IsNullOrWhiteSpace(order.PostalCode)
            ? ""
            : $"<div class=\"muted\">کد پستی: <b dir=\"ltr\">{E(order.PostalCode)}</b></div>";

        var email = string.IsNullOrWhiteSpace(order.Email)
            ? ""
            : $"<div class=\"muted\" dir=\"ltr\" style=\"text-align:right\">{E(order.Email)}</div>";

        var paymentInfo = order.Status switch
        {
            OrderStatus.Approved or OrderStatus.Completed => order.PaymentDate.HasValue
                ? $"پرداخت‌شده — {PersianDateHelper.ToPersianDateTimeString(order.PaymentDate.Value, includeTime: true)}"
                : "پرداخت‌شده",
            OrderStatus.ReceiptUploaded => "در انتظار تأیید رسید پرداخت",
            OrderStatus.PendingPayment => "در انتظار پرداخت",
            OrderStatus.Rejected => "پرداخت رد شده",
            OrderStatus.Cancelled => "سفارش لغو شده",
            _ => StatusLabel(order.Status)
        };

        var html = InvoiceTemplate
            .Replace("[[ORDER_NO]]", E(order.OrderNumber))
            .Replace("[[DATE]]", E(PersianDateHelper.ToPersianDateTimeString(order.CreatedAt, includeTime: true)))
            .Replace("[[STATUS]]", E(StatusLabel(order.Status)))
            .Replace("[[FULL_NAME]]", E(order.FullName))
            .Replace("[[PHONE]]", E(order.PhoneNumber))
            .Replace("[[EMAIL]]", email)
            .Replace("[[ADDRESS]]", E(string.IsNullOrWhiteSpace(address) ? "-" : address))
            .Replace("[[POSTAL]]", postal)
            .Replace("[[SHIPPING_METHOD]]", E(string.IsNullOrWhiteSpace(order.ShippingMethodName) ? "-" : order.ShippingMethodName))
            .Replace("[[TRACKING_ROW]]", trackingRow)
            .Replace("[[ROWS]]", rows.ToString())
            .Replace("[[SUBTOTAL]]", subtotal.ToString("N0"))
            .Replace("[[DISCOUNT_ROW]]", discountRow)
            .Replace("[[SHIPPING_COST]]", shippingText)
            .Replace("[[NET]]", net.ToString("N0"))
            .Replace("[[PAYMENT_INFO]]", E(paymentInfo));

        return Content(html, "text/html; charset=utf-8");
    }

    private static string E(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

    private const string InvoiceTemplate = """
    <!DOCTYPE html>
    <html lang="fa" dir="rtl">
    <head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>فاکتور سفارش [[ORDER_NO]]</title>
    <style>
    * { box-sizing: border-box; }
    body { margin: 0; padding: 24px; background: #eef2f7; color: #0f172a; font-family: Vazirmatn, Tahoma, "Segoe UI", Arial, sans-serif; font-size: 13px; line-height: 1.7; }
    .sheet { max-width: 840px; margin: 0 auto; background: #fff; border-radius: 16px; box-shadow: 0 12px 34px rgba(15, 23, 42, .1); padding: 34px 36px; }
    .toolbar { max-width: 840px; margin: 0 auto 12px; display: flex; justify-content: space-between; align-items: center; gap: 10px; }
    .toolbar .hint { color: #64748b; font-size: 12px; }
    .btn { border: 0; border-radius: 10px; background: #4f46e5; color: #fff; padding: 9px 20px; font-family: inherit; font-size: 13px; font-weight: 700; cursor: pointer; }
    .btn:hover { background: #4338ca; }
    .head { display: flex; justify-content: space-between; align-items: flex-start; gap: 20px; padding-bottom: 18px; border-bottom: 2px solid #0f172a; }
    .brand { font-size: 22px; font-weight: 900; letter-spacing: -.5px; }
    .brand small { display: block; font-size: 12px; font-weight: 600; color: #64748b; margin-top: 2px; letter-spacing: 0; }
    .meta { display: grid; gap: 6px; min-width: 210px; }
    .meta div { display: flex; justify-content: space-between; gap: 12px; border-bottom: 1px dashed #e2e8f0; padding-bottom: 4px; }
    .meta span { color: #64748b; }
    .badge { display: inline-block; padding: 1px 10px; border-radius: 999px; background: #eef2ff; color: #4338ca; font-size: 12px; }
    .parties { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin: 20px 0; }
    .box { border: 1px solid #e2e8f0; border-radius: 12px; padding: 14px 16px; background: #f8fafc; }
    .box h4 { margin: 0 0 8px; font-size: 12px; color: #4f46e5; font-weight: 800; }
    .box .name { font-weight: 800; margin-bottom: 2px; }
    .muted { color: #64748b; }
    table { width: 100%; border-collapse: collapse; margin: 6px 0 18px; }
    thead th { background: #0f172a; color: #fff; font-size: 12px; font-weight: 700; padding: 9px 10px; text-align: right; }
    thead th:first-child { border-radius: 0 10px 0 0; }
    thead th:last-child { border-radius: 10px 0 0 0; }
    tbody td { padding: 9px 10px; border-bottom: 1px solid #e2e8f0; vertical-align: top; }
    tbody tr:nth-child(even) td { background: #f8fafc; }
    .c { text-align: center; }
    .totals { margin-right: auto; width: 340px; max-width: 100%; display: grid; gap: 6px; }
    .totals .row { display: flex; justify-content: space-between; gap: 12px; padding: 5px 0; border-bottom: 1px dashed #e2e8f0; }
    .totals .discount b { color: #dc2626; }
    .totals .grand { border-bottom: 0; border-top: 2px solid #0f172a; margin-top: 4px; padding-top: 8px; font-size: 15px; font-weight: 900; }
    .foot { margin-top: 26px; padding-top: 14px; border-top: 1px solid #e2e8f0; display: flex; justify-content: space-between; gap: 14px; flex-wrap: wrap; }
    .foot .pay { font-weight: 700; }
    .small { font-size: 11px; }
    @media print {
      body { background: #fff; padding: 0; }
      .sheet { box-shadow: none; border-radius: 0; padding: 0; max-width: none; }
      .toolbar { display: none !important; }
      thead th { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
      tbody tr:nth-child(even) td { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
    }
    @page { size: A4; margin: 14mm; }
    </style>
    </head>
    <body>
    <div class="toolbar">
      <span class="hint">برای ذخیره به‌صورت PDF، در پنجرهٔ چاپ «Save as PDF» را انتخاب کنید.</span>
      <button class="btn" onclick="window.print()">چاپ فاکتور</button>
    </div>
    <div class="sheet">
      <header class="head">
        <div class="brand">ارورسرویس<small>فاکتور فروش کالا</small></div>
        <div class="meta">
          <div><span>شماره فاکتور</span><b dir="ltr">[[ORDER_NO]]</b></div>
          <div><span>تاریخ صدور</span><b>[[DATE]]</b></div>
          <div><span>وضعیت</span><b class="badge">[[STATUS]]</b></div>
        </div>
      </header>
      <section class="parties">
        <div class="box">
          <h4>خریدار</h4>
          <div class="name">[[FULL_NAME]]</div>
          <div class="muted" dir="ltr" style="text-align:right">[[PHONE]]</div>
          [[EMAIL]]
          <div class="muted">آدرس: [[ADDRESS]]</div>
          [[POSTAL]]
        </div>
        <div class="box">
          <h4>ارسال</h4>
          <div>روش ارسال: <b>[[SHIPPING_METHOD]]</b></div>
          [[TRACKING_ROW]]
        </div>
      </section>
      <table>
        <thead>
          <tr><th style="width:44px" class="c">#</th><th>شرح کالا</th><th style="width:70px" class="c">تعداد</th><th style="width:120px">قیمت واحد (تومان)</th><th style="width:130px">جمع (تومان)</th></tr>
        </thead>
        <tbody>[[ROWS]]</tbody>
      </table>
      <div class="totals">
        <div class="row"><span>جمع کالاها</span><b>[[SUBTOTAL]] تومان</b></div>
        [[DISCOUNT_ROW]]
        <div class="row"><span>هزینه ارسال</span><b>[[SHIPPING_COST]]</b></div>
        <div class="row grand"><span>مبلغ قابل پرداخت</span><b>[[NET]] تومان</b></div>
      </div>
      <footer class="foot">
        <div class="pay">[[PAYMENT_INFO]]</div>
        <div class="muted small">این فاکتور به‌صورت خودکار توسط سامانهٔ ارورسرویس صادر شده است.</div>
      </footer>
    </div>
    </body>
    </html>
    """;

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id, [FromQuery] string? token, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order == null) return NotFound();

        if (!await CanAccessOrderAsync(order, token))
            return NotFound();

        var items = await _db.OrderItems.AsNoTracking().Where(i => i.OrderId == id).ToListAsync(ct);
        var shipments = await _db.OrderShipments.AsNoTracking().Where(s => s.OrderId == id).OrderBy(s => s.AttemptNumber).ToListAsync(ct);
        var events = await _db.OrderEvents.AsNoTracking().Where(e => e.OrderId == id).OrderBy(e => e.CreatedAt).ToListAsync(ct);
        var returns = await _db.OrderReturns.AsNoTracking().Where(r => r.OrderId == id).OrderBy(r => r.CreatedAt).ToListAsync(ct);

        var dto = MapOrderToDto(order, items, shipments, events, returns);
        await ApplyProductImagesAsync(new[] { dto });
        return Ok(dto);
    }

    private async Task ApplyProductImagesAsync(IEnumerable<OrderDto> orders)
    {
        var productIds = orders
            .Where(o => o.Items != null)
            .SelectMany(o => o.Items)
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();
        if (productIds.Count == 0) return;

        var images = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.MainImageUrl })
            .ToDictionaryAsync(p => p.Id, p => p.MainImageUrl);

        foreach (var item in orders.Where(o => o.Items != null).SelectMany(o => o.Items))
        {
            if (images.TryGetValue(item.ProductId, out var url))
                item.ImageUrl = url;
        }
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<ActionResult<List<OrderDto>>> GetMyOrders()
    {
        var userId = await ResolveSiteUserIdAsync();
        if (userId == null) return Unauthorized();

        var isWorkshopToken = User.FindFirst("workshop_user_id") != null;
        var phoneClaim = User.FindFirst(ClaimTypes.MobilePhone)?.Value;

        var orders = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Shipments)
            .Where(o => o.UserId == userId.Value
                || (isWorkshopToken && o.UserId == null && o.PhoneNumber == phoneClaim))
            .OrderByDescending(o => o.CreatedAt)
            .Select(order => new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                Status = order.Status,
                Stage = order.Stage,
                TotalAmount = order.TotalAmount,
                FullName = order.FullName,
                PhoneNumber = order.PhoneNumber,
                Email = order.Email,
                Address = order.Address,
                ProvinceId = order.ProvinceId,
                CityId = order.CityId,
                ProvinceName = order.ProvinceName,
                CityName = order.CityName,
                PostalCode = order.PostalCode,
                ReceiptImageUrl = order.ReceiptImageUrl,
                ReceiptImageUrls = GetReceiptUrls(order),
                TrackingNumber = order.TrackingNumber,
                    PaymentDate = order.PaymentDate,
                    AdminNotes = order.AdminNotes,
                    CustomerNotes = order.CustomerNotes,
                    PaymentProvider = order.PaymentProvider,
                PaymentReference = order.PaymentReference,
                ShippingMethodId = order.ShippingMethodId,
                ShippingMethodName = order.ShippingMethodName,
                ShippingCost = order.ShippingCost,                
                ShippingIsFree = order.ShippingIsFree,
                DiscountAmount = order.DiscountAmount,
                CouponCode = order.CouponCode,
                Shipments = order.Shipments.OrderBy(s => s.AttemptNumber).Select(s => new OrderShipmentDto
                {
                    Id = s.Id,
                    AttemptNumber = s.AttemptNumber,
                    Carrier = s.Carrier,
                    TrackingNumber = s.TrackingNumber,
                    Status = s.Status.ToString(),
                    Notes = s.Notes,
                    ShippedAt = s.ShippedAt,
                    DeliveredAt = s.DeliveredAt,
                    CreatedAt = s.CreatedAt
                }).ToList(),
                ReturnCount = order.Returns.Count,
                Returns = order.Returns.OrderBy(r => r.CreatedAt).Select(r => new OrderReturnDto
                {
                    Id = r.Id,
                    OrderId = r.OrderId,
                    RefundAmount = r.RefundAmount,
                    RefundCardNumber = r.RefundCardNumber,
                    RefundBankName = r.RefundBankName,
                    RefundDate = r.RefundDate,
                    RefundReceiptImageUrl = r.RefundReceiptImageUrl,
                    ReturnReason = r.ReturnReason,
                    AdminNotes = r.AdminNotes,
                    IsRefunded = r.IsRefunded,
                    CreatedAt = r.CreatedAt,
                    CreatedBy = r.CreatedBy,
                    IsCustomerRequest = (r.CreatedBy ?? "").StartsWith("customer:")
                }).ToList(),
                Items = order.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Price = i.Price,
                    Quantity = i.Quantity
                }).ToList()
            })
            .ToListAsync();

        await FillStageDatesAsync(orders);
        await ApplyProductImagesAsync(orders);
        return Ok(orders);
    }

    /// <summary>
    /// سفارش‌های نیمه‌تمام کاربر (در انتظار پرداخت یا در انتظار تایید) برای بنر
    /// «سفارش ناقص دارید» در سربرگ سایت، پروفایل و بعد از ورود مجدد.
    /// سبک است: فقط خلاصهٔ سفارش، بدون اقلام.
    /// </summary>
    [Authorize]
    [HttpGet("my/unfinished")]
    public async Task<ActionResult<List<UnfinishedOrderDto>>> GetMyUnfinishedOrders()
    {
        var userId = await ResolveSiteUserIdAsync();
        if (userId == null) return Unauthorized();

        var isWorkshopToken = User.FindFirst("workshop_user_id") != null;
        var phoneClaim = User.FindFirst(ClaimTypes.MobilePhone)?.Value;
        var timeoutMinutes = PendingOrderTimeout.GetMinutes(_config);

        var rows = await _db.Orders.AsNoTracking()
            .Where(o => (o.UserId == userId.Value
                         || (isWorkshopToken && o.UserId == null && o.PhoneNumber == phoneClaim))
                        && (o.Status == OrderStatus.PendingPayment
                            || o.Status == OrderStatus.ReceiptUploaded))
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.Status,
                o.CreatedAt,
                o.TotalAmount,
                ItemsCount = o.Items.Count
            })
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        var result = rows.Select(o => new UnfinishedOrderDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            Status = o.Status.ToString(),
            StatusLabel = StatusLabel(o.Status),
            CreatedAt = o.CreatedAt,
            TotalAmount = o.TotalAmount,
            ItemsCount = o.ItemsCount,
            ResumeUrl = $"/payment/offline/{o.Id}",
            NeedsPayment = o.Status == OrderStatus.PendingPayment,
            MinutesToAutoCancel = o.Status == OrderStatus.PendingPayment
                ? Math.Max(0, timeoutMinutes - (int)Math.Floor((now - o.CreatedAt).TotalMinutes))
                : 0
        }).ToList();

        return Ok(result);
    }

    private async Task FillStageDatesAsync(List<OrderDto> orders)
    {
        if (orders.Count == 0) return;

        var ids = orders.Select(o => o.Id).ToList();
        var stageEvents = await _db.OrderEvents.AsNoTracking()
            .Where(e => ids.Contains(e.OrderId) && e.EventType == "stage_changed" && e.Message != null)
            .ToListAsync();

        foreach (var order in orders)
        {
            var stageDates = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
            foreach (var e in stageEvents)
            {
                if (e.OrderId != order.Id) continue;
                var parts = (e.Message ?? string.Empty).Split('|');
                if (parts.Length < 3) continue;
                if (Enum.TryParse<OrderStage>(parts[^2], out var stageKey) && DateTimeOffset.TryParse(parts[^1], out var stageAt))
                    stageDates[stageKey.ToString()] = stageAt;
            }

            if (order.Status is OrderStatus.Approved or OrderStatus.Completed)
            {
                if (!stageDates.ContainsKey(nameof(OrderStage.PaymentConfirmed)))
                    stageDates[nameof(OrderStage.PaymentConfirmed)] = order.PaymentDate ?? order.UpdatedAt;

                var effectiveStage = order.Stage
                    ?? (order.Status == OrderStatus.Completed ? OrderStage.Resolved : OrderStage.PaymentConfirmed);
                if (!stageDates.ContainsKey(effectiveStage.ToString()))
                    stageDates[effectiveStage.ToString()] = order.UpdatedAt;
            }

            order.StageDates = stageDates;
        }
    }

    [HttpPost("{id:int}/receipt")]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> UploadReceipt(int id, [FromForm] List<IFormFile> files, [FromForm] string? trackingNumber, [FromForm] string? accessToken, [FromForm] string? customerNotes)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();

        if (!await IsGuestCheckoutAllowedAsync() && !IsCallerAuthenticated())
            return NotFound();

        if (!await CanAccessOrderAsync(order, accessToken))
            return NotFound();

        if (order.Status is OrderStatus.Approved or OrderStatus.Completed or OrderStatus.Rejected or OrderStatus.Cancelled)
            return BadRequest("در وضعیت فعلی سفارش امکان آپلود مجدد رسید وجود ندارد.");

        if (files == null || files.Count == 0)
            return BadRequest("فایل رسید ارسال نشده است.");

        var existingUrls = GetReceiptUrls(order);
        const int maxTotal = 3;
        if (existingUrls.Count + files.Count > maxTotal)
            return BadRequest($"حداکثر {maxTotal} تصویر رسید مجاز است. فعلاً {existingUrls.Count} تصویر ثبت شده است.");

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "receipts");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var savedUrls = new List<string>();
        foreach (var file in files)
        {
            if (file == null || file.Length == 0) continue;

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";
            var safeExt = ext.ToLowerInvariant();
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(safeExt))
                return BadRequest("فرمت فایل مجاز نیست. فقط تصاویر jpg, jpeg, png, webp");

            var fileName = $"{id}_{Guid.NewGuid():N}{safeExt}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            savedUrls.Add($"/uploads/receipts/{fileName}");
        }

        if (savedUrls.Count == 0)
            return BadRequest("هیچ فایل معتبری ارسال نشده است.");

        existingUrls.AddRange(savedUrls);
        order.ReceiptImageUrlsJson = SerializeReceiptUrls(existingUrls);
        order.ReceiptImageUrl = existingUrls[^1];
        order.TrackingNumber = trackingNumber;
        if (!string.IsNullOrWhiteSpace(customerNotes))
            order.CustomerNotes = customerNotes.Trim();
        order.PaymentDate = DateTimeOffset.UtcNow;
        order.Status = OrderStatus.ReceiptUploaded;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        await FinalizeOrderAsync(order);
        await _notificationEvents.NotifyOrderReceiptUploadedAsync(order);
        await _db.SaveChangesAsync();

        return Ok(new { urls = existingUrls });
    }

    // ──────────── ADMIN: SUMMARY ────────────

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpGet("admin/summary")]
    public async Task<ActionResult<OrderAdminSummaryDto>> GetOrderSummary()
    {
        var now = DateTimeOffset.UtcNow;
        // مرز «امروز» بر اساس وقت تهران (مبنای نمایش تاریخ فارسی)، نه UTC؛
        // قبلاً مرز نیمه‌شب UTC بود و بخشی از امروز را جا می‌انداخت.
        var tehranOffset = TimeSpan.FromHours(3.5);
        var tehranNow = now.ToOffset(tehranOffset);
        var todayStart = new DateTimeOffset(tehranNow.Year, tehranNow.Month, tehranNow.Day, 0, 0, 0, tehranOffset);

        var totalCount = await _db.Orders.CountAsync();
        var pendingReceiptCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.ReceiptUploaded);
        var readyToShipCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Approved && !o.Shipments.Any());
        var failedShipmentCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Approved && o.Shipments.Any(s => s.Status == OrderShipmentStatus.Failed || s.Status == OrderShipmentStatus.Returned));
        var missingTrackingCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Approved && o.Shipments.Any() && !o.Shipments.Any(s => s.TrackingNumber != null));
        // درآمد = مبلغ واقعاً قابل پرداخت (بدون تخفیف)؛ سفارش لغو/رد شده درآمد نیست.
        var todayAmount = await _db.Orders
            .Where(o => o.CreatedAt >= todayStart
                        && o.Status != OrderStatus.Cancelled
                        && o.Status != OrderStatus.Rejected)
            .SumAsync(o => o.TotalAmount - (o.DiscountAmount ?? 0));
        var pendingActionCount = await _db.Orders.CountAsync(o =>
            o.Status == OrderStatus.ReceiptUploaded ||
            (o.Status == OrderStatus.Approved && !o.Shipments.Any()) ||
            (o.Status == OrderStatus.Approved && o.Shipments.Any(s => s.Status == OrderShipmentStatus.Failed || s.Status == OrderShipmentStatus.Returned)));

        var stageCounts = await _db.Orders
            .Where(o => o.Stage != null)
            .GroupBy(o => o.Stage!.Value)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(new OrderAdminSummaryDto
        {
            TotalCount = totalCount,
            PendingActionCount = pendingActionCount,
            PendingReceiptCount = pendingReceiptCount,
            ReadyToShipCount = readyToShipCount,
            FailedShipmentCount = failedShipmentCount,
            MissingTrackingCount = missingTrackingCount,
            TodayAmount = todayAmount,
            StageCounts = stageCounts.ToDictionary(x => (int)x.Stage, x => x.Count)
        });
    }

    // ──────────── ADMIN: ACCOUNTING (گزارش مالی فروشگاه) ────────────

    private static readonly TimeSpan TehranUtcOffset = TimeSpan.FromHours(3.5);
    private static readonly PersianCalendar JalaliCalendar = new();

    private static readonly Dictionary<OrderStatus, string> StatusLabels = new()
    {
        { OrderStatus.PendingPayment, "در انتظار پرداخت" },
        { OrderStatus.ReceiptUploaded, "رسید آپلود شده" },
        { OrderStatus.Approved, "تایید شده" },
        { OrderStatus.Completed, "تکمیل شده" },
        { OrderStatus.Rejected, "رد شده" },
        { OrderStatus.Cancelled, "لغو شده" }
    };

    private static string StatusLabel(OrderStatus status) =>
        StatusLabels.TryGetValue(status, out var label) ? label : status.ToString();

    private static DateTimeOffset TehranDayStart(DateOnly day) =>
        new(day.Year, day.Month, day.Day, 0, 0, 0, TehranUtcOffset);

    private static string JalaliDate(DateTime day)
    {
        try
        {
            return $"{JalaliCalendar.GetYear(day):0000}/{JalaliCalendar.GetMonth(day):00}/{JalaliCalendar.GetDayOfMonth(day):00}";
        }
        catch (ArgumentOutOfRangeException)
        {
            return day.ToString("yyyy/MM/dd");
        }
    }

    private static bool TryParseYmd(string? value, out DateOnly day)
    {
        day = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var s = value.Trim();
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) return true;
        if (DateOnly.TryParseExact(s, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) return true;
        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
        {
            day = DateOnly.FromDateTime(dto.DateTime);
            return true;
        }
        return false;
    }

    /// <summary>
    /// گزارش جمع‌وجور حسابداری فروشگاه برای یک بازهٔ شمسی:
    /// تعداد سفارش/کالای فروخته‌شده، تاییدشده، لغو/رد شده، مرجوعی و مبالغ آن‌ها.
    /// مبنای روز، نیمه‌شب تهران است و سفارش لغو/رد شده هرگز جزو فروش حساب نمی‌شود.
    /// </summary>
    [Authorize(Policy = "perm:admin.orders.view")]
    [HttpGet("admin/accounting")]
    public async Task<ActionResult<ShopAccountingDto>> GetShopAccounting(
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        [FromQuery] string? preset = null)
    {
        var today = DateTimeOffset.UtcNow.ToOffset(TehranUtcOffset).Date;
        var startDay = today.AddDays(-29);
        var endDay = today;
        var resolvedPreset = (preset ?? "").Trim().ToLowerInvariant();

        switch (resolvedPreset)
        {
            case "today":
                startDay = today;
                break;
            case "7d":
                startDay = today.AddDays(-6);
                break;
            case "30d":
                startDay = today.AddDays(-29);
                break;
            case "month":
                startDay = new DateTime(today.Year, today.Month, 1);
                break;
            case "year":
                startDay = new DateTime(today.Year, 1, 1);
                break;
            case "all":
                startDay = new DateTime(2015, 1, 1);
                break;
            default:
                resolvedPreset = "custom";
                if (TryParseYmd(dateFrom, out var fromDay)) startDay = fromDay.ToDateTime(TimeOnly.MinValue);
                else startDay = today.AddDays(-29);
                if (TryParseYmd(dateTo, out var toDay)) endDay = toDay.ToDateTime(TimeOnly.MinValue);
                else endDay = today;
                break;
        }

        if (startDay > endDay) (startDay, endDay) = (endDay, startDay);

        var from = TehranDayStart(DateOnly.FromDateTime(startDay));
        var toExclusive = TehranDayStart(DateOnly.FromDateTime(endDay).AddDays(1));
        var rangeDays = (int)(toExclusive - from).TotalDays;
        // چون روزشمار فقط روزهای دارای سفارش را برمی‌گرداند، بازهٔ بلند هم ارزان است؛
        // سقف فقط برای جلوگیری از بازه‌های بی‌نهایت است.
        var includeDaily = rangeDays <= 10000;

        var orderRows = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= from && o.CreatedAt < toExclusive)
            .Select(o => new
            {
                o.Status,
                o.TotalAmount,
                o.DiscountAmount,
                o.ShippingCost,
                o.CreatedAt,
                Units = o.Items.Sum(i => i.Quantity)
            })
            .ToListAsync();

        var returnRows = await _db.OrderReturns.AsNoTracking()
            .Where(r => r.CreatedAt >= from && r.CreatedAt < toExclusive)
            .Select(r => new { r.IsRefunded, r.RefundAmount, r.CreatedAt })
            .ToListAsync();

        var report = new ShopAccountingDto
        {
            DateFrom = JalaliDate(startDay),
            DateTo = JalaliDate(endDay),
            Preset = resolvedPreset,
            RangeDays = rangeDays,
            IncludeDaily = includeDaily
        };

        var statusTotals = new Dictionary<OrderStatus, (int Count, decimal Amount)>
        {
            { OrderStatus.PendingPayment, (0, 0m) },
            { OrderStatus.ReceiptUploaded, (0, 0m) },
            { OrderStatus.Approved, (0, 0m) },
            { OrderStatus.Completed, (0, 0m) },
            { OrderStatus.Rejected, (0, 0m) },
            { OrderStatus.Cancelled, (0, 0m) }
        };

        Dictionary<DateTime, ShopAccountingDayDto>? daily = includeDaily ? new() : null;
        if (daily != null)
        {
            for (var day = startDay; day <= endDay; day = day.AddDays(1))
            {
                daily[day] = new ShopAccountingDayDto
                {
                    Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    JalaliDate = JalaliDate(day)
                };
            }
        }

        foreach (var row in orderRows)
        {
            // مبلغ قابل پرداخت = کل سفارش (با ارسال) منهای تخفیف
            var payable = row.TotalAmount - (row.DiscountAmount ?? 0m);
            var isActive = row.Status is not (OrderStatus.Cancelled or OrderStatus.Rejected);
            var isConfirmed = row.Status is OrderStatus.Approved or OrderStatus.Completed;

            report.OrdersCount++;
            statusTotals[row.Status] = (statusTotals[row.Status].Count + 1, statusTotals[row.Status].Amount + payable);

            if (isActive)
            {
                report.ActiveCount++;
                report.GrossAmount += row.TotalAmount;
                report.DiscountTotal += row.DiscountAmount ?? 0m;
                report.ShippingTotal += row.ShippingCost;
                report.SellAmount += payable;
                report.UnitsSold += row.Units;

                if (isConfirmed)
                {
                    report.ConfirmedCount++;
                    report.ConfirmedAmount += payable;
                }
                if (row.Status == OrderStatus.Completed) report.CompletedCount++;
                if (row.Status == OrderStatus.PendingPayment)
                {
                    report.PendingCount++;
                    report.PendingAmount += payable;
                }
                if (row.Status == OrderStatus.ReceiptUploaded)
                {
                    report.ReceiptCount++;
                    report.ReceiptAmount += payable;
                }
            }
            else
            {
                report.CancelledAmount += payable;
                report.UnitsCancelled += row.Units;
                if (row.Status == OrderStatus.Cancelled) report.CancelledCount++;
                else report.RejectedCount++;
            }

            if (daily != null)
            {
                var dayKey = row.CreatedAt.ToOffset(TehranUtcOffset).Date;
                if (daily.TryGetValue(dayKey, out var bucket))
                {
                    bucket.OrdersCount++;
                    if (isActive)
                    {
                        bucket.Units += row.Units;
                        bucket.Amount += payable;
                        if (isConfirmed)
                        {
                            bucket.ConfirmedCount++;
                            bucket.ConfirmedAmount += payable;
                        }
                    }
                    else
                    {
                        bucket.CancelledAmount += payable;
                    }
                }
            }
        }

        foreach (var ret in returnRows)
        {
            report.ReturnsCount++;
            if (ret.IsRefunded)
            {
                report.RefundedCount++;
                report.RefundAmount += ret.RefundAmount;
            }

            if (daily != null)
            {
                var dayKey = ret.CreatedAt.ToOffset(TehranUtcOffset).Date;
                if (daily.TryGetValue(dayKey, out var bucket))
                    bucket.RefundAmount += ret.IsRefunded ? ret.RefundAmount : 0m;
            }
        }

        report.NetRevenue = report.ConfirmedAmount - report.RefundAmount;
        report.AvgOrderAmount = report.ActiveCount > 0 ? report.SellAmount / report.ActiveCount : 0m;

        if (daily != null)
        {
            // فقط روزهایی که واقعاً سفارشی در آن‌ها ثبت شده (یا مرجوعی داشته‌اند)؛
            // روزهای خالی و بدون سفارش در روزشمار نمایش داده نمی‌شوند.
            report.Daily = daily.Values
                .Where(d => d.OrdersCount > 0 || d.RefundAmount > 0)
                .OrderBy(d => d.Date)
                .ToList();
        }

        var statusOrder = new[]
        {
            OrderStatus.PendingPayment,
            OrderStatus.ReceiptUploaded,
            OrderStatus.Approved,
            OrderStatus.Completed,
            OrderStatus.Rejected,
            OrderStatus.Cancelled
        };
        var statusLabels = StatusLabels;
        report.Statuses = statusOrder
            .Select(s => new ShopAccountingStatusDto
            {
                Key = s.ToString(),
                Label = statusLabels[s],
                Count = statusTotals[s].Count,
                Amount = statusTotals[s].Amount
            })
            .ToList();

        return Ok(report);
    }

    /// <summary>
    /// جزئیات یک روز از روزشمار فروش: سفارش‌های همان روز به‌همراه اقلام، قیمت واحد،
    /// تخفیف، هزینه ارسال، وضعیت و مرجوعی‌ها. مبنای روز، نیمه‌شب تهران است.
    /// </summary>
    [Authorize(Policy = "perm:admin.orders.view")]
    [HttpGet("admin/accounting/day")]
    public async Task<ActionResult<ShopAccountingDayDetailDto>> GetShopAccountingDay([FromQuery] string? date = null)
    {
        if (!TryParseYmd(date, out var day))
            return BadRequest("تاریخ نامعتبر است.");

        var from = TehranDayStart(day);
        var toExclusive = TehranDayStart(day.AddDays(1));

        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= from && o.CreatedAt < toExclusive)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var returnRows = await _db.OrderReturns.AsNoTracking()
            .Where(r => r.CreatedAt >= from && r.CreatedAt < toExclusive)
            .Select(r => new
            {
                r.Id,
                r.RefundAmount,
                r.IsRefunded,
                r.ReturnReason,
                r.CreatedAt,
                r.Order.OrderNumber
            })
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var detail = new ShopAccountingDayDetailDto
        {
            Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            JalaliDate = JalaliDate(day.ToDateTime(TimeOnly.MinValue))
        };

        foreach (var o in orders)
        {
            var payable = o.TotalAmount - (o.DiscountAmount ?? 0m);
            var isActive = o.Status is not (OrderStatus.Cancelled or OrderStatus.Rejected);
            var isConfirmed = o.Status is OrderStatus.Approved or OrderStatus.Completed;

            detail.OrdersCount++;
            detail.DiscountTotal += o.DiscountAmount ?? 0m;
            detail.ShippingTotal += o.ShippingCost;

            if (isActive)
            {
                detail.ActiveCount++;
                detail.Amount += payable;
                detail.Units += o.Items.Sum(i => i.Quantity);
                if (isConfirmed)
                {
                    detail.ConfirmedCount++;
                    detail.ConfirmedAmount += payable;
                }
            }
            else
            {
                detail.CancelledCount++;
                detail.CancelledAmount += payable;
            }

            detail.Orders.Add(new ShopAccountingOrderDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                CustomerName = o.FullName,
                Phone = o.PhoneNumber,
                Time = o.CreatedAt.ToOffset(TehranUtcOffset).ToString("HH:mm", CultureInfo.InvariantCulture),
                StatusKey = o.Status.ToString(),
                StatusLabel = StatusLabel(o.Status),
                IsCancelled = !isActive,
                IsConfirmed = isConfirmed,
                CouponCode = o.CouponCode,
                TotalAmount = o.TotalAmount,
                DiscountAmount = o.DiscountAmount ?? 0m,
                ShippingCost = o.ShippingCost,
                Payable = payable,
                Items = o.Items.Select(i => new ShopAccountingItemDto
                {
                    Name = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.Price,
                    Total = i.Price * i.Quantity
                }).ToList()
            });
        }

        foreach (var r in returnRows)
        {
            if (r.IsRefunded) detail.RefundAmount += r.RefundAmount;

            detail.Returns.Add(new ShopAccountingReturnDto
            {
                Id = r.Id,
                OrderNumber = r.OrderNumber,
                Time = r.CreatedAt.ToOffset(TehranUtcOffset).ToString("HH:mm", CultureInfo.InvariantCulture),
                Reason = r.ReturnReason ?? "",
                IsRefunded = r.IsRefunded,
                RefundAmount = r.RefundAmount
            });
        }

        return Ok(detail);
    }

    // ──────────── ADMIN: LIST (PAGINATED) ────────────

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpGet("admin/all")]
    public async Task<ActionResult<OrderAdminListResponse>> GetAllOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] OrderStage? stage = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        [FromQuery] bool? hasTracking = null,
        [FromQuery] bool? needsReship = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _db.Orders.AsNoTracking().AsQueryable();

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        if (stage.HasValue)
            query = query.Where(o => o.Stage == stage.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(s) ||
                o.FullName.ToLower().Contains(s) ||
                o.PhoneNumber.Contains(s) ||
                (o.TrackingNumber != null && o.TrackingNumber.ToLower().Contains(s)) ||
                (o.CityName != null && o.CityName.ToLower().Contains(s)) ||
                (o.ProvinceName != null && o.ProvinceName.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(dateFrom) && DateTimeOffset.TryParse(dateFrom, out var df))
            query = query.Where(o => o.CreatedAt >= df);

        if (!string.IsNullOrWhiteSpace(dateTo) && DateTimeOffset.TryParse(dateTo, out var dt))
            query = query.Where(o => o.CreatedAt <= dt);

        if (hasTracking.HasValue)
        {
            if (hasTracking.Value)
                query = query.Where(o => o.TrackingNumber != null);
            else
                query = query.Where(o => o.TrackingNumber == null);
        }

        if (needsReship.HasValue && needsReship.Value)
        {
            query = query.Where(o => o.Status == OrderStatus.Approved && o.Shipments.Any(s => s.Status == OrderShipmentStatus.Failed || s.Status == OrderShipmentStatus.Returned));
        }

        var totalCount = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                DiscountAmount = order.DiscountAmount,
                CouponCode = order.CouponCode,
                FullName = order.FullName,
                PhoneNumber = order.PhoneNumber,
                ProvinceId = order.ProvinceId,
                CityId = order.CityId,
                ProvinceName = order.ProvinceName,
                CityName = order.CityName,
                PostalCode = order.PostalCode,
                TrackingNumber = order.TrackingNumber,
                PaymentProvider = order.PaymentProvider,
                PaymentReference = order.PaymentReference,
                ShippingMethodId = order.ShippingMethodId,
                ShippingMethodName = order.ShippingMethodName,
                ShippingCost = order.ShippingCost,                
                ShippingIsFree = order.ShippingIsFree,
                ShipmentCount = order.Shipments.Count,
                LatestTrackingNumber = order.Shipments
                    .Where(s => s.TrackingNumber != null)
                    .OrderByDescending(s => s.AttemptNumber)
                    .Select(s => s.TrackingNumber)
                    .FirstOrDefault(),
                ReturnCount = order.Returns.Count,
                Stage = order.Stage,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Price = i.Price,
                    Quantity = i.Quantity
                }).ToList()
            })
            .ToListAsync();

        await ApplyProductImagesAsync(orders);

        var pendingActionCount = await _db.Orders.CountAsync(o =>
            o.Status == OrderStatus.ReceiptUploaded ||
            (o.Status == OrderStatus.Approved && !o.Shipments.Any()) ||
            (o.Status == OrderStatus.Approved && o.Shipments.Any(s => s.Status == OrderShipmentStatus.Failed || s.Status == OrderShipmentStatus.Returned)));

        return Ok(new OrderAdminListResponse
        {
            Items = orders,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            PendingActionCount = pendingActionCount
        });
    }

    // ──────────── ADMIN: DETAIL ────────────

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpGet("admin/{id:int}")]
    public async Task<ActionResult<OrderDto>> GetAdminOrderDetail(int id)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();

        var items = await _db.OrderItems.Where(i => i.OrderId == id).ToListAsync();
        var shipments = await _db.OrderShipments.Where(s => s.OrderId == id).OrderBy(s => s.AttemptNumber).ToListAsync();
        var events = await _db.OrderEvents.Where(e => e.OrderId == id).OrderBy(e => e.CreatedAt).ToListAsync();
        var returns = await _db.OrderReturns.Where(r => r.OrderId == id).OrderBy(r => r.CreatedAt).ToListAsync();

        var dto = MapOrderToDto(order, items, shipments, events, returns);
        await ApplyProductImagesAsync(new[] { dto });
        return Ok(dto);
    }

    // ──────────── ADMIN: DELETE (SAFE) ────────────

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpDelete("admin/{id:int}")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();

        var previousStatus = order.Status;

        var orderItems = await LoadOrderItemsAsync(order.Id);
        var restockCandidates = await GetOutOfStockProductIdsAsync(orderItems);

        await using var tx = await _db.Database.BeginTransactionAsync();

        await RestoreStockForOrderAsync(order);

        await ReleaseCouponUsageAsync(order.CouponId);

        await AddOrderEventAsync(order.Id, "deleted", previousStatus, null, "سفارش حذف شد توسط مدیر");
        await _db.SaveChangesAsync();

        DeleteReceiptFiles(GetReceiptUrls(order));

        var shipments = await _db.OrderShipments.Where(s => s.OrderId == id).ToListAsync();
        _db.OrderShipments.RemoveRange(shipments);

        var events = await _db.OrderEvents.Where(e => e.OrderId == id).ToListAsync();
        _db.OrderEvents.RemoveRange(events);

        var returnItems = await _db.OrderReturns.Where(r => r.OrderId == id).ToListAsync();
        _db.OrderReturns.RemoveRange(returnItems);

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        await _stockAlerts.NotifyRestockedAsync(restockCandidates);
        return NoContent();
    }

    // ──────────── ADMIN: UPDATE INFO ────────────

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpPut("admin/{id:int}/info")]
    public async Task<IActionResult> UpdateOrderInfo(int id, [FromBody] OrderUpdateInfoRequest request)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();

        string? provinceName = null;
        string? cityName = null;
        if (request.ProvinceId.HasValue)
            provinceName = await _db.Provinces.Where(x => x.Id == request.ProvinceId.Value).Select(x => x.Name).FirstOrDefaultAsync();
        if (request.CityId.HasValue)
            cityName = await _db.Cities.Where(x => x.Id == request.CityId.Value).Select(x => x.Name).FirstOrDefaultAsync();

        order.FullName = request.FullName;
        order.PhoneNumber = request.PhoneNumber;
        order.Email = request.Email;
        order.Address = request.Address;
        order.ProvinceId = request.ProvinceId;
        order.CityId = request.CityId;
        order.ProvinceName = provinceName;
        order.CityName = cityName;
        order.PostalCode = request.PostalCode;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        await AddOrderEventAsync(order.Id, "info_updated", order.Status, order.Status, "اطلاعات خریدار بروزرسانی شد");
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ──────────── ADMIN: UPDATE STATUS ────────────

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpPut("admin/{id:int}/status")]
    public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] OrderStatusUpdateDto request)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound("سفارش یافت نشد");

        var previousStatus = order.Status;
        var stageBeforeStatusUpdate = order.Stage;

        if (request.Status == previousStatus)
            return BadRequest("وضعیت سفارش قبلاً روی همین حالت تنظیم شده است.");

        if (request.Status == OrderStatus.Completed && previousStatus != OrderStatus.Approved)
            return BadRequest("فقط سفارشی که تایید شده است می‌تواند 'تکمیل شده' شود.");

        if (!AllowedTransitions.Contains((previousStatus, request.Status)))
            return BadRequest("انتقال وضعیت انتخابی برای این سفارش مجاز نیست.");

        // حفاظت سفارش پرداخت‌شده: اگر پولی دریافت شده (PaymentDate یا مرجع پرداخت
        // ثبت شده)، لغو/رد فقط با ثبت دلیل در یادداشت مدیر مجاز است.
        var paymentRecorded = order.PaymentDate.HasValue
            || !string.IsNullOrWhiteSpace(order.PaymentReference);
        if (request.Status is OrderStatus.Rejected or OrderStatus.Cancelled
            && paymentRecorded
            && string.IsNullOrWhiteSpace(request.AdminNotes))
            return BadRequest("برای لغو یا رد سفارشِ پرداخت‌شده، ثبت دلیل در «یادداشت مدیر» الزامی است.");

        if (request.Status == OrderStatus.Approved && GetReceiptUrls(order).Count == 0 && order.PaymentProvider == null)
            return BadRequest("برای تایید سفارش، ابتدا باید رسید پرداخت آپلود شده باشد.");

        List<int> restockCandidates = new();
        if (request.Status is OrderStatus.Rejected or OrderStatus.Cancelled)
        {
            var orderItems = await LoadOrderItemsAsync(order.Id);
            restockCandidates = await GetOutOfStockProductIdsAsync(orderItems);
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        if (request.Status is OrderStatus.Rejected or OrderStatus.Cancelled)
        {
            await RestoreStockForOrderAsync(order);
            await ReleaseCouponUsageAsync(order.CouponId);
        }

        var stageAssigned = false;
        if (request.Status == OrderStatus.Approved && previousStatus is OrderStatus.PendingPayment or OrderStatus.ReceiptUploaded)
        {
            await FinalizeOrderAsync(order);
            if (order.Stage == null)
            {
                order.Stage = OrderStage.PaymentConfirmed;
                stageAssigned = true;
            }
        }

        if (request.Status == OrderStatus.Completed && order.Stage != OrderStage.Resolved)
        {
            order.Stage = OrderStage.Resolved;
            stageAssigned = true;
        }

        order.Status = request.Status;

        if (!string.IsNullOrWhiteSpace(request.AdminNotes))
        {
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyy/MM/dd HH:mm");
            var adminName = GetAdminDisplayName();
            var newNote = $"[{timestamp}] {adminName}: {request.AdminNotes}";
            order.AdminNotes = string.IsNullOrWhiteSpace(order.AdminNotes)
                ? newNote
                : order.AdminNotes + "\n" + newNote;
        }

        order.UpdatedAt = DateTimeOffset.UtcNow;

        var statusLabels = new Dictionary<OrderStatus, string>
        {
            { OrderStatus.PendingPayment, "در انتظار پرداخت" },
            { OrderStatus.ReceiptUploaded, "رسید آپلود شد" },
            { OrderStatus.Approved, "تایید شده" },
            { OrderStatus.Rejected, "رد شده" },
            { OrderStatus.Cancelled, "لغو شده" },
            { OrderStatus.Completed, "تکمیل شده" }
        };
        var statusLabel = statusLabels.TryGetValue(request.Status, out var lbl) ? lbl : request.Status.ToString();
        var autoStageNote = stageAssigned && request.Status == OrderStatus.Completed ? "؛ تنظیم خودکار مرحله «حمل شده»" : "";
        await AddOrderEventAsync(order.Id, "status_changed", previousStatus, request.Status, $"تغییر وضعیت به «{statusLabel}»{autoStageNote}");
        await _notificationEvents.NotifyOrderStatusChangedAsync(order, previousStatus);
        if (stageAssigned)
        {
            await _db.OrderEvents.AddAsync(new OrderEvent
            {
                OrderId = order.Id,
                EventType = "stage_changed",
                FromStatus = order.Status,
                ToStatus = order.Status,
                Message = $"تغییر مرحله به «{OrderStageInfo.Label(order.Stage!.Value)}»|{order.Stage!.Value}|{DateTimeOffset.UtcNow:O}",
                CreatedBy = GetAdminDisplayName(),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await _notificationEvents.NotifyOrderStageChangedAsync(order, stageBeforeStatusUpdate);
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        await _stockAlerts.NotifyRestockedAsync(restockCandidates);
        return NoContent();
    }

    // ──────────── ADMIN: UPDATE STAGE ────────────

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpPut("admin/{id:int}/stage")]
    public async Task<IActionResult> UpdateOrderStage(int id, [FromBody] OrderStageUpdateDto request)
    {
        if (!Enum.IsDefined(typeof(OrderStage), request.Stage))
            return BadRequest("مرحله انتخابی معتبر نیست.");

        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound("سفارش یافت نشد");

        if (order.Status is OrderStatus.Rejected or OrderStatus.Cancelled or OrderStatus.PendingPayment)
            return BadRequest("برای سفارش در وضعیت فعلی امکان تغییر مرحله وجود ندارد.");

        if (order.Stage == request.Stage)
            return BadRequest("سفارش قبلاً روی همین مرحله تنظیم شده است.");

        if (!string.IsNullOrWhiteSpace(request.AdminNotes))
        {
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyy/MM/dd HH:mm");
            var adminName = GetAdminDisplayName();
            var newNote = $"[{timestamp}] {adminName}: {request.AdminNotes}";
            order.AdminNotes = string.IsNullOrWhiteSpace(order.AdminNotes)
                ? newNote
                : order.AdminNotes + "\n" + newNote;
        }

        var previousStage = order.Stage;
        order.Stage = request.Stage;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        var stageLabel = OrderStageInfo.Label(request.Stage);
        _db.OrderEvents.Add(new OrderEvent
        {
            OrderId = order.Id,
            EventType = "stage_changed",
            FromStatus = order.Status,
            ToStatus = order.Status,
            Message = $"تغییر مرحله به «{stageLabel}»|{request.Stage}|{DateTimeOffset.UtcNow:O}",
            CreatedBy = GetAdminDisplayName(),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _notificationEvents.NotifyOrderStageChangedAsync(order, previousStage);

        if (request.Stage == OrderStage.Resolved && order.Status == OrderStatus.Approved)
        {
            order.Status = OrderStatus.Completed;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await AddOrderEventAsync(order.Id, "status_changed", OrderStatus.Approved, OrderStatus.Completed,
                "تغییر وضعیت به «تکمیل شده» (با رسیدن به مرحله حمل شده)");
            await _notificationEvents.NotifyOrderStatusChangedAsync(order, OrderStatus.Approved);
        }
        else if (order.Status == OrderStatus.Completed && request.Stage != OrderStage.Resolved)
        {
            order.Status = OrderStatus.Approved;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await AddOrderEventAsync(order.Id, "status_changed", OrderStatus.Completed, OrderStatus.Approved,
                $"خروج از «تکمیل شده» (تغییر مرحله از «حمل شده» به «{stageLabel}»)");
            await _notificationEvents.NotifyOrderStatusChangedAsync(order, OrderStatus.Completed);
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ──────────── ADMIN: SHIPMENT CRUD ────────────

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpPost("admin/{id:int}/shipments")]
    public async Task<ActionResult<OrderShipmentDto>> CreateShipment(int id, [FromBody] OrderShipmentCreateDto request)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound("سفارش یافت نشد");

        if (order.Status is OrderStatus.Completed or OrderStatus.Rejected or OrderStatus.Cancelled)
            return BadRequest("در وضعیت فعلی سفارش امکان ایجاد مرسوله وجود ندارد.");

        var attemptNumber = await _db.OrderShipments.CountAsync(s => s.OrderId == id) + 1;

        var shipment = new OrderShipment
        {
            OrderId = id,
            AttemptNumber = attemptNumber,
            Carrier = request.Carrier,
            TrackingNumber = request.TrackingNumber,
            Notes = request.Notes,
            FailureReason = request.FailureReason,
            Status = OrderShipmentStatus.Pending,
            CreatedBy = GetAdminDisplayName(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.OrderShipments.Add(shipment);

        if (attemptNumber > 1)
            await AddOrderEventAsync(id, "reship_created", order.Status, order.Status, $"ارسال مجدد (تلاش #{attemptNumber}) - شرکت حمل: {request.Carrier}");
        else
            await AddOrderEventAsync(id, "shipment_created", order.Status, order.Status, $"مرسوله ایجاد شد - شرکت حمل: {request.Carrier}");

        if (!string.IsNullOrWhiteSpace(request.TrackingNumber))
            order.TrackingNumber = request.TrackingNumber;

        await _notificationEvents.NotifyAsync(
            eventType: "shipment.created",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["Carrier"] = shipment.Carrier,
                ["TrackingNumber"] = shipment.TrackingNumber ?? "—",
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"shipment.created:{order.Id}:{attemptNumber}",
            fallbackSeverity: NotificationSeverity.Info,
            fallbackTitle: "مرسوله شما ثبت شد",
            fallbackBody: $"مرسوله سفارش {order.OrderNumber} با شرکت حمل {shipment.Carrier} ثبت شد.",
            fallbackUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId,
            phone: order.PhoneNumber);

        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new OrderShipmentDto
        {
            Id = shipment.Id,
            AttemptNumber = shipment.AttemptNumber,
            Carrier = shipment.Carrier,
            TrackingNumber = shipment.TrackingNumber,
            Status = shipment.Status.ToString(),
            FailureReason = shipment.FailureReason,
            Notes = shipment.Notes,
            ShippedAt = shipment.ShippedAt,
            DeliveredAt = shipment.DeliveredAt,
            CreatedAt = shipment.CreatedAt,
            CreatedBy = shipment.CreatedBy
        });
    }

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpPut("admin/{orderId:int}/shipments/{shipmentId:int}")]
    public async Task<IActionResult> UpdateShipment(int orderId, int shipmentId, [FromBody] OrderShipmentUpdateDto request)
    {
        var shipment = await _db.OrderShipments.FirstOrDefaultAsync(s => s.Id == shipmentId && s.OrderId == orderId);
        if (shipment == null) return NotFound("مرسوله یافت نشد");

        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return NotFound();

        var previousStatus = shipment.Status;

        if (!string.IsNullOrWhiteSpace(request.Carrier))
            shipment.Carrier = request.Carrier;
        if (request.TrackingNumber != null)
            shipment.TrackingNumber = request.TrackingNumber;
        if (request.Notes != null)
            shipment.Notes = request.Notes;
        if (request.FailureReason != null)
            shipment.FailureReason = request.FailureReason;
        if (request.Status.HasValue)
        {
            shipment.Status = request.Status.Value;

            if (request.Status == OrderShipmentStatus.Shipped && !shipment.ShippedAt.HasValue)
                shipment.ShippedAt = DateTimeOffset.UtcNow;
            if (request.Status == OrderShipmentStatus.Delivered && !shipment.DeliveredAt.HasValue)
                shipment.DeliveredAt = DateTimeOffset.UtcNow;

            var shipmentStatusLabels = new Dictionary<OrderShipmentStatus, string>
            {
                { OrderShipmentStatus.Pending, "در انتظار ارسال" },
                { OrderShipmentStatus.Shipped, "ارسال شد" },
                { OrderShipmentStatus.InTransit, "در مسیر" },
                { OrderShipmentStatus.Delivered, "تحویل شد" },
                { OrderShipmentStatus.Failed, "تحویل ناموفق" },
                { OrderShipmentStatus.Returned, "برگشتی" },
                { OrderShipmentStatus.Cancelled, "لغو شد" }
            };
            var label = shipmentStatusLabels.TryGetValue(request.Status.Value, out var sl) ? sl : request.Status.Value.ToString();
            await AddOrderEventAsync(orderId, "shipment_status_changed", order.Status, order.Status, $"مرسوله #{shipment.AttemptNumber}: {label}");

            if (request.Status.Value != previousStatus)
            {
                var shipmentSeverity = request.Status.Value switch
                {
                    OrderShipmentStatus.Delivered => NotificationSeverity.Success,
                    OrderShipmentStatus.Failed or OrderShipmentStatus.Returned or OrderShipmentStatus.Cancelled => NotificationSeverity.Warning,
                    _ => NotificationSeverity.Info
                };
                await _notificationEvents.NotifyAsync(
                    eventType: "shipment.status_changed",
                    values: new Dictionary<string, string?>
                    {
                        ["OrderNumber"] = order.OrderNumber,
                        ["Status"] = label,
                        ["TrackingNumber"] = shipment.TrackingNumber ?? "—",
                        ["OrderId"] = order.Id.ToString()
                    },
                    idempotencyKey: $"shipment.status:{shipment.Id}:{request.Status.Value}:{DateTimeOffset.UtcNow:yyyyMMddHH}",
                    fallbackSeverity: shipmentSeverity,
                    fallbackTitle: "وضعیت مرسوله تغییر کرد",
                    fallbackBody: $"وضعیت مرسوله سفارش {order.OrderNumber} به «{label}» تغییر کرد.",
                    fallbackUrl: $"/my-orders/{order.Id}",
                    appUserId: order.UserId,
                    phone: order.PhoneNumber);
            }
        }

        if (request.Status == OrderShipmentStatus.Delivered && order.Status != OrderStatus.Completed)
        {
            var previousOrderStatus = order.Status;
            order.Status = OrderStatus.Completed;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await AddOrderEventAsync(orderId, "status_changed", previousOrderStatus, OrderStatus.Completed, "سفارش به‌طور خودکار تکمیل شد (تحویل مرسوله)");
            await _notificationEvents.NotifyOrderStatusChangedAsync(order, previousOrderStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.TrackingNumber))
            order.TrackingNumber = request.TrackingNumber;

        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpDelete("admin/{orderId:int}/shipments/{shipmentId:int}")]
    public async Task<IActionResult> DeleteShipment(int orderId, int shipmentId)
    {
        var shipment = await _db.OrderShipments.FirstOrDefaultAsync(s => s.Id == shipmentId && s.OrderId == orderId);
        if (shipment == null) return NotFound();

        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return NotFound();

        await AddOrderEventAsync(orderId, "shipment_deleted", order.Status, order.Status, $"مرسوله #{shipment.AttemptNumber} حذف شد");

        _db.OrderShipments.Remove(shipment);
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ──────────── ADMIN: RETURNS ────────────

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpPost("admin/{orderId:int}/returns")]
    public async Task<ActionResult<OrderReturnDto>> CreateReturn(int orderId, [FromBody] OrderReturnCreateRequest request)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return NotFound();

        if (request.RefundAmount <= 0)
            return BadRequest("مبلغ مرجوعی باید بزرگ‌تر از صفر باشد.");

        if (request.RefundAmount > order.TotalAmount - (order.DiscountAmount ?? 0))
            return BadRequest("مبلغ مرجوعی بیشتر از مبلغ سفارش است.");

        var userName = User.Identity?.Name ?? "admin";

        var orderReturn = new OrderReturn
        {
            OrderId = orderId,
            RefundAmount = request.RefundAmount,
            RefundCardNumber = request.RefundCardNumber,
            RefundBankName = request.RefundBankName,
            RefundDate = request.RefundDate,
            ReturnReason = request.ReturnReason,
            AdminNotes = request.AdminNotes,
            IsRefunded = false,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userName
        };

        _db.OrderReturns.Add(orderReturn);
        await _db.SaveChangesAsync();

        await AddOrderEventAsync(orderId, "return_created", order.Status, order.Status,
            $"مرجوعی ثبت شد — مبلغ: {request.RefundAmount:N0} تومان{(string.IsNullOrEmpty(request.RefundCardNumber) ? "" : $" — کارت: {request.RefundCardNumber}")}");
        await _notificationEvents.NotifyReturnCreatedAsync(order, orderReturn);
        await _db.SaveChangesAsync();

        return Ok(new OrderReturnDto
        {
            Id = orderReturn.Id,
            OrderId = orderReturn.OrderId,
            RefundAmount = orderReturn.RefundAmount,
            RefundCardNumber = orderReturn.RefundCardNumber,
            RefundBankName = orderReturn.RefundBankName,
            RefundDate = orderReturn.RefundDate,
            RefundReceiptImageUrl = orderReturn.RefundReceiptImageUrl,
            ReturnReason = orderReturn.ReturnReason,
            AdminNotes = orderReturn.AdminNotes,
            IsRefunded = orderReturn.IsRefunded,
            CreatedAt = orderReturn.CreatedAt,
            CreatedBy = orderReturn.CreatedBy
        });
    }

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpPost("admin/returns/receipt")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<string>> UploadReturnReceipt(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length <= 0)
            return BadRequest("فایل رسید ارسال نشده است.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";
        var safeExt = ext.ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowed.Contains(safeExt))
            return BadRequest("فرمت فایل مجاز نیست. فقط jpg, jpeg, png, webp");

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "return-receipts");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid():N}{safeExt}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, ct);
        }

        return Ok($"/uploads/return-receipts/{fileName}");
    }

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpPost("admin/{orderId:int}/returns/{returnId:int}/refund")]
    public async Task<IActionResult> MarkRefunded(int orderId, int returnId, [FromBody] OrderReturnRefundRequest request)
    {
        var orderReturn = await _db.OrderReturns.FirstOrDefaultAsync(r => r.Id == returnId && r.OrderId == orderId);
        if (orderReturn == null) return NotFound();

        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return NotFound();

        if (orderReturn.IsRefunded)
            return BadRequest("این مورد قبلاً واریز شده است.");

        var userName = User.Identity?.Name ?? "admin";

        orderReturn.IsRefunded = true;
        orderReturn.RefundCardNumber = request.RefundCardNumber;
        orderReturn.RefundBankName = request.RefundBankName;
        orderReturn.RefundDate = request.RefundDate ?? DateTimeOffset.UtcNow;
        orderReturn.RefundReceiptImageUrl = request.RefundReceiptImageUrl ?? orderReturn.RefundReceiptImageUrl;
        orderReturn.AdminNotes = request.AdminNotes;

        await AddOrderEventAsync(orderId, "return_refunded", order.Status, order.Status,
            $"واریز مرجوعی ثبت شد — مبلغ: {orderReturn.RefundAmount:N0} تومان — کارت: {request.RefundCardNumber}");
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _notificationEvents.NotifyReturnRefundedAsync(order, orderReturn);
        await _db.SaveChangesAsync();

        return Ok(new { message = "واریز ثبت شد." });
    }

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpDelete("admin/{orderId:int}/returns/{returnId:int}")]
    public async Task<IActionResult> DeleteReturn(int orderId, int returnId)
    {
        var orderReturn = await _db.OrderReturns.FirstOrDefaultAsync(r => r.Id == returnId && r.OrderId == orderId);
        if (orderReturn == null) return NotFound();

        if (orderReturn.IsRefunded)
            return BadRequest("این مورد واریز شده و قابل حذف نیست.");

        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return NotFound();

        await AddOrderEventAsync(orderId, "return_deleted", order.Status, order.Status,
            $"مرجوعی #{orderReturn.Id} حذف شد");

        await _notificationEvents.NotifyAsync(
            eventType: "return.rejected",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["Reason"] = string.IsNullOrWhiteSpace(orderReturn.ReturnReason)
                    ? (string.IsNullOrWhiteSpace(orderReturn.AdminNotes) ? "—" : orderReturn.AdminNotes!)
                    : orderReturn.ReturnReason!,
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"return.rejected:{orderReturn.Id}",
            fallbackSeverity: NotificationSeverity.Warning,
            fallbackTitle: "درخواست مرجوعی رد شد",
            fallbackBody: $"درخواست مرجوعی سفارش {order.OrderNumber} رد شد.",
            fallbackUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId,
            phone: order.PhoneNumber);

        _db.OrderReturns.Remove(orderReturn);
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ──────────── RETURN REQUEST BY CUSTOMER ────────────

    [Authorize]
    [HttpPost("{orderId:int}/return-request")]
    [EnableRateLimiting("commerce")]
    public async Task<IActionResult> CreateReturnRequest(int orderId, [FromBody] OrderReturnRequestDto dto)
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings == null || !settings.EnableReturnRequests)
            return BadRequest("درخواست مرجوعی در حال حاضر غیرفعال است.");

        var userId = await ResolveSiteUserIdAsync();
        if (userId == null) return Unauthorized();

        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return NotFound();
        if (order.UserId != userId) return NotFound();

        if (order.Status is OrderStatus.Cancelled or OrderStatus.Rejected)
            return BadRequest("برای سفارش لغو شده امکان درخواست مرجوعی وجود ندارد.");

        var hasOpenRequest = await _db.OrderReturns
            .AnyAsync(r => r.OrderId == orderId && !r.IsRefunded && (r.CreatedBy ?? "").StartsWith("customer:"));
        if (hasOpenRequest)
            return BadRequest("برای این سفارش یک درخواست مرجوعی در انتظار بررسی ثبت شده است.");

        var reason = (dto.Reason ?? string.Empty).Trim();

        var orderReturn = new OrderReturn
        {
            OrderId = orderId,
            RefundAmount = 0,
            ReturnReason = reason,
            CreatedBy = $"customer:{userId}",
            IsRefunded = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.OrderReturns.Add(orderReturn);

        await AddOrderEventAsync(orderId, "return_requested", order.Status, order.Status,
            $"درخواست مرجوعی مشتری: {reason}");

        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        await _notificationEvents.NotifyAsync(
            eventType: "return.requested",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["Reason"] = reason,
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"return.requested:{orderReturn.Id}",
            fallbackSeverity: NotificationSeverity.Warning,
            fallbackTitle: "درخواست مرجوعی جدید",
            fallbackBody: $"مشتری برای سفارش {order.OrderNumber} درخواست مرجوعی ثبت کرده است.",
            fallbackUrl: $"/admin/orders/{order.Id}");
        await _db.SaveChangesAsync();

        return Ok(new
        {
            id = orderReturn.Id,
            message = "درخواست مرجوعی ثبت شد و پس از بررسی از طریق پنل کاربری به شما اطلاع داده می‌شود."
        });
    }

    // ──────────── PAYMENT ENDPOINTS (UNCHANGED) ────────────

    [AllowAnonymous]
    [HttpGet("{id:int}/payment/intent")]
    public async Task<ActionResult<PaymentIntentResult>> CreatePaymentIntent(int id, [FromQuery] int? gatewayId, [FromQuery] string? token, CancellationToken ct)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();

        if (!await CanAccessOrderAsync(order, token))
            return NotFound();

        if (order.Status is OrderStatus.Approved or OrderStatus.Completed)
            return Ok(new PaymentIntentResult { Success = false, Message = "این سفارش قبلاً پرداخت شده است." });
        if (order.Status is OrderStatus.Rejected or OrderStatus.Cancelled)
            return Ok(new PaymentIntentResult { Success = false, Message = "این سفارش قابل پرداخت نیست." });
        if (order.TotalAmount <= 0)
            return Ok(new PaymentIntentResult { Success = false, Message = "مبلغ سفارش صفر است؛ نیازی به پرداخت آنلاین نیست." });

        int resolvedGatewayId;
        if (gatewayId.HasValue && gatewayId.Value > 0)
        {
            resolvedGatewayId = gatewayId.Value;
        }
        else
        {
            var online = await _paymentGatewayFactory.GetOnlineGatewaysAsync(ct);
            if (online.Count == 0)
                return Ok(new PaymentIntentResult { Success = false, Message = "درگاه پرداخت آنلاین فعالی پیکربندی نشده است." });
            resolvedGatewayId = online[0].Id;
        }

        var gatewayDto = await _paymentGatewayFactory.GetGatewayDtoAsync(resolvedGatewayId, ct);
        if (gatewayDto == null || !gatewayDto.Online || !gatewayDto.IsActive)
            return Ok(new PaymentIntentResult { Success = false, Message = "درگاه پرداخت انتخابی در دسترس نیست." });
        if (gatewayDto.SupportsSandbox == false && !gatewayDto.IsConfigured && gatewayDto.Fields.Any(f => f.Required))
            return Ok(new PaymentIntentResult { Success = false, Message = "درگاه پرداخت هنوز پیکربندی نشده است." });

        var gateway = await _paymentGatewayFactory.GetGatewayByIdAsync(resolvedGatewayId, ct);
        if (gateway == null)
            return Ok(new PaymentIntentResult { Success = false, Message = "درگاه پرداخت انتخابی در دسترس نیست." });

        order.PaymentProvider = gateway.Provider;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var intentRequest = new PaymentIntentRequest
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Amount = order.TotalAmount - (order.DiscountAmount ?? 0),
            DiscountAmount = order.DiscountAmount,
            Description = $"پرداخت سفارش {order.OrderNumber}",
            CallbackUrl = BuildPaymentCallbackUrl(order.Id, gatewayDto.CallbackBaseUrl),
            Mobile = order.PhoneNumber,
            Email = order.Email
        };

        var result = await gateway.CreateIntentAsync(intentRequest, ct);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/payment/callback")]
    public async Task<IActionResult> PaymentCallback(int id, CancellationToken ct)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null)
            return Content(PaymentResultHtml(false, "سفارش یافت نشد.", null), "text/html; charset=utf-8");

        // وضعیت سفارش ملاک است، نه وجود PaymentReference: سفارشی که ادمین
        // تایید کرده دیگر نباید از مسیر callback دوباره «پرداخت» شود.
        if (order.Status is OrderStatus.Approved or OrderStatus.Completed)
            return Content(PaymentResultHtml(true, "این پرداخت قبلاً تایید شده است.", order), "text/html; charset=utf-8");

        // سفارش لغو/رد شده نباید هرگز با یک callback قابل بازگشت به «تایید شده» باشد.
        if (order.Status is OrderStatus.Cancelled or OrderStatus.Rejected)
            return Content(PaymentResultHtml(false, "این سفارش لغو شده است و قابل پرداخت نیست.", order), "text/html; charset=utf-8");

        var providerKey = order.PaymentProvider.HasValue
            ? PaymentProviderCatalog.ToProviderKey(order.PaymentProvider.Value)
            : null;
        if (string.IsNullOrEmpty(providerKey) || providerKey == "manual")
            return Content(PaymentResultHtml(false, "شناسه درگاه برای این سفارش ثبت نشده است.", order), "text/html; charset=utf-8");

        // درگاه مجازی فقط در محیط توسعه (یا با فعال‌سازی صریح Payment:AllowSimulate)
        // معتبر است؛ همان قیدی که روی /api/payment/simulate اعمال می‌شود. بدون آن،
        // هر بازدیدکننده‌ای با «?Status=OK» می‌توانست سفارش را پرداخت‌شده کند.
        if (string.Equals(providerKey, "virtual", StringComparison.OrdinalIgnoreCase)
            && !_env.IsDevelopment()
            && !_config.GetValue("Payment:AllowSimulate", false))
            return Content(PaymentResultHtml(false, "درگاه تست در این محیط غیرفعال است.", order), "text/html; charset=utf-8");

        var gateway = await _paymentGatewayFactory.GetGatewayByProviderAsync(providerKey, ct);
        if (gateway == null)
            return Content(PaymentResultHtml(false, "درگاه پرداخت این سفارش فعال نیست.", order), "text/html; charset=utf-8");

        var successParam = Request.Query["success"].FirstOrDefault();
        var status = Request.Query["Status"].FirstOrDefault() ?? "";
        if (string.Equals(successParam, "1", StringComparison.Ordinal)
            || string.Equals(successParam, "true", StringComparison.OrdinalIgnoreCase))
            status = "OK";

        var payload = new PaymentCallbackPayload
        {
            OrderId = order.Id,
            Status = string.IsNullOrEmpty(status) ? Request.Query["status"].FirstOrDefault() ?? "" : status,
            Authority = Request.Query["Authority"].FirstOrDefault() ?? Request.Query["trackId"].FirstOrDefault() ?? "",
            Amount = order.TotalAmount - (order.DiscountAmount ?? 0),
            RefId = Request.Query["refNumber"].FirstOrDefault() ?? Request.Query["RefId"].FirstOrDefault()
        };

        // درگاه مجازی فقط «Status=OK» را قبول می‌کند؛ بدون Authority معتبر، هر URL
        // دست‌نوشته‌ای می‌توانست سفارش را پرداخت‌شده کند. (برای درگاه‌های واقعی
        // الزامی نمی‌کنیم تا callback هیچ‌کدام از آن‌ها بشکند.)
        if (string.Equals(providerKey, "virtual", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(payload.Authority))
            return Content(PaymentResultHtml(false, "شناسه پرداخت (Authority) ارسال نشده است.", order), "text/html; charset=utf-8");

        var result = await gateway.VerifyCallbackAsync(payload, ct);

        if (!result.Success)
        {
            await _notificationEvents.NotifyAsync(
                eventType: "payment.failed",
                values: new Dictionary<string, string?>
                {
                    ["OrderNumber"] = order.OrderNumber,
                    ["Amount"] = (order.TotalAmount - (order.DiscountAmount ?? 0)).ToString("N0"),
                    ["OrderId"] = order.Id.ToString()
                },
                idempotencyKey: $"payment.failed:{order.Id}:{payload.Authority}:{DateTimeOffset.UtcNow:yyyyMMddHH}",
                fallbackSeverity: NotificationSeverity.Warning,
                fallbackTitle: "پرداخت ناموفق بود",
                fallbackBody: $"پرداخت سفارش {order.OrderNumber} به مبلغ {order.TotalAmount - (order.DiscountAmount ?? 0):N0} تومان ناموفق بود؛ لطفاً دوباره تلاش کنید.",
                fallbackUrl: $"/my-orders/{order.Id}",
                appUserId: order.UserId,
                phone: order.PhoneNumber);
            await _db.SaveChangesAsync(ct);

            return Content(PaymentResultHtml(false, result.Message ?? "پرداخت ناموفق بود.", order), "text/html; charset=utf-8");
        }

        if (order.Status is not (OrderStatus.Approved or OrderStatus.Completed))
        {
            var previousPaymentStatus = order.Status;
            await FinalizeOrderAsync(order);
            order.Status = OrderStatus.Approved;
            var stageAssignedByPayment = order.Stage == null;
            order.Stage ??= OrderStage.PaymentConfirmed;
            order.PaymentDate = DateTimeOffset.UtcNow;
            order.PaymentProvider = gateway.Provider;
            order.PaymentReference = result.GatewayUrl;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await _notificationEvents.NotifyOrderStatusChangedAsync(order, previousPaymentStatus);
            if (stageAssignedByPayment)
                await _notificationEvents.NotifyOrderStageChangedAsync(order, null);
            await _notificationEvents.NotifyAsync(
                eventType: "payment.succeeded",
                values: new Dictionary<string, string?>
                {
                    ["OrderNumber"] = order.OrderNumber,
                    ["Amount"] = (order.TotalAmount - (order.DiscountAmount ?? 0)).ToString("N0"),
                    ["OrderId"] = order.Id.ToString()
                },
                idempotencyKey: $"payment.succeeded:{order.Id}",
                fallbackSeverity: NotificationSeverity.Success,
                fallbackTitle: "پرداخت شما با موفقیت انجام شد",
                fallbackBody: $"مبلغ {order.TotalAmount - (order.DiscountAmount ?? 0):N0} تومان برای سفارش {order.OrderNumber} پرداخت و تأیید شد.",
                fallbackUrl: $"/my-orders/{order.Id}",
                appUserId: order.UserId,
                phone: order.PhoneNumber);
            await _db.SaveChangesAsync(ct);
        }

        return Content(PaymentResultHtml(true, result.Message ?? "پرداخت با موفقیت انجام شد.", order), "text/html; charset=utf-8");
    }

    private static string BuildPaymentCallbackUrl(int orderId, string? callbackBaseUrl)
    {
        var baseUrl = PaymentGatewayFactory.BuildCallbackUrl(callbackBaseUrl);
        if (baseUrl.Contains("{orderId}", StringComparison.Ordinal))
            return baseUrl.Replace("{orderId}", orderId.ToString());
        return baseUrl;
    }

    private static string PaymentResultHtml(bool success, string? message, Order? order)
    {
        var orderNumber = order?.OrderNumber is { Length: > 0 } n
            ? $"<p class=\"meta\">شماره سفارش: <b>{System.Net.WebUtility.HtmlEncode(n)}</b></p>"
            : "";
        var track = order != null
            ? $"<p class=\"note\"><a href=\"/order-tracking?orderNumber={Uri.EscapeDataString(order.OrderNumber)}&phone={Uri.EscapeDataString(order.PhoneNumber)}\">پیگیری سفارش</a></p>"
            : "";
        var iconClass = success ? "icon ok" : "icon fail";
        var icon = success ? "&#10003;" : "&#10007;";
        var title = success ? "پرداخت موفق" : "پرداخت ناموفق";
        var color = success ? "#16a34a" : "#dc2626";
        var messageText = System.Net.WebUtility.HtmlEncode(message ?? "");

        // کاربر باید داخل SPA بماند؛ این صفحه فقط برای درگاه/کلاینت‌های بدون جاوااسکریپت
        // است و بلافاصله به صفحه نتیجه پرداخت منتقل می‌کند.
        var redirect = "";
        if (order != null)
        {
            var target = $"/payment/result?orderId={order.Id}&status={(success ? "success" : "failed")}"
                         + $"&msg={Uri.EscapeDataString(message ?? "")}"
                         + $"&orderNumber={Uri.EscapeDataString(order.OrderNumber)}"
                         + $"&phone={Uri.EscapeDataString(order.PhoneNumber)}";
            redirect = $@"<meta http-equiv=""refresh"" content=""0;url={System.Net.WebUtility.HtmlEncode(target)}"">
<script>location.replace({System.Text.Json.JsonSerializer.Serialize(target)});</script>";
        }

        return $@"<!DOCTYPE html>
<html lang=""fa"" dir=""rtl"">
<head>
<meta charset=""utf-8"">
{redirect}
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>{title} - ارورسرویس</title>
<style>
  body {{ margin:0; font-family:Tahoma,Arial,sans-serif; background:#f1f5f9; display:grid; place-items:center; min-height:100vh; }}
  .card {{ background:#fff; border-radius:18px; box-shadow:0 10px 30px rgba(0,0,0,.08); padding:40px 32px; max-width:400px; width:92%; text-align:center; }}
  .icon {{ width:76px; height:76px; border-radius:50%; display:grid; place-items:center; margin:0 auto 18px; font-size:34px; color:#fff; }}
  .icon.ok {{ background:{color}; }}
  .icon.fail {{ background:{color}; }}
  h1 {{ font-size:1.25rem; margin:0 0 10px; color:#0f172a; }}
  p {{ font-size:.85rem; color:#475569; line-height:1.9; margin:6px 0; }}
  p.meta {{ color:#64748b; font-size:.8rem; }}
  a {{ color:{color}; font-weight:700; text-decoration:none; }}
  .note {{ margin-top:16px; padding-top:14px; border-top:1px dashed #e2e8f0; }}
</style>
</head>
<body>
  <div class=""card"">
    <div class=""icon {iconClass}"">{icon}</div>
    <h1>{title}</h1>
    <p>{messageText}</p>
    {orderNumber}
    {track}
  </div>
</body>
</html>";
    }
}
