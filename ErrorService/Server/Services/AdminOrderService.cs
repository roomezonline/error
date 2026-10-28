using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

public sealed record AdminOrderLine(int ProductId, int Quantity);

public sealed record AdminOrderInput(
    int? UserId,
    string FullName,
    string PhoneNumber,
    string? Email,
    string Address,
    int? ProvinceId,
    int? CityId,
    string? ProvinceName,
    string? CityName,
    string? PostalCode,
    IReadOnlyList<AdminOrderLine> Items,
    string? CouponCode,
    decimal? ManualDiscountAmount,
    int? ShippingMethodId,
    string? CustomerNotes,
    string? AdminNotes,
    int? CreatedByAdminId = null);

/// <summary>
/// چرخهٔ سفارش‌های ساخته‌شده توسط ادمین: پیش‌فاکتور (Draft) بدون رزرو موجودی،
/// انتشار/ثبت پرداخت با رزرو اتمیک موجودی و مصرف کوپن.
/// </summary>
public sealed class AdminOrderService
{
    public const int DefaultExpiryHours = 24;
    public const int MaxExpiryHours = 24 * 7;

    private static readonly HashSet<string> AllowedPaymentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "cash", "card_to_card", "pos", "transfer", "other"
    };

    private readonly ErrorServiceDbContext _db;
    private readonly IOutputCacheStore _outputCache;
    private readonly NotificationEventService _notifications;

    public AdminOrderService(ErrorServiceDbContext db, IOutputCacheStore outputCache, NotificationEventService notifications)
    {
        _db = db;
        _outputCache = outputCache;
        _notifications = notifications;
    }

    public static int NormalizeExpiryHours(int? hours)
        => hours is > 0 ? Math.Min(hours.Value, MaxExpiryHours) : DefaultExpiryHours;

    public async Task<(Order? Order, string? Error)> CreateAsync(
        AdminOrderInput input,
        bool publish,
        int? expiresInHours,
        string adminName,
        OrderOrigin origin,
        CancellationToken ct)
    {
        if (input.UserId.HasValue)
        {
            var userExists = await _db.Users.AsNoTracking()
                .AnyAsync(u => u.Id == input.UserId.Value && !u.IsDeleted, ct);
            if (!userExists)
                return (null, "مشتری انتخاب‌شده معتبر نیست یا حذف شده است.");
        }

        var (orderItems, itemsTotal, priceError) = await PriceItemsAsync(input.Items, requireStock: publish, ct);
        if (priceError != null) return (null, priceError);

        var now = DateTimeOffset.UtcNow;
        var (provinceName, cityName) = await ResolveLocationAsync(input.ProvinceId, input.CityId, input.ProvinceName, input.CityName, ct);
        var shipping = await ResolveShippingAsync(input.ShippingMethodId, itemsTotal, ct);
        if (shipping.Error != null) return (null, shipping.Error);

        var discount = await CalculateDiscountAsync(input.CouponCode, itemsTotal, input.UserId, input.ManualDiscountAmount, ct);
        if (discount.Error != null) return (null, discount.Error);

        var order = new Order
        {
            UserId = input.UserId,
            OrderNumber = await GenerateOrderNumberAsync(ct),
            CreatedAt = now,
            UpdatedAt = now,
            Status = publish ? OrderStatus.PendingPayment : OrderStatus.Draft,
            Origin = origin,
            CreatedByAdminId = input.CreatedByAdminId,
            FullName = input.FullName.Trim(),
            PhoneNumber = input.PhoneNumber.Trim(),
            Email = input.Email?.Trim(),
            Address = input.Address.Trim(),
            ProvinceId = input.ProvinceId,
            CityId = input.CityId,
            ProvinceName = provinceName,
            CityName = cityName,
            PostalCode = input.PostalCode,
            CustomerNotes = input.CustomerNotes,
            AdminNotes = input.AdminNotes,
            CouponCode = discount.Coupon?.Code,
            // کوپن تا زمان انتشار/ثبت پرداخت مصرف نمی‌شود؛ CouponId فقط بعد از مصرف ست می‌شود.
            CouponId = null,
            ManualDiscountAmount = discount.Manual,
            DiscountAmount = discount.Total,
            ShippingMethodId = shipping.Id,
            ShippingMethodName = shipping.Name,
            ShippingCost = shipping.Cost,
            ShippingIsFree = shipping.IsFree,
            TotalAmount = itemsTotal + shipping.Cost,
            AccessToken = Guid.NewGuid().ToString("N"),
            StockDeducted = false,
            ExpiresAt = publish ? now.AddHours(NormalizeExpiryHours(expiresInHours)) : null
        };

        foreach (var item in orderItems)
            order.Items.Add(item);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (publish)
        {
            var reserveError = await ReserveStockAsync(orderItems, ct);
            if (reserveError != null)
            {
                await tx.RollbackAsync(ct);
                return (null, reserveError);
            }

            order.StockDeducted = true;

            if (discount.Coupon != null)
            {
                var counted = await IncrementCouponUsageAsync(discount.Coupon.Id, now, ct);
                if (!counted)
                {
                    await tx.RollbackAsync(ct);
                    return (null, "تعداد استفاده از این کد تخفیف به پایان رسیده است.");
                }
                order.CouponId = discount.Coupon.Id;
            }
        }

        _db.Orders.Add(order);

        order.Events.Add(new OrderEvent
        {
            EventType = publish ? "created_by_admin" : "draft_created",
            FromStatus = null,
            ToStatus = order.Status,
            Message = publish
                ? "سفارش آمادهٔ پرداخت توسط مدیر ایجاد شد"
                : "پیش‌فاکتور توسط مدیر ایجاد شد",
            CreatedBy = adminName,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (publish)
        {
            await EvictProductsCacheAsync();
            await _notifications.NotifyOrderCreatedByAdminAsync(order);
            await _db.SaveChangesAsync(ct);
        }

        return (order, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateDraftAsync(
        Order order,
        AdminOrderInput input,
        int? expiresInHours,
        string adminName,
        CancellationToken ct)
    {
        if (order.Status != OrderStatus.Draft)
            return (false, "فقط پیش‌فاکتور (پیش از انتشار) قابل ویرایش است.");

        if (input.UserId.HasValue && input.UserId != order.UserId)
        {
            var userExists = await _db.Users.AsNoTracking()
                .AnyAsync(u => u.Id == input.UserId.Value && !u.IsDeleted, ct);
            if (!userExists)
                return (false, "مشتری انتخاب‌شده معتبر نیست یا حذف شده است.");
        }

        var (orderItems, itemsTotal, priceError) = await PriceItemsAsync(input.Items, requireStock: false, ct);
        if (priceError != null) return (false, priceError);

        var (provinceName, cityName) = await ResolveLocationAsync(input.ProvinceId, input.CityId, input.ProvinceName, input.CityName, ct);
        var shipping = await ResolveShippingAsync(input.ShippingMethodId, itemsTotal, ct);
        if (shipping.Error != null) return (false, shipping.Error);

        var targetUserId = input.UserId ?? order.UserId;
        var discount = await CalculateDiscountAsync(input.CouponCode, itemsTotal, targetUserId, input.ManualDiscountAmount, ct);
        if (discount.Error != null) return (false, discount.Error);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var existingItems = await _db.OrderItems.Where(i => i.OrderId == order.Id).ToListAsync(ct);
        _db.OrderItems.RemoveRange(existingItems);
        foreach (var item in orderItems)
        {
            item.OrderId = order.Id;
            _db.OrderItems.Add(item);
        }

        if (input.UserId.HasValue)
            order.UserId = input.UserId;

        order.FullName = input.FullName.Trim();
        order.PhoneNumber = input.PhoneNumber.Trim();
        order.Email = input.Email?.Trim();
        order.Address = input.Address.Trim();
        order.ProvinceId = input.ProvinceId;
        order.CityId = input.CityId;
        order.ProvinceName = provinceName;
        order.CityName = cityName;
        order.PostalCode = input.PostalCode;
        order.CustomerNotes = input.CustomerNotes;
        order.AdminNotes = input.AdminNotes;
        order.CouponId = null;
        order.CouponCode = discount.Coupon?.Code;
        order.ManualDiscountAmount = discount.Manual;
        order.DiscountAmount = discount.Total;
        order.ShippingMethodId = shipping.Id;
        order.ShippingMethodName = shipping.Name;
        order.ShippingCost = shipping.Cost;
        order.ShippingIsFree = shipping.IsFree;
        order.TotalAmount = itemsTotal + shipping.Cost;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        _db.OrderEvents.Add(new OrderEvent
        {
            OrderId = order.Id,
            EventType = "draft_updated",
            FromStatus = OrderStatus.Draft,
            ToStatus = OrderStatus.Draft,
            Message = "پیش‌فاکتور توسط مدیر ویرایش شد",
            CreatedBy = adminName,
            CreatedAt = order.UpdatedAt
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> PublishAsync(Order order, string adminName, CancellationToken ct)
    {
        if (order.Status != OrderStatus.Draft)
            return (false, "این سفارش پیش‌فاکتور نیست یا قبلاً منتشر شده است.");

        var items = await _db.OrderItems.AsNoTracking().Where(i => i.OrderId == order.Id).ToListAsync(ct);
        if (items.Count == 0)
            return (false, "پیش‌فاکتور هیچ آیتمی ندارد.");

        var itemsTotal = items.Sum(i => i.Price * i.Quantity);
        var coupon = await ResolveCouponAsync(order.CouponCode, itemsTotal, order.UserId, ct);
        if (coupon.Error != null) return (false, coupon.Error);

        var manual = order.ManualDiscountAmount ?? 0;
        var newDiscount = Math.Max(0, Math.Min(itemsTotal, coupon.Discount + manual));

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddHours(NormalizeExpiryHours(null));

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var claimed = await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Orders
SET Status = {(int)OrderStatus.PendingPayment}, ExpiresAt = {expiresAt}, UpdatedAt = {now}
WHERE Id = {order.Id} AND Status = {(int)OrderStatus.Draft}", ct);

        if (claimed == 0)
        {
            await tx.RollbackAsync(ct);
            return (false, "وضعیت سفارش هم‌زمان تغییر کرده است؛ صفحه را تازه کنید.");
        }

        var reserveError = await ReserveStockAsync(items, ct);
        if (reserveError != null)
        {
            await tx.RollbackAsync(ct);
            return (false, reserveError);
        }

        if (coupon.Coupon == null && !string.IsNullOrWhiteSpace(order.CouponCode))
        {
            await tx.RollbackAsync(ct);
            return (false, coupon.Error ?? "کد تخفیف معتبر نیست.");
        }

        if (coupon.Coupon != null)
        {
            var counted = await IncrementCouponUsageAsync(coupon.Coupon.Id, now, ct);
            if (!counted)
            {
                await tx.RollbackAsync(ct);
                return (false, "تعداد استفاده از این کد تخفیف به پایان رسیده است.");
            }
        }

        order.Status = OrderStatus.PendingPayment;
        order.StockDeducted = true;
        order.CouponId = coupon.Coupon?.Id;
        order.CouponCode = coupon.Coupon?.Code;
        order.DiscountAmount = newDiscount;
        order.ExpiresAt = expiresAt;
        order.UpdatedAt = now;

        _db.OrderEvents.Add(new OrderEvent
        {
            OrderId = order.Id,
            EventType = "draft_published",
            FromStatus = OrderStatus.Draft,
            ToStatus = OrderStatus.PendingPayment,
            Message = "پیش‌فاکتور منتشر شد و آمادهٔ پرداخت است",
            CreatedBy = adminName,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await EvictProductsCacheAsync();
        await _notifications.NotifyOrderCreatedByAdminAsync(order);
        await _db.SaveChangesAsync(ct);

        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> MarkPaidAsync(
        Order order,
        AdminOrderMarkPaidRequest request,
        string adminName,
        CancellationToken ct)
    {
        if (order.Status is not (OrderStatus.Draft or OrderStatus.PendingPayment or OrderStatus.ReceiptUploaded))
            return (false, "برای سفارش در وضعیت فعلی امکان ثبت پرداخت وجود ندارد.");

        var method = (request.PaymentMethod ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedPaymentMethods.Contains(method))
            return (false, "روش پرداخت انتخابی معتبر نیست.");

        var wasDraft = order.Status == OrderStatus.Draft;
        var previousStatus = order.Status;
        var previousStage = order.Stage;
        var now = DateTimeOffset.UtcNow;
        var paidAt = request.PaidAt ?? now;

        var items = await _db.OrderItems.AsNoTracking().Where(i => i.OrderId == order.Id).ToListAsync(ct);
        if (items.Count == 0)
            return (false, "سفارش هیچ آیتمی ندارد.");

        var itemsTotal = items.Sum(i => i.Price * i.Quantity);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (wasDraft)
        {
            // پیش‌فاکتور هنگام ثبت پرداخت، موجودی را رزرو و کوپن را مصرف می‌کند.
            var coupon = await ResolveCouponAsync(order.CouponCode, itemsTotal, order.UserId, ct);
            if (coupon.Error != null)
            {
                await tx.RollbackAsync(ct);
                return (false, coupon.Error);
            }

            var reserveError = await ReserveStockAsync(items, ct);
            if (reserveError != null)
            {
                await tx.RollbackAsync(ct);
                return (false, reserveError);
            }

            if (coupon.Coupon != null)
            {
                var counted = await IncrementCouponUsageAsync(coupon.Coupon.Id, now, ct);
                if (!counted)
                {
                    await tx.RollbackAsync(ct);
                    return (false, "تعداد استفاده از این کد تخفیف به پایان رسیده است.");
                }
            }

            var manual = order.ManualDiscountAmount ?? 0;
            order.CouponId = coupon.Coupon?.Id;
            order.CouponCode = coupon.Coupon?.Code;
            order.DiscountAmount = Math.Max(0, Math.Min(itemsTotal, coupon.Discount + manual));
            order.StockDeducted = true;
        }
        else if (!order.StockDeducted)
        {
            var reserveError = await ReserveStockAsync(items, ct);
            if (reserveError != null)
            {
                await tx.RollbackAsync(ct);
                return (false, reserveError);
            }
            order.StockDeducted = true;
        }

        var paymentReference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
        var oldStatusInt = (int)previousStatus;

        var claimed = await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Orders
SET Status = {(int)OrderStatus.Approved},
    Stage = CASE WHEN Stage IS NULL THEN {(int)OrderStage.PaymentConfirmed} ELSE Stage END,
    PaymentDate = {paidAt},
    PaymentProvider = {(int)PaymentProvider.Manual},
    PaymentReference = {paymentReference},
    PaymentMethod = {method},
    UpdatedAt = {now}
WHERE Id = {order.Id}
  AND Status = {oldStatusInt}", ct);

        if (claimed == 0)
        {
            await tx.RollbackAsync(ct);
            return (false, "وضعیت سفارش هم‌زمان تغییر کرده است؛ صفحه را تازه کنید.");
        }

        var note = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (!string.IsNullOrWhiteSpace(request.ReceiptUrl))
        {
            if (string.IsNullOrEmpty(order.ReceiptImageUrlsJson))
            {
                var urls = new List<string>();
                if (!string.IsNullOrWhiteSpace(order.ReceiptImageUrl)) urls.Add(order.ReceiptImageUrl);
                if (!urls.Contains(request.ReceiptUrl.Trim(), StringComparer.OrdinalIgnoreCase)
                    && !string.Equals(order.ReceiptImageUrl, request.ReceiptUrl.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    urls.Add(request.ReceiptUrl.Trim());
                }
                order.ReceiptImageUrlsJson = System.Text.Json.JsonSerializer.Serialize(urls);
            }
            else
            {
                try
                {
                    var urls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(order.ReceiptImageUrlsJson) ?? new();
                    if (!urls.Contains(request.ReceiptUrl.Trim(), StringComparer.OrdinalIgnoreCase))
                    {
                        urls.Add(request.ReceiptUrl.Trim());
                        order.ReceiptImageUrlsJson = System.Text.Json.JsonSerializer.Serialize(urls);
                    }
                }
                catch (System.Text.Json.JsonException) { }
            }
        }

        var noteLine = $"[{now:yyyy/MM/dd HH:mm}] {adminName}: پرداخت دستی ثبت شد ({MethodLabel(method)})"
            + (string.IsNullOrWhiteSpace(paymentReference) ? "" : $" — مرجع: {paymentReference}")
            + (note == null ? "" : $" — {note}");
        order.AdminNotes = string.IsNullOrWhiteSpace(order.AdminNotes) ? noteLine : order.AdminNotes + "\n" + noteLine;

        order.Status = OrderStatus.Approved;
        order.Stage ??= OrderStage.PaymentConfirmed;
        order.PaymentDate = paidAt;
        order.PaymentProvider = PaymentProvider.Manual;
        order.PaymentReference = paymentReference;
        order.PaymentMethod = method;
        order.UpdatedAt = now;

        _db.OrderEvents.Add(new OrderEvent
        {
            OrderId = order.Id,
            EventType = "payment.recorded_by_admin",
            FromStatus = previousStatus,
            ToStatus = OrderStatus.Approved,
            Message = $"پرداخت دستی توسط مدیر ثبت شد ({MethodLabel(method)})",
            CreatedBy = adminName,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _notifications.NotifyOrderStatusChangedAsync(order, previousStatus);
        if (previousStage == null && order.Stage == OrderStage.PaymentConfirmed)
            await _notifications.NotifyOrderStageChangedAsync(order, null);
        await _notifications.NotifyAsync(
            eventType: "payment.recorded_by_admin",
            values: new Dictionary<string, string?>
            {
                ["OrderNumber"] = order.OrderNumber,
                ["Amount"] = (order.TotalAmount - (order.DiscountAmount ?? 0)).ToString("N0"),
                ["Method"] = MethodLabel(method),
                ["OrderId"] = order.Id.ToString()
            },
            idempotencyKey: $"payment.admin:{order.Id}",
            fallbackSeverity: NotificationSeverity.Success,
            fallbackTitle: "پرداخت سفارش ثبت شد",
            fallbackBody: $"پرداخت سفارش {order.OrderNumber} به مبلغ {order.TotalAmount - (order.DiscountAmount ?? 0):N0} تومان ثبت و تأیید شد.",
            fallbackUrl: $"/my-orders/{order.Id}",
            appUserId: order.UserId,
            phone: order.PhoneNumber);
        await _db.SaveChangesAsync(ct);

        return (true, null);
    }

    private static string MethodLabel(string method) => method switch
    {
        "cash" => "نقدی",
        "card_to_card" => "کارت به کارت",
        "pos" => "دستگاه کارتخوان",
        "transfer" => "انتقال بانکی",
        _ => "سایر"
    };

    private async Task<(List<OrderItem> Items, decimal Total, string? Error)> PriceItemsAsync(
        IReadOnlyList<AdminOrderLine> lines,
        bool requireStock,
        CancellationToken ct)
    {
        if (lines == null || lines.Count == 0)
            return (new List<OrderItem>(), 0, "سبد خرید خالی است.");

        var combined = new Dictionary<int, int>();
        foreach (var line in lines)
        {
            if (line.ProductId <= 0 || line.Quantity <= 0)
                return (new List<OrderItem>(), 0, "تعداد هر محصول باید بزرگ‌تر از صفر باشد.");
            combined[line.ProductId] = combined.GetValueOrDefault(line.ProductId) + line.Quantity;
        }

        var productIds = combined.Keys.ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var items = new List<OrderItem>();
        decimal total = 0;
        foreach (var (productId, quantity) in combined)
        {
            if (!products.TryGetValue(productId, out var product))
                return (items, total, $"محصول با شناسه {productId} یافت نشد.");
            if (product.IsDraft)
                return (items, total, $"محصول «{product.Name}» در فروشگاه قابل فروش نیست.");

            if (requireStock && (!product.IsAvailable || product.StockQuantity < quantity))
                return (items, total, $"موجودی «{product.Name}» کافی نیست. موجودی فعلی: {product.StockQuantity}");

            var price = DiscountHelper.IsActive(product) ? product.DiscountPrice!.Value : product.Price;
            total += price * quantity;
            items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Price = price,
                Quantity = quantity
            });
        }

        return (items, total, null);
    }

    private async Task<(decimal Discount, Coupon? Coupon, decimal Manual, decimal Total, string? Error)> CalculateDiscountAsync(
        string? couponCode,
        decimal itemsTotal,
        int? targetUserId,
        decimal? manualDiscount,
        CancellationToken ct)
    {
        var manual = Math.Clamp(manualDiscount ?? 0, 0, itemsTotal);
        var coupon = await ResolveCouponAsync(couponCode, itemsTotal, targetUserId, ct);
        if (coupon.Error != null) return (0, null, manual, 0, coupon.Error);

        var total = Math.Max(0, Math.Min(itemsTotal, coupon.Discount + manual));
        return (coupon.Discount, coupon.Coupon, manual, total, null);
    }

    private async Task<(Coupon? Coupon, decimal Discount, string? Error)> ResolveCouponAsync(
        string? couponCode,
        decimal itemsTotal,
        int? targetUserId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
            return (null, 0, null);

        var code = couponCode.Trim().ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code.ToUpper() == code, ct);
        if (coupon == null) return (null, 0, "کد تخفیف معتبر نیست یا حذف شده است.");
        if (!coupon.IsActive) return (null, 0, "کد تخفیف غیرفعال است.");
        if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate.Value < now) return (null, 0, "کد تخفیف منقضی شده است.");
        if (coupon.MaxUsageCount.HasValue && coupon.CurrentUsageCount >= coupon.MaxUsageCount.Value)
            return (null, 0, "تعداد استفاده از این کد تخفیف به پایان رسیده است.");
        if (coupon.AssignedToUserId.HasValue)
        {
            if (!targetUserId.HasValue || targetUserId.Value != coupon.AssignedToUserId.Value)
                return (null, 0, "این کد تخفیف متعلق به مشتری انتخاب‌شده نیست.");
        }
        if (coupon.MinOrderAmount.HasValue && itemsTotal < coupon.MinOrderAmount.Value)
            return (null, 0, $"حداقل مبلغ سفارش برای این کد تخفیف {coupon.MinOrderAmount.Value:N0} تومان است.");

        decimal discount = 0;
        if (coupon.DiscountPercent.HasValue)
            discount = itemsTotal * coupon.DiscountPercent.Value / 100m;
        else if (coupon.DiscountAmount.HasValue)
            discount = coupon.DiscountAmount.Value;

        discount = Math.Max(0, Math.Min(discount, itemsTotal));
        return (coupon, discount, null);
    }

    private async Task<(int? Id, string? Name, decimal Cost, bool IsFree, string? Error)> ResolveShippingAsync(
        int? shippingMethodId,
        decimal itemsTotal,
        CancellationToken ct)
    {
        if (!shippingMethodId.HasValue)
            return (null, null, 0, false, null);

        var method = await _db.ShippingMethods.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shippingMethodId.Value, ct);
        if (method == null || !method.IsActive)
            return (null, null, 0, false, "روش ارسال انتخاب شده معتبر نیست.");

        var cost = method.Price;
        var isFree = method.IsFree;

        var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (settings != null
            && settings.EnableFreeShipping
            && settings.FreeShippingThreshold is > 0
            && itemsTotal >= settings.FreeShippingThreshold.Value)
        {
            cost = 0;
            isFree = true;
        }

        return (method.Id, method.Name, cost, isFree, null);
    }

    private async Task<(string? ProvinceName, string? CityName)> ResolveLocationAsync(
        int? provinceId,
        int? cityId,
        string? provinceName,
        string? cityName,
        CancellationToken ct)
    {
        if (provinceId.HasValue && string.IsNullOrWhiteSpace(provinceName))
            provinceName = await _db.Provinces.Where(x => x.Id == provinceId.Value).Select(x => x.Name).FirstOrDefaultAsync(ct);
        if (cityId.HasValue && string.IsNullOrWhiteSpace(cityName))
            cityName = await _db.Cities.Where(x => x.Id == cityId.Value).Select(x => x.Name).FirstOrDefaultAsync(ct);
        return (provinceName, cityName);
    }

    private async Task<string?> ReserveStockAsync(IEnumerable<OrderItem> items, CancellationToken ct)
    {
        foreach (var item in items)
        {
            var affected = await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Products
SET StockQuantity = StockQuantity - {item.Quantity},
    IsAvailable = CASE WHEN StockQuantity <= {item.Quantity} THEN 0 ELSE IsAvailable END
WHERE Id = {item.ProductId} AND IsAvailable = 1 AND StockQuantity >= {item.Quantity}", ct);

            if (affected == 0)
                return $"موجودی «{item.ProductName}» کافی نیست. لطفاً تعداد یا موجودی را بررسی کنید.";
        }

        return null;
    }

    private async Task<bool> IncrementCouponUsageAsync(int couponId, DateTimeOffset now, CancellationToken ct)
    {
        var counted = await _db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Coupons
SET CurrentUsageCount = CurrentUsageCount + 1
WHERE Id = {couponId}
  AND IsActive = 1
  AND (ExpiryDate IS NULL OR ExpiryDate >= {now})
  AND (MaxUsageCount IS NULL OR CurrentUsageCount < MaxUsageCount)", ct);
        return counted > 0;
    }

    private async Task EvictProductsCacheAsync()
    {
        try { await _outputCache.EvictByTagAsync(Controllers.ProductsController.CacheTag, CancellationToken.None); }
        catch { }
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = Random.Shared.Next(100000, 1000000).ToString();
            if (!await _db.Orders.AnyAsync(o => o.OrderNumber == candidate, ct))
                return candidate;
        }

        for (var n = 999999; n >= 100000; n--)
        {
            var candidate = n.ToString();
            if (!await _db.Orders.AnyAsync(o => o.OrderNumber == candidate, ct))
                return candidate;
        }

        throw new InvalidOperationException("هیچ شماره سفارش آزادی یافت نشد.");
    }
}
