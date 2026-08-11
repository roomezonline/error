using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services.Payment;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IAuthorizationService _authorizationService;
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;

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

    public OrdersController(ErrorServiceDbContext db, IWebHostEnvironment env, IAuthorizationService authorizationService, IPaymentGatewayFactory paymentGatewayFactory)
    {
        _db = db;
        _env = env;
        _authorizationService = authorizationService;
        _paymentGatewayFactory = paymentGatewayFactory;
    }

    private async Task<List<OrderItem>> LoadOrderItemsAsync(int orderId)
        => await _db.OrderItems.Where(i => i.OrderId == orderId).ToListAsync();

    private async Task RestoreStockAsync(List<OrderItem> items)
    {
        foreach (var item in items)
        {
            var product = await _db.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                product.StockQuantity += item.Quantity;
                product.IsAvailable = true;
            }
        }
    }

    private async Task<bool> ReleaseCouponUsageAsync(int? couponId)
    {
        if (!couponId.HasValue) return false;
        return await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Coupons SET CurrentUsageCount = CASE WHEN CurrentUsageCount > 0 THEN CurrentUsageCount - 1 ELSE 0 END WHERE Id = {couponId.Value}") > 0;
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

    private bool IsCallerAuthenticated() => User.Identity?.IsAuthenticated == true;

    private async Task<bool> IsGuestCheckoutAllowedAsync()
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        return settings?.AllowGuestCheckout ?? true;
    }

    private async Task<int?> ResolveSiteUserIdAsync()
    {
        var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(idStr) && int.TryParse(idStr, out var id))
            return id;

        var phone = User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
            if (user != null) return user.Id;
        }
        return null;
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

        // Guest order: tie by phone for authenticated callers, access token otherwise.
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

            var effectivePrice = product.DiscountPrice.HasValue && product.DiscountExpiryDate.HasValue && product.DiscountExpiryDate > now
                ? product.DiscountPrice.Value
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
            OrderNumber = $"ORD-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}",
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

        await using var tx = await _db.Database.BeginTransactionAsync();

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var coupon = await _db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Code == request.CouponCode);
            if (coupon != null)
            {
                if (!coupon.IsActive)
                    return BadRequest("کد تخفیف غیرفعال است.");

                if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate < now)
                    return BadRequest("کد تخفیف منقضی شده است.");

                if (coupon.MaxUsageCount.HasValue && coupon.CurrentUsageCount >= coupon.MaxUsageCount.Value)
                    return BadRequest("تعداد استفاده از این کد تخفیف به پایان رسیده است.");

                if (coupon.MinOrderAmount.HasValue && order.TotalAmount < coupon.MinOrderAmount.Value)
                    return BadRequest($"حداقل مبلغ سفارش برای این کد تخفیف {coupon.MinOrderAmount.Value:N0} تومان است.");

                var claimed = await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE Coupons SET CurrentUsageCount = CurrentUsageCount + 1 WHERE Id = {coupon.Id} AND IsActive = 1 AND (ExpiryDate IS NULL OR ExpiryDate > {now}) AND (MaxUsageCount IS NULL OR CurrentUsageCount < MaxUsageCount)");
                if (claimed != 1)
                    return BadRequest("کد تخفیف قابل اعمال نیست.");

                decimal discount = 0;
                if (coupon.DiscountPercent.HasValue)
                    discount = order.TotalAmount * coupon.DiscountPercent.Value / 100m;
                else if (coupon.DiscountAmount.HasValue)
                    discount = coupon.DiscountAmount.Value;

                discount = Math.Min(discount, order.TotalAmount);
                order.DiscountAmount = discount;
                order.CouponId = coupon.Id;
                order.CouponCode = coupon.Code;
            }
        }

        // Reserve stock at creation (release on Rejected/Cancelled/delete)
        foreach (var oi in orderItems)
        {
            order.Items.Add(oi);
            var product = products[oi.ProductId];
            product.StockQuantity = Math.Max(0, product.StockQuantity - oi.Quantity);
            if (product.StockQuantity == 0)
                product.IsAvailable = false;
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new OrderDto 
        { 
            Id = order.Id, 
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            DiscountAmount = order.DiscountAmount,
            CouponCode = order.CouponCode,
            AccessToken = order.AccessToken
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

        var items = await _db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == id)
            .ToListAsync(ct);

        return Ok(new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            DiscountAmount = order.DiscountAmount,
            CouponCode = order.CouponCode,
            AccessToken = order.AccessToken,
            CreatedAt = order.CreatedAt,
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
            TrackingNumber = order.TrackingNumber,
            PaymentDate = order.PaymentDate,
            PaymentProvider = order.PaymentProvider,
            PaymentReference = order.PaymentReference,
            Items = items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Price = i.Price,
                Quantity = i.Quantity
            }).ToList()
        });
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<ActionResult<List<OrderDto>>> GetMyOrders()
    {
        var userId = await ResolveSiteUserIdAsync();
        if (userId == null)
            return Unauthorized();

        var isWorkshopToken = User.FindFirst("workshop_user_id") != null;
        var phoneClaim = User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value;

        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId.Value
                || (isWorkshopToken && o.UserId == null && o.PhoneNumber == phoneClaim))
            .OrderByDescending(o => o.CreatedAt)
            .Select(order => new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CreatedAt = order.CreatedAt,
                Status = order.Status,
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
            TrackingNumber = order.TrackingNumber,
            PaymentDate = order.PaymentDate,
            AdminNotes = order.AdminNotes,
            PaymentProvider = order.PaymentProvider,
            PaymentReference = order.PaymentReference,
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

        return Ok(orders);
    }

    [HttpPost("{id:int}/receipt")]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> UploadReceipt(int id, [FromForm] IFormFile file, [FromForm] string? trackingNumber, [FromForm] string? accessToken)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();

        if (!await IsGuestCheckoutAllowedAsync() && !IsCallerAuthenticated())
            return NotFound();

        if (!await CanAccessOrderAsync(order, accessToken))
            return NotFound();

        if (order.Status is OrderStatus.Approved or OrderStatus.Completed or OrderStatus.Rejected or OrderStatus.Cancelled)
            return BadRequest("در وضعیت فعلی سفارش امکان آپلود مجدد رسید وجود ندارد.");

        if (file == null || file.Length == 0) return BadRequest("فایل رسید ارسال نشده است.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";
        var safeExt = ext.ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowed.Contains(safeExt))
            return BadRequest("فرمت فایل مجاز نیست. فقط تصاویر jpg, jpeg, png, webp");

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "receipts");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{id}_{Guid.NewGuid():N}{safeExt}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        order.ReceiptImageUrl = $"/uploads/receipts/{fileName}";
        order.TrackingNumber = trackingNumber;
        order.PaymentDate = DateTimeOffset.UtcNow;
        order.Status = OrderStatus.ReceiptUploaded;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { url = order.ReceiptImageUrl });
    }

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpGet("admin/all")]
    public async Task<ActionResult<List<OrderDto>>> GetAllOrders()
    {
        var orders = await _db.Orders
            .OrderByDescending(o => o.CreatedAt)
            .Select(order => new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CreatedAt = order.CreatedAt,
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
                PaymentProvider = order.PaymentProvider,
                PaymentReference = order.PaymentReference
            })
            .ToListAsync();

        return Ok(orders);
    }

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpDelete("admin/{id:int}")]
    public async Task<IActionResult> DeleteOrder(int id)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();

        await using var tx = await _db.Database.BeginTransactionAsync();

        await RestoreStockAsync(await LoadOrderItemsAsync(order.Id));

        await ReleaseCouponUsageAsync(order.CouponId);

        DeleteReceiptFile(order.ReceiptImageUrl);

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

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

        await _db.SaveChangesAsync();
        return NoContent();
    }

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

        if (request.Status == OrderStatus.Approved && string.IsNullOrWhiteSpace(order.ReceiptImageUrl) && order.PaymentProvider == null)
            return BadRequest("برای تایید سفارش، ابتدا باید رسید پرداخت آپلود شده باشد.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        if (request.Status is OrderStatus.Rejected or OrderStatus.Cancelled)
        {
            // Stock was reserved at order creation — release it back.
            await RestoreStockAsync(await LoadOrderItemsAsync(order.Id));
        }

        if (request.Status is OrderStatus.Rejected or OrderStatus.Cancelled)
            await ReleaseCouponUsageAsync(order.CouponId);

        order.Status = request.Status;
        order.AdminNotes = request.AdminNotes;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

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
            return Content(PaymentResultHtml(false, result.Message ?? "پرداخت ناموفق بود.", order), "text/html; charset=utf-8");

        if (order.Status is not (OrderStatus.Approved or OrderStatus.Completed))
        {
            order.Status = OrderStatus.Approved;
            order.PaymentDate = DateTimeOffset.UtcNow;
            order.PaymentProvider = gateway.Provider;
            order.PaymentReference = result.GatewayUrl;
            order.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return Content(PaymentResultHtml(true, result.Message ?? "پرداخت با موفقیت انجام شد.", order), "text/html; charset=utf-8");
    }

    private static string BuildPaymentCallbackUrl(int orderId, string? callbackBaseUrl)
    {
        var baseUrl = PaymentGatewayFactory.BuildCallbackUrl(callbackBaseUrl);
        if (baseUrl.Contains("{orderId}", StringComparison.Ordinal))
            return baseUrl.Replace("{orderId}", orderId.ToString());
        return baseUrl; // relative path /api/orders/{orderId}/payment/callback already, but ensure concrete
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
