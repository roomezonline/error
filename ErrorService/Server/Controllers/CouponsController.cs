using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CouponsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public CouponsController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpPost("apply")]
    public async Task<ActionResult<CouponApplyResult>> Apply([FromBody] CouponApplyRequest request)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == request.Code);

        if (coupon == null)
            return Ok(new CouponApplyResult { IsValid = false, Message = "کد تخفیف یافت نشد." });

        if (!coupon.IsActive)
            return Ok(new CouponApplyResult { IsValid = false, Message = "این کد تخفیف غیرفعال است." });

        if (coupon.ExpiryDate.HasValue && coupon.ExpiryDate < DateTimeOffset.UtcNow)
            return Ok(new CouponApplyResult { IsValid = false, Message = "کد تخفیف منقضی شده است." });

        if (coupon.MaxUsageCount.HasValue && coupon.CurrentUsageCount >= coupon.MaxUsageCount.Value)
            return Ok(new CouponApplyResult { IsValid = false, Message = "تعداد استفاده از این کد تخفیف به پایان رسیده است." });

        if (coupon.MinOrderAmount.HasValue && request.OrderAmount < coupon.MinOrderAmount.Value)
            return Ok(new CouponApplyResult { IsValid = false, Message = $"حداقل مبلغ سفارش برای این کد تخفیف {coupon.MinOrderAmount.Value:N0} تومان است." });

        decimal discount = 0;
        if (coupon.DiscountPercent.HasValue)
        {
            discount = request.OrderAmount * coupon.DiscountPercent.Value / 100m;
        }
        else if (coupon.DiscountAmount.HasValue)
        {
            discount = coupon.DiscountAmount.Value;
        }

        discount = Math.Min(discount, request.OrderAmount);

        return Ok(new CouponApplyResult
        {
            IsValid = true,
            Message = $"کد تخفیف با موفقیت اعمال شد.",
            DiscountAmount = discount,
            Coupon = MapDto(coupon)
        });
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpGet]
    public async Task<ActionResult<List<CouponDto>>> GetAll()
    {
        return await _db.Coupons.OrderByDescending(c => c.CreatedAt).Select(c => MapDto(c)).ToListAsync();
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CouponDto>> GetById(int id)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null) return NotFound();
        return MapDto(coupon);
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpPost]
    public async Task<ActionResult<CouponDto>> Create([FromBody] CouponDto dto)
    {
        if (await _db.Coupons.AnyAsync(c => c.Code == dto.Code))
            return BadRequest("کد تخفیف با این نام قبلاً ثبت شده است.");

        var coupon = new Coupon
        {
            Code = dto.Code,
            Description = dto.Description,
            DiscountPercent = dto.DiscountPercent,
            DiscountAmount = dto.DiscountAmount,
            MinOrderAmount = dto.MinOrderAmount,
            MaxUsageCount = dto.MaxUsageCount,
            IsActive = dto.IsActive,
            ExpiryDate = dto.ExpiryDate
        };

        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = coupon.Id }, MapDto(coupon));
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CouponDto dto)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null) return NotFound();

        if (await _db.Coupons.AnyAsync(c => c.Code == dto.Code && c.Id != id))
            return BadRequest("کد تخفیف با این نام قبلاً ثبت شده است.");

        coupon.Code = dto.Code;
        coupon.Description = dto.Description;
        coupon.DiscountPercent = dto.DiscountPercent;
        coupon.DiscountAmount = dto.DiscountAmount;
        coupon.MinOrderAmount = dto.MinOrderAmount;
        coupon.MaxUsageCount = dto.MaxUsageCount;
        coupon.IsActive = dto.IsActive;
        coupon.ExpiryDate = dto.ExpiryDate;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null) return NotFound();

        _db.Coupons.Remove(coupon);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static CouponDto MapDto(Coupon c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Description = c.Description,
        DiscountPercent = c.DiscountPercent,
        DiscountAmount = c.DiscountAmount,
        MinOrderAmount = c.MinOrderAmount,
        MaxUsageCount = c.MaxUsageCount,
        CurrentUsageCount = c.CurrentUsageCount,
        IsActive = c.IsActive,
        ExpiryDate = c.ExpiryDate,
        CreatedAt = c.CreatedAt
    };
}
