using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Server.Services.Payment;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;
    private readonly NotificationEventService _notificationEvents;

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

    public OrdersController(ErrorServiceDbContext db, IWebHostEnvironment env, IAuthorizationService authorizationService, IPaymentGatewayFactory paymentGatewayFactory, NotificationEventService notificationEvents)
    {
        _db = db;
        _env = env;
        _authorizationService = authorizationService;
        _paymentGatewayFactory = paymentGatewayFactory;
        _notificationEvents = notificationEvents;
    }

    private async Task<List<OrderItem>> LoadOrderItemsAsync(int orderId)
        => await _db.OrderItems.Where(i => i.OrderId == orderId).ToListAsync();

    private async Task RestoreStockAsync(List<OrderItem> items)
    {
        foreach (var item in items)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Products SET StockQuantity = StockQuantity + {item.Quantity}, IsAvailable = 1 WHERE Id = {item.ProductId}");
        }
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
        foreach (var item in items)
        {
            var product = await _db.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                product.StockQuantity = Math.Max(0, product.StockQuantity - item.Quantity);
                if (product.StockQuantity == 0)
                    product.IsAvailable = false;
            }
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

    private static string GenerateOrderNumber()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> code = stackalloc char[6];
        for (var i = 0; i < code.Length; i++)
            code[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        return $"ORD-{new string(code)}";
    }

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

        return !string.IsNullOrEmpty(order.AccessToken)
            && !string.IsNullOrEmpty(token)
            && order.AccessToken.Length == token.Length
            && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(order.AccessToken),
                System.Text.Encoding.UTF8.GetBytes(token));
    }

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
            PaymentProvider = order.PaymentProvider,
            PaymentReference = order.PaymentReference,
            ShipmentCount = shipmentCount,
            LatestTrackingNumber = latestTracking,
            NextAction = nextAction,
            ShippingMethodId = order.ShippingMethodId,
            ShippingMethodName = order.ShippingMethodName,
            ShippingCost = order.ShippingCost,
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
                CreatedBy = r.CreatedBy
            }).ToList()
        };
    }

    // ──────────── PUBLIC ENDPOINTS ────────────

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder(OrderCreateRequest request)
    {
        if (!await IsGuestCheckoutAllowedAsync() && !IsCallerAuthenticated())
            return Unauthorized("ثبت سفارش مهمان غیرفعال است. لطفاً ابتدا وارد شوید.");

        if (request.Items == null || !request.Items.Any())
            return BadRequest("سبد خرید خالی است.");

        int? userId = await ResolveSiteUserIdAsync();

        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var now = DateTimeOffset.UtcNow;
        var orderItems = new List<OrderItem>();
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
            if (!product.IsAvailable || product.StockQuantity < item.Quantity)
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
            OrderNumber = GenerateOrderNumber(),
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
            AccessToken = Guid.NewGuid().ToString("N")
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
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

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

                coupon.CurrentUsageCount++;
                _db.Coupons.Update(coupon);
            }
        }

        foreach (var oi in orderItems)
        {
            order.Items.Add(oi);
        }

        order.TotalAmount += order.ShippingCost;

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await AddOrderEventAsync(order.Id, "created", null, OrderStatus.PendingPayment, "سفارش ثبت شد");
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

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
            ShippingCost = order.ShippingCost
        });
    }

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
                PaymentProvider = order.PaymentProvider,
                PaymentReference = order.PaymentReference,
                ShippingMethodId = order.ShippingMethodId,
                ShippingMethodName = order.ShippingMethodName,
                ShippingCost = order.ShippingCost,
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
                    CreatedBy = r.CreatedBy
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
    public async Task<IActionResult> UploadReceipt(int id, [FromForm] List<IFormFile> files, [FromForm] string? trackingNumber, [FromForm] string? accessToken)
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
        var todayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);

        var totalCount = await _db.Orders.CountAsync();
        var pendingReceiptCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.ReceiptUploaded);
        var readyToShipCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Approved && !o.Shipments.Any());
        var failedShipmentCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Approved && o.Shipments.Any(s => s.Status == OrderShipmentStatus.Failed || s.Status == OrderShipmentStatus.Returned));
        var missingTrackingCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Approved && o.Shipments.Any() && !o.Shipments.Any(s => s.TrackingNumber != null));
        var todayAmount = await _db.Orders.Where(o => o.CreatedAt >= todayStart).SumAsync(o => o.TotalAmount);
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

        if (request.Status == previousStatus)
            return BadRequest("وضعیت سفارش قبلاً روی همین حالت تنظیم شده است.");

        if (request.Status == OrderStatus.Completed && previousStatus != OrderStatus.Approved)
            return BadRequest("فقط سفارشی که تایید شده است می‌تواند 'تکمیل شده' شود.");

        if (!AllowedTransitions.Contains((previousStatus, request.Status)))
            return BadRequest("انتقال وضعیت انتخابی برای این سفارش مجاز نیست.");

        if (request.Status == OrderStatus.Approved && GetReceiptUrls(order).Count == 0 && order.PaymentProvider == null)
            return BadRequest("برای تایید سفارش، ابتدا باید رسید پرداخت آپلود شده باشد.");

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
        await AddOrderEventAsync(order.Id, "status_changed", previousStatus, request.Status, $"تغییر وضعیت به «{statusLabel}»");
        await _notificationEvents.NotifyOrderStatusChangedAsync(order, previousStatus);
        if (stageAssigned)
            await _notificationEvents.NotifyOrderStageChangedAsync(order, null);

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
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

        if ((order.Status is OrderStatus.Approved or OrderStatus.Completed) && !string.IsNullOrWhiteSpace(order.PaymentReference))
            return Content(PaymentResultHtml(true, "این پرداخت قبلاً تایید شده است.", order), "text/html; charset=utf-8");

        var providerKey = order.PaymentProvider.HasValue
            ? PaymentProviderCatalog.ToProviderKey(order.PaymentProvider.Value)
            : null;
        if (string.IsNullOrEmpty(providerKey) || providerKey == "manual")
            return Content(PaymentResultHtml(false, "شناسه درگاه برای این سفارش ثبت نشده است.", order), "text/html; charset=utf-8");

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
            ? $"<p class=\"note\"><a href=\"/order-tracking?orderNumber={Uri.EscapeDataString(order.OrderNumber)}\">پیگیری سفارش</a></p>"
            : "";
        var iconClass = success ? "icon ok" : "icon fail";
        var icon = success ? "&#10003;" : "&#10007;";
        var title = success ? "پرداخت موفق" : "پرداخت ناموفق";
        var color = success ? "#16a34a" : "#dc2626";
        var messageText = System.Net.WebUtility.HtmlEncode(message ?? "");

        return $@"<!DOCTYPE html>
<html lang=""fa"" dir=""rtl"">
<head>
<meta charset=""utf-8"">
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
