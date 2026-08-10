using ErrorService.Server.Data;
using ErrorService.Server.Models;
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

    public OrdersController(ErrorServiceDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
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

    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder(OrderCreateRequest request)
    {
        if (request.Items == null || !request.Items.Any())
            return BadRequest("سبد خرید خالی است.");

        int? userId = await ResolveSiteUserIdAsync();

        var order = new Order
        {
            UserId = userId,
            OrderNumber = $"ORD-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}",
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Address = request.Address,
            Status = OrderStatus.PendingPayment,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            TotalAmount = request.Items.Sum(x => x.Price * x.Quantity)
        };

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == request.CouponCode);
            if (coupon != null && coupon.IsActive &&
                (!coupon.ExpiryDate.HasValue || coupon.ExpiryDate > DateTimeOffset.UtcNow) &&
                (!coupon.MaxUsageCount.HasValue || coupon.CurrentUsageCount < coupon.MaxUsageCount.Value))
            {
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
            }
        }

        foreach (var item in request.Items)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Price = item.Price,
                Quantity = item.Quantity
            });
        }
        
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return Ok(new OrderDto 
        { 
            Id = order.Id, 
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            DiscountAmount = order.DiscountAmount,
            CouponCode = order.CouponCode
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
                ReceiptImageUrl = order.ReceiptImageUrl,
                TrackingNumber = order.TrackingNumber,
                PaymentDate = order.PaymentDate,
                AdminNotes = order.AdminNotes,
                DiscountAmount = order.DiscountAmount,
                CouponCode = order.CouponCode,
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

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOrder(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        return Ok(new OrderDto
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
            Email = order.Email,
            Address = order.Address,
            ReceiptImageUrl = order.ReceiptImageUrl,
            TrackingNumber = order.TrackingNumber,
            PaymentDate = order.PaymentDate,
            AdminNotes = order.AdminNotes,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Price = i.Price,
                Quantity = i.Quantity
            }).ToList()
        });
    }

    [HttpPost("{id:int}/receipt")]
    public async Task<IActionResult> UploadReceipt(int id, [FromForm] IFormFile file, [FromForm] string? trackingNumber)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();

        if (file == null || file.Length == 0) return BadRequest("فایل رسید ارسال نشده است.");

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "receipts");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{id}_{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
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
                PhoneNumber = order.PhoneNumber
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

        _db.Orders.Remove(order);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.orders.manage")]
    [HttpPut("admin/{id:int}/info")]
    public async Task<IActionResult> UpdateOrderInfo(int id, [FromBody] OrderUpdateInfoRequest request)
    {
        var order = await _db.Orders.FindAsync(id);
        if (order == null) return NotFound();

        order.FullName = request.FullName;
        order.PhoneNumber = request.PhoneNumber;
        order.Email = request.Email;
        order.Address = request.Address;
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

        if (request.Status == OrderStatus.Approved && string.IsNullOrWhiteSpace(order.ReceiptImageUrl))
            return BadRequest("برای تایید سفارش، ابتدا باید رسید پرداخت آپلود شده باشد.");

        if (request.Status == OrderStatus.PendingPayment && !string.IsNullOrWhiteSpace(order.ReceiptImageUrl))
            return BadRequest("پس از آپلود رسید، امکان بازگردانی وضعیت به 'در انتظار پرداخت' وجود ندارد.");

        order.Status = request.Status;
        order.AdminNotes = request.AdminNotes;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        if (request.Status == OrderStatus.Approved)
        {
            var orderItems = await _db.OrderItems.Where(i => i.OrderId == order.Id).ToListAsync();
            foreach (var item in orderItems)
            {
                var product = await _db.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.StockQuantity = Math.Max(0, product.StockQuantity - item.Quantity);
                    if (product.StockQuantity == 0)
                        product.IsAvailable = false;
                }
            }
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
