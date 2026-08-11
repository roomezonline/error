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

    [HttpGet]
    public async Task<ActionResult<List<CartItemDto>>> GetCart()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        return await _db.CartItems
            .Where(c => c.UserId == userId)
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

    [HttpPost("sync")]
    public async Task<IActionResult> SyncCart([FromBody] List<CartItemDto> clientItems)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        var serverItems = await _db.CartItems.Where(c => c.UserId == userId).ToListAsync();

        foreach (var clientItem in clientItems)
        {
            var existing = serverItems.FirstOrDefault(s => s.ProductId == clientItem.ProductId);
            if (existing != null)
            {
                existing.Quantity = Math.Max(existing.Quantity, clientItem.Quantity);
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                var product = await _db.Products.FindAsync(clientItem.ProductId);
                if (product != null)
                {
                    _db.CartItems.Add(new CartItem
                    {
                        UserId = userId,
                        ProductId = clientItem.ProductId,
                        ProductName = product.Name,
                        ImageUrl = product.MainImageUrl,
                        Price = DiscountHelper.IsActive(product) ? product.DiscountPrice!.Value : product.Price,
                        Quantity = clientItem.Quantity
                    });
                }
            }
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> AddItem([FromBody] CartItemDto item)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        var existing = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == item.ProductId);

        if (existing != null)
        {
            existing.Quantity += item.Quantity;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            var product = await _db.Products.FindAsync(item.ProductId);
            if (product == null) return NotFound();

            _db.CartItems.Add(new CartItem
            {
                UserId = userId,
                ProductId = item.ProductId,
                ProductName = product.Name,
                ImageUrl = product.MainImageUrl,
                Price = DiscountHelper.IsActive(product) ? product.DiscountPrice!.Value : product.Price,
                Quantity = item.Quantity
            });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{productId:int}")]
    public async Task<IActionResult> UpdateQuantity(int productId, [FromBody] CartItemUpdateDto request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);
        if (item == null) return NotFound();

        if (request.Quantity <= 0)
        {
            _db.CartItems.Remove(item);
        }
        else
        {
            item.Quantity = request.Quantity;
            item.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> RemoveItem(int productId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        var item = await _db.CartItems.FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);
        if (item == null) return NotFound();

        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        var items = await _db.CartItems.Where(c => c.UserId == userId).ToListAsync();
        _db.CartItems.RemoveRange(items);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
