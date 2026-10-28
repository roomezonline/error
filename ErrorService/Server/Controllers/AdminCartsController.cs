using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ErrorService.Server.Controllers;

/// <summary>مدیریت سبد خرید مشتری و تبدیل آن به پیش‌فاکتور/سفارش آمادهٔ پرداخت.</summary>
[ApiController]
[Route("api/admin/carts")]
[Authorize(Policy = "perm:admin.orders.manage")]
public sealed class AdminCartsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly AdminOrderService _adminOrders;
    private readonly NotificationEventService _notifications;

    public AdminCartsController(ErrorServiceDbContext db, AdminOrderService adminOrders, NotificationEventService notifications)
    {
        _db = db;
        _adminOrders = adminOrders;
        _notifications = notifications;
    }

    private string AdminName()
    {
        var name = User.FindFirst(ClaimTypes.Name)?.Value;
        if (!string.IsNullOrWhiteSpace(name)) return name;
        var phone = User.FindFirst(ClaimTypes.MobilePhone)?.Value;
        if (!string.IsNullOrWhiteSpace(phone)) return phone;
        return User.Identity?.Name ?? "مدیر";
    }

    private int? CurrentAdminId()
        => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    [HttpGet("{userId:int}")]
    public async Task<ActionResult<AdminCartDto>> GetCart(int userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound("مشتری یافت نشد.");
        return Ok(await BuildCartAsync(user));
    }

    [HttpPut("{userId:int}")]
    public async Task<ActionResult<AdminCartSaveResult>> SaveCart(int userId, [FromBody] AdminCartSaveRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound("مشتری یافت نشد.");

        if (request.ExpectedRevision != user.CartRevision)
        {
            return Conflict(new AdminCartSaveResult
            {
                Success = false,
                Message = "سبد خرید هم‌زمان توسط مشتری تغییر کرده است؛ نسخهٔ جدید بارگذاری شد.",
                Cart = await BuildCartAsync(user)
            });
        }

        var desired = new Dictionary<int, int>();
        foreach (var item in request.Items ?? new List<AdminCartItemDto>())
        {
            if (item.ProductId <= 0) continue;
            desired[item.ProductId] = Math.Max(desired.GetValueOrDefault(item.ProductId), item.Quantity);
        }

        if (desired.Any(kv => kv.Value <= 0))
            return BadRequest("تعداد هر آیتم باید بزرگ‌تر از صفر باشد.");

        var productIds = desired.Keys.ToList();
        var products = productIds.Count == 0
            ? new Dictionary<int, Product>()
            : await _db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        foreach (var (productId, quantity) in desired)
        {
            if (!products.TryGetValue(productId, out var product) || product.IsDraft)
                return BadRequest("یکی از محصولات انتخاب‌شده در فروشگاه موجود نیست.");
            if (!product.IsAvailable || product.StockQuantity <= 0)
                return BadRequest($"محصول «{product.Name}» ناموجود است؛ آن را حذف یا جایگزین کنید.");
            if (quantity > product.StockQuantity)
                return BadRequest($"تعداد «{product.Name}» از موجودی ({product.StockQuantity}) بیشتر است.");
        }

        var existing = await _db.CartItems.Where(c => c.UserId == userId).ToListAsync();
        _db.CartItems.RemoveRange(existing);

        var now = DateTimeOffset.UtcNow;
        foreach (var (productId, quantity) in desired)
        {
            var product = products[productId];
            _db.CartItems.Add(new CartItem
            {
                UserId = userId,
                ProductId = productId,
                ProductName = product.Name,
                ImageUrl = product.MainImageUrl,
                Price = DiscountHelper.IsActive(product) ? product.DiscountPrice!.Value : product.Price,
                Quantity = quantity,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        user.CartRevision = DateTimeOffset.UtcNow.UtcTicks;
        user.UpdatedAt = now;
        await _db.SaveChangesAsync();

        return Ok(new AdminCartSaveResult
        {
            Success = true,
            Message = "سبد خرید مشتری ذخیره شد.",
            Cart = await BuildCartAsync(user)
        });
    }

    /// <summary>ترمیم خودکار: حذف ناموجودها، اصلاح تعداد بیش از موجودی و به‌روزرسانی قیمت.</summary>
    [HttpPost("{userId:int}/repair")]
    public async Task<ActionResult<AdminCartDto>> RepairCart(int userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound("مشتری یافت نشد.");

        var items = await _db.CartItems.Where(c => c.UserId == userId).ToListAsync();
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var products = productIds.Count == 0
            ? new Dictionary<int, Product>()
            : await _db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var now = DateTimeOffset.UtcNow;
        var seen = new HashSet<int>();
        var removed = 0;
        var adjusted = 0;

        foreach (var item in items.OrderBy(i => i.Id))
        {
            if (!seen.Add(item.ProductId))
            {
                _db.CartItems.Remove(item);
                removed++;
                continue;
            }

            if (!products.TryGetValue(item.ProductId, out var product)
                || product.IsDraft
                || !product.IsAvailable
                || product.StockQuantity <= 0)
            {
                _db.CartItems.Remove(item);
                removed++;
                continue;
            }

            var quantity = Math.Min(item.Quantity, product.StockQuantity);
            if (quantity != item.Quantity)
            {
                item.Quantity = quantity;
                adjusted++;
            }

            item.ProductName = product.Name;
            item.ImageUrl = product.MainImageUrl;
            item.Price = DiscountHelper.IsActive(product) ? product.DiscountPrice!.Value : product.Price;
            item.UpdatedAt = now;
        }

        user.CartRevision = DateTimeOffset.UtcNow.UtcTicks;
        user.UpdatedAt = now;
        await _db.SaveChangesAsync();

        Response.Headers["X-Cart-Repair-Summary"] = $"{removed}:{adjusted}";
        return Ok(await BuildCartAsync(user));
    }

    /// <summary>تبدیل سبد اصلاح‌شده به پیش‌فاکتور یا سفارش آمادهٔ پرداخت.</summary>
    [HttpPost("{userId:int}/checkout")]
    public async Task<ActionResult<OrderDto>> CheckoutCart(int userId, [FromBody] AdminCartCheckoutRequest request)
    {
        request ??= new();
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound("مشتری یافت نشد.");
        if (string.IsNullOrWhiteSpace(user.Address))
            return BadRequest("آدرس مشتری ثبت نشده است؛ ابتدا پروفایل مشتری را تکمیل کنید.");

        var cart = await _db.CartItems.AsNoTracking().Where(c => c.UserId == userId).ToListAsync();
        if (cart.Count == 0) return BadRequest("سبد خرید مشتری خالی است.");

        var input = new AdminOrderInput(
            user.Id,
            user.FullName,
            user.PhoneNumber,
            user.Email,
            user.Address,
            null,
            null,
            null,
            null,
            user.PostalCode,
            cart.Select(c => new AdminOrderLine(c.ProductId, c.Quantity)).ToList(),
            request.CouponCode,
            request.ManualDiscountAmount,
            request.ShippingMethodId,
            request.CustomerNotes,
            request.AdminNotes,
            CurrentAdminId());

        var origin = request.Publish ? OrderOrigin.AdminCart : OrderOrigin.AdminProforma;
        var (order, error) = await _adminOrders.CreateAsync(
            input,
            request.Publish,
            request.ExpiresInHours,
            AdminName(),
            origin,
            HttpContext.RequestAborted);

        if (order == null)
            return BadRequest(error ?? "ایجاد سفارش ناموفق بود.");

        // سبد به سفارش تبدیل شد؛ ردیف‌های سبد پاک و نسخهٔ آن عوض می‌شود.
        var rows = await _db.CartItems.Where(c => c.UserId == userId).ToListAsync();
        if (rows.Count > 0)
        {
            _db.CartItems.RemoveRange(rows);
            var trackedUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (trackedUser != null)
            {
                trackedUser.CartRevision = DateTimeOffset.UtcNow.UtcTicks;
                trackedUser.UpdatedAt = DateTimeOffset.UtcNow;
            }
            await _db.SaveChangesAsync();
        }

        return Ok(ToDto(order));
    }

    [HttpPost("{userId:int}/notify")]
    public async Task<IActionResult> NotifyCartUpdated(int userId, [FromBody] AdminCartNotifyRequest request)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound("مشتری یافت نشد.");

        var message = string.IsNullOrWhiteSpace(request?.Message)
            ? "سبد خرید شما توسط پشتیبانی به‌روزرسانی شد. لطفاً سبد خرید را بررسی کنید."
            : request!.Message!.Trim();

        await _notifications.NotifyAsync(
            eventType: "cart.admin_updated",
            values: new Dictionary<string, string?>
            {
                ["Message"] = message,
                ["UserId"] = userId.ToString()
            },
            idempotencyKey: $"cart-admin:{userId}:{DateTimeOffset.UtcNow.UtcTicks}",
            fallbackSeverity: NotificationSeverity.Info,
            fallbackTitle: "به‌روزرسانی سبد خرید",
            fallbackBody: message,
            fallbackUrl: "/cart",
            appUserId: userId,
            phone: user.PhoneNumber);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<AdminCartDto> BuildCartAsync(AppUser user)
    {
        var rows = await _db.CartItems.AsNoTracking()
            .Where(c => c.UserId == user.Id)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();

        var productIds = rows.Select(r => r.ProductId).Distinct().ToList();
        var products = productIds.Count == 0
            ? new Dictionary<int, Product>()
            : await _db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var items = new List<AdminCartItemDto>();
        foreach (var row in rows)
        {
            products.TryGetValue(row.ProductId, out var product);
            var effectivePrice = product == null
                ? row.Price
                : DiscountHelper.IsActive(product) ? product.DiscountPrice!.Value : product.Price;

            var available = product != null && !product.IsDraft && product.IsAvailable && product.StockQuantity > 0;
            string? issue = null;
            if (product == null) issue = "محصول حذف شده است";
            else if (product.IsDraft) issue = "محصول پیش‌نویس است";
            else if (!available) issue = "ناموجود";
            else if (row.Quantity > product.StockQuantity) issue = "بیش از موجودی";
            else if (effectivePrice != row.Price) issue = "قیمت به‌روزرسانی شده است";

            items.Add(new AdminCartItemDto
            {
                ProductId = row.ProductId,
                ProductName = product?.Name ?? row.ProductName,
                ImageUrl = product?.MainImageUrl ?? row.ImageUrl,
                Price = effectivePrice,
                OriginalPrice = product?.Price,
                Quantity = row.Quantity,
                StockQuantity = product?.StockQuantity ?? 0,
                IsAvailable = available,
                IsDraft = product?.IsDraft ?? false,
                Issue = issue
            });
        }

        return new AdminCartDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Revision = user.CartRevision,
            UpdatedAt = rows.Count == 0 ? null : rows.Max(r => r.UpdatedAt),
            Items = items,
            TotalAmount = items.Sum(i => i.Price * i.Quantity)
        };
    }

    private static OrderDto ToDto(Order order) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        Status = order.Status,
        CreatedAt = order.CreatedAt,
        UpdatedAt = order.UpdatedAt,
        TotalAmount = order.TotalAmount,
        DiscountAmount = order.DiscountAmount,
        ManualDiscountAmount = order.ManualDiscountAmount,
        CouponCode = order.CouponCode,
        FullName = order.FullName,
        PhoneNumber = order.PhoneNumber,
        ShippingCost = order.ShippingCost,
        ShippingIsFree = order.ShippingIsFree,
        ShippingMethodId = order.ShippingMethodId,
        ShippingMethodName = order.ShippingMethodName,
        AccessToken = order.AccessToken,
        Origin = order.Origin,
        ExpiresAt = order.ExpiresAt
    };
}
