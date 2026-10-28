using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public CartController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    private int? CurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    // هر تغییر نسخهٔ یکتا می‌گیرد؛ مقایسهٔ نسخه در همگام‌سازی جلوی
    // بازنویسی تغییرات ادمین با سبد قدیمی کلاینت را می‌گیرد.
    private static long NextRevision() => DateTimeOffset.UtcNow.UtcTicks;

    private async Task<CartStateDto> LoadStateAsync(int userId)
    {
        var revision = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.CartRevision)
            .FirstOrDefaultAsync();

        var items = await _db.CartItems.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();

        return new CartStateDto
        {
            Revision = revision,
            UpdatedAt = items.Count == 0 ? null : items.Max(i => i.UpdatedAt),
            TotalAmount = items.Sum(i => i.Price * i.Quantity),
            Items = items.Select(c => new CartItemDto
            {
                ProductId = c.ProductId,
                ProductName = c.ProductName,
                ImageUrl = c.ImageUrl,
                Price = c.Price,
                Quantity = c.Quantity
            }).ToList()
        };
    }

    private void SetRevisionHeader(long revision)
        => Response.Headers["X-Cart-Revision"] = revision.ToString();

    /// <summary>اعمال مجموعهٔ نهایی سبد (جایگزینی کامل) با اعتبارسنجی محصول/موجودی.</summary>
    private async Task ApplyTargetsAsync(int userId, Dictionary<int, int> desired)
    {
        var serverItems = await _db.CartItems.Where(c => c.UserId == userId).ToListAsync();

        // خودترمیمی ردیف‌های تکراری قدیمی: فقط اولین ردیف می‌ماند.
        var duplicates = serverItems
            .GroupBy(s => s.ProductId)
            .SelectMany(g => g.Skip(1))
            .ToList();
        if (duplicates.Count > 0)
        {
            _db.CartItems.RemoveRange(duplicates);
            serverItems = serverItems.Except(duplicates).ToList();
        }

        var toRemove = serverItems.Where(s => !desired.ContainsKey(s.ProductId)).ToList();
        if (toRemove.Count > 0)
            _db.CartItems.RemoveRange(toRemove);

        if (desired.Count == 0) return;

        var productIds = desired.Keys.ToList();
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var (productId, wantedQuantity) in desired)
        {
            var existing = serverItems.FirstOrDefault(s => s.ProductId == productId);

            if (!products.TryGetValue(productId, out var product)
                || product.IsDraft
                || !product.IsAvailable
                || product.StockQuantity <= 0)
            {
                if (existing != null)
                    _db.CartItems.Remove(existing);
                continue;
            }

            var quantity = Math.Min(wantedQuantity, product.StockQuantity);
            if (quantity <= 0)
            {
                if (existing != null) _db.CartItems.Remove(existing);
                continue;
            }

            var price = DiscountHelper.IsActive(product) ? product.DiscountPrice!.Value : product.Price;

            if (existing != null)
            {
                existing.Quantity = quantity;
                existing.ProductName = product.Name;
                existing.ImageUrl = product.MainImageUrl;
                existing.Price = price;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                _db.CartItems.Add(new CartItem
                {
                    UserId = userId,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ImageUrl = product.MainImageUrl,
                    Price = price,
                    Quantity = quantity
                });
            }
        }
    }

    private static Dictionary<int, int> Normalize(IEnumerable<CartItemDto>? items)
    {
        var result = new Dictionary<int, int>();
        foreach (var item in items ?? Enumerable.Empty<CartItemDto>())
        {
            if (item.ProductId <= 0 || item.Quantity <= 0) continue;
            result[item.ProductId] = Math.Max(result.GetValueOrDefault(item.ProductId), item.Quantity);
        }
        return result;
    }

    private async Task<long> SaveRevisionAsync(int userId)
    {
        var revision = NextRevision();
        await _db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.CartRevision, revision));
        return revision;
    }

    [HttpGet]
    public async Task<ActionResult<List<CartItemDto>>> GetCart()
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        return await _db.CartItems
            .Where(c => c.UserId == userId.Value)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new CartItemDto
            {
                ProductId = c.ProductId,
                ProductName = c.ProductName,
                ImageUrl = c.ImageUrl,
                Price = c.Price,
                Quantity = c.Quantity
            })
            .ToListAsync();
    }

    [HttpGet("state")]
    public async Task<ActionResult<CartStateDto>> GetState()
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await LoadStateAsync(userId.Value));
    }

    /// <summary>
    /// همگام‌سازی نسخه‌دار: فقط وقتی BaseRevision با سرور یکی است جایگزینی کامل
    /// انجام می‌شود. BaseRevision = 0 یعنی کاربر مهمان است و سبد محلی با سبد
    /// حساب merge می‌شود (هیچ آیتمی حذف نمی‌شود).
    /// </summary>
    [HttpPost("sync-v2")]
    public async Task<ActionResult<CartSyncResult>> SyncCartV2([FromBody] CartSyncRequest request)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user == null) return Unauthorized();

        var serverRevision = user.CartRevision;
        var clientItems = Normalize(request.Items);

        if (request.BaseRevision > 0 && request.BaseRevision != serverRevision)
        {
            return Conflict(new CartSyncResult
            {
                Accepted = false,
                Message = "سبد خرید شما هم‌زمان تغییر کرده است؛ نسخهٔ سرور اعمال شد.",
                State = await LoadStateAsync(userId.Value)
            });
        }

        if (request.BaseRevision == 0)
        {
            // merge سبد مهمان: اجتماع سبد سرور و محلی با بیشترین تعداد
            var serverItems = await _db.CartItems.AsNoTracking()
                .Where(c => c.UserId == userId.Value)
                .ToListAsync();

            var merged = serverItems
                .GroupBy(c => c.ProductId)
                .ToDictionary(g => g.Key, g => g.Max(c => c.Quantity));

            foreach (var (productId, quantity) in clientItems)
                merged[productId] = Math.Max(merged.GetValueOrDefault(productId), quantity);

            await ApplyTargetsAsync(userId.Value, merged);
        }
        else
        {
            await ApplyTargetsAsync(userId.Value, clientItems);
        }

        var revision = await SaveRevisionAsync(userId.Value);
        await _db.SaveChangesAsync();

        SetRevisionHeader(revision);
        return Ok(new CartSyncResult
        {
            Accepted = true,
            State = await LoadStateAsync(userId.Value)
        });
    }

    /// <summary>نسخهٔ قدیمی همگام‌سازی (کلاینت‌های کش‌شده) — بدون محافظت نسخه.</summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncCart([FromBody] List<CartItemDto> clientItems)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        await ApplyTargetsAsync(userId.Value, Normalize(clientItems));
        var revision = await SaveRevisionAsync(userId.Value);
        await _db.SaveChangesAsync();
        SetRevisionHeader(revision);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> AddItem([FromBody] CartItemDto item)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        if (item.Quantity <= 0)
            return BadRequest("تعداد باید بزرگتر از صفر باشد.");

        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == item.ProductId);
        if (product == null || product.IsDraft) return NotFound();
        if (!product.IsAvailable || product.StockQuantity <= 0)
            return BadRequest("این محصول در حال حاضر موجود نیست.");

        var existing = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == userId.Value && c.ProductId == item.ProductId);
        var quantity = Math.Min((existing?.Quantity ?? 0) + item.Quantity, product.StockQuantity);
        var price = DiscountHelper.IsActive(product) ? product.DiscountPrice!.Value : product.Price;

        if (existing != null)
        {
            existing.Quantity = quantity;
            existing.ProductName = product.Name;
            existing.ImageUrl = product.MainImageUrl;
            existing.Price = price;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            _db.CartItems.Add(new CartItem
            {
                UserId = userId.Value,
                ProductId = product.Id,
                ProductName = product.Name,
                ImageUrl = product.MainImageUrl,
                Price = price,
                Quantity = quantity
            });
        }

        var revision = await SaveRevisionAsync(userId.Value);
        await _db.SaveChangesAsync();
        SetRevisionHeader(revision);
        return NoContent();
    }

    [HttpPut("{productId:int}")]
    public async Task<IActionResult> UpdateQuantity(int productId, [FromBody] CartItemUpdateDto request)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == userId.Value && c.ProductId == productId);
        if (item == null) return NotFound();

        if (request.Quantity <= 0)
        {
            _db.CartItems.Remove(item);
        }
        else
        {
            var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null || product.IsDraft || !product.IsAvailable || product.StockQuantity <= 0)
                return BadRequest("این محصول در حال حاضر موجود نیست.");

            item.Quantity = Math.Min(request.Quantity, product.StockQuantity);
            item.ProductName = product.Name;
            item.ImageUrl = product.MainImageUrl;
            item.Price = DiscountHelper.IsActive(product) ? product.DiscountPrice!.Value : product.Price;
            item.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var revision = await SaveRevisionAsync(userId.Value);
        await _db.SaveChangesAsync();
        SetRevisionHeader(revision);
        return NoContent();
    }

    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> RemoveItem(int productId)
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == userId.Value && c.ProductId == productId);
        if (item == null) return NotFound();

        _db.CartItems.Remove(item);
        var revision = await SaveRevisionAsync(userId.Value);
        await _db.SaveChangesAsync();
        SetRevisionHeader(revision);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart()
    {
        var userId = CurrentUserId();
        if (userId == null) return Unauthorized();

        var items = await _db.CartItems.Where(c => c.UserId == userId.Value).ToListAsync();
        _db.CartItems.RemoveRange(items);
        var revision = await SaveRevisionAsync(userId.Value);
        await _db.SaveChangesAsync();
        SetRevisionHeader(revision);
        return NoContent();
    }
}
