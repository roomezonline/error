using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/products/{productId:int}/specs")]
public sealed class ProductSpecsController : ControllerBase
{
    private const string CacheTag = "product-specs";

    private readonly ErrorServiceDbContext _db;
    private readonly Microsoft.AspNetCore.OutputCaching.IOutputCacheStore _cacheStore;

    public ProductSpecsController(ErrorServiceDbContext db, Microsoft.AspNetCore.OutputCaching.IOutputCacheStore cacheStore)
    {
        _db = db;
        _cacheStore = cacheStore;
    }

    private async Task EvictAsync()
    {
        try { await _cacheStore.EvictByTagAsync(CacheTag, CancellationToken.None); } catch { }
        try { await _cacheStore.EvictByTagAsync("products", CancellationToken.None); } catch { }
        ErrorService.Server.Infrastructure.SeoFallbackMiddleware.InvalidateHtmlCache();
    }

    [HttpGet]
    [OutputCache(Duration = 600, VaryByRouteValueNames = new[] { "productId" }, Tags = new[] { CacheTag, "products" })]
    public async Task<ActionResult<List<ProductSpecDto>>> GetSpecs(int productId)
    {
        return await _db.ProductSpecs.AsNoTracking()
            .Where(s => s.ProductId == productId)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .Select(s => new ProductSpecDto
            {
                Id = s.Id,
                ProductId = s.ProductId,
                SpecKey = s.SpecKey,
                SpecValue = s.SpecValue,
                SortOrder = s.SortOrder
            })
            .ToListAsync();
    }

    [Authorize(Policy = "perm:admin.products.manage")]
    [HttpPut]
    public async Task<IActionResult> ReplaceSpecs(int productId, [FromBody] ProductSpecsUpsertRequest request)
    {
        var product = await _db.Products.FindAsync(productId);
        if (product == null) return NotFound("محصول یافت نشد");

        var incoming = (request.Specs ?? new List<ProductSpecDto>())
            .Select((s, i) => new
            {
                Key = (s.SpecKey ?? string.Empty).Trim(),
                Value = (s.SpecValue ?? string.Empty).Trim(),
                Order = i
            })
            .Where(s => s.Key.Length > 0 && s.Value.Length > 0)
            .ToList();

        if (incoming.Count > 60)
            return BadRequest("حداکثر ۶۰ ردیف مشخصات فنی مجاز است.");

        var existing = await _db.ProductSpecs.Where(s => s.ProductId == productId).ToListAsync();
        _db.ProductSpecs.RemoveRange(existing);

        foreach (var s in incoming)
        {
            _db.ProductSpecs.Add(new ProductSpec
            {
                ProductId = productId,
                SpecKey = s.Key.Length > 120 ? s.Key[..120] : s.Key,
                SpecValue = s.Value.Length > 500 ? s.Value[..500] : s.Value,
                SortOrder = s.Order,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        await EvictAsync();
        return Ok(new { saved = incoming.Count });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "perm:admin.products.manage")]
    public async Task<IActionResult> DeleteSpec(int productId, int id)
    {
        var spec = await _db.ProductSpecs.FirstOrDefaultAsync(s => s.Id == id && s.ProductId == productId);
        if (spec == null) return NotFound();
        _db.ProductSpecs.Remove(spec);
        await _db.SaveChangesAsync();
        await EvictAsync();
        return NoContent();
    }
}
