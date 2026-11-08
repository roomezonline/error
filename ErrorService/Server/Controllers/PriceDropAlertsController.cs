using System.Security.Claims;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/price-drop-alerts")]
[Authorize]
public sealed class PriceDropAlertsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public PriceDropAlertsController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    private int? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("user_id")
                  ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<PriceDropAlertMineDto>> GetMine()
    {
        var userId = CurrentUserId();
        if (!userId.HasValue)
            return Ok(new PriceDropAlertMineDto());

        var ids = await _db.PriceDropAlerts
            .AsNoTracking()
            .Where(x => x.AppUserId == userId.Value && x.Status == PriceDropAlertStatus.Active)
            .Select(x => x.ProductId)
            .ToListAsync();

        return Ok(new PriceDropAlertMineDto { ActiveProductIds = ids });
    }

    [HttpPost("{productId:int}")]
    public async Task<IActionResult> Subscribe(int productId)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue)
            return Unauthorized("برای ثبت یادآوری باید وارد حساب کاربری شوید.");

        var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
        if (settings is { EnablePriceDropAlerts: false })
            return BadRequest("یادآوری کاهش قیمت توسط مدیر غیرفعال شده است.");

        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == productId && !x.IsDraft);
        if (product == null)
            return NotFound("محصول یافت نشد.");

        var effectivePrice = DiscountHelper.GetEffectivePrice(product);
        if (effectivePrice <= 0)
            return BadRequest("برای این محصول قیمت معتبر ثبت نشده است.");

        var alert = await _db.PriceDropAlerts
            .FirstOrDefaultAsync(x => x.AppUserId == userId.Value && x.ProductId == productId);

        if (alert == null)
        {
            _db.PriceDropAlerts.Add(new PriceDropAlert
            {
                AppUserId = userId.Value,
                ProductId = productId,
                SubscribedPrice = effectivePrice,
                Status = PriceDropAlertStatus.Active
            });
        }
        else
        {
            alert.Status = PriceDropAlertStatus.Active;
            alert.SubscribedPrice = effectivePrice;
            alert.NotifiedAt = null;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique (AppUserId, ProductId) race — subscription already exists.
        }

        return Ok(new PriceDropAlertMineDto { ActiveProductIds = await ActiveProductIdsAsync(userId.Value) });
    }

    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> Unsubscribe(int productId)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var rows = await _db.PriceDropAlerts
            .Where(x => x.AppUserId == userId.Value && x.ProductId == productId)
            .ToListAsync();

        if (rows.Count > 0)
        {
            _db.PriceDropAlerts.RemoveRange(rows);
            await _db.SaveChangesAsync();
        }

        return Ok();
    }

    private async Task<List<int>> ActiveProductIdsAsync(int userId) =>
        await _db.PriceDropAlerts
            .AsNoTracking()
            .Where(x => x.AppUserId == userId && x.Status == PriceDropAlertStatus.Active)
            .Select(x => x.ProductId)
            .ToListAsync();
}
