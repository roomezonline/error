using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShippingMethodsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public ShippingMethodsController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<List<ShippingMethodDto>> GetActive()
    {
        return await _db.ShippingMethods
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Select(s => new ShippingMethodDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Price = s.Price,
                EstimatedDays = s.EstimatedDays,
                IsActive = s.IsActive,
                SortOrder = s.SortOrder
            })
            .ToListAsync();
    }

    [Authorize(Policy = "perm:admin.shipping.manage")]
    [HttpGet("admin/all")]
    public async Task<List<ShippingMethodDto>> GetAll()
    {
        return await _db.ShippingMethods
            .OrderBy(s => s.SortOrder)
            .Select(s => new ShippingMethodDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Price = s.Price,
                EstimatedDays = s.EstimatedDays,
                IsActive = s.IsActive,
                SortOrder = s.SortOrder
            })
            .ToListAsync();
    }

    [Authorize(Policy = "perm:admin.shipping.manage")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ShippingMethodCreateRequest request)
    {
        var method = new ShippingMethod
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            EstimatedDays = request.EstimatedDays,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };

        _db.ShippingMethods.Add(method);
        await _db.SaveChangesAsync();

        return Ok(new ShippingMethodDto
        {
            Id = method.Id,
            Name = method.Name,
            Description = method.Description,
            Price = method.Price,
            EstimatedDays = method.EstimatedDays,
            IsActive = method.IsActive,
            SortOrder = method.SortOrder
        });
    }

    [Authorize(Policy = "perm:admin.shipping.manage")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ShippingMethodCreateRequest request)
    {
        var method = await _db.ShippingMethods.FindAsync(id);
        if (method == null)
            return NotFound();

        method.Name = request.Name;
        method.Description = request.Description;
        method.Price = request.Price;
        method.EstimatedDays = request.EstimatedDays;
        method.IsActive = request.IsActive;
        method.SortOrder = request.SortOrder;

        await _db.SaveChangesAsync();

        return Ok(new ShippingMethodDto
        {
            Id = method.Id,
            Name = method.Name,
            Description = method.Description,
            Price = method.Price,
            EstimatedDays = method.EstimatedDays,
            IsActive = method.IsActive,
            SortOrder = method.SortOrder
        });
    }

    [Authorize(Policy = "perm:admin.shipping.manage")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var method = await _db.ShippingMethods.FindAsync(id);
        if (method == null)
            return NotFound();

        var hasOrders = await _db.Orders.AnyAsync(o => o.ShippingMethodId == id);
        if (hasOrders)
            return BadRequest(new { message = "این روش ارسال در سفارشات استفاده شده و قابل حذف نیست." });

        _db.ShippingMethods.Remove(method);
        await _db.SaveChangesAsync();

        return Ok(new { message = "حذف شد." });
    }

    [Authorize(Policy = "perm:admin.shipping.manage")]
    [HttpPost("{id}/toggle")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var method = await _db.ShippingMethods.FindAsync(id);
        if (method == null)
            return NotFound();

        method.IsActive = !method.IsActive;
        await _db.SaveChangesAsync();

        return Ok(new { isActive = method.IsActive });
    }
}
