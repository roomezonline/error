using System.Security.Claims;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/addresses")]
[Authorize]
public sealed class AddressesController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public AddressesController(ErrorServiceDbContext db)
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

    [HttpGet]
    public async Task<ActionResult<List<CustomerAddressDto>>> GetMine()
    {
        var userId = CurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
        if (settings is { EnableSavedAddresses: false })
            return Ok(new List<CustomerAddressDto>());

        var items = await _db.CustomerAddresses
            .AsNoTracking()
            .Where(x => x.AppUserId == userId.Value)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.UpdatedAt)
            .Select(x => new CustomerAddressDto
            {
                Id = x.Id,
                Title = x.Title,
                FullName = x.FullName,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                ProvinceId = x.ProvinceId,
                CityId = x.CityId,
                ProvinceName = x.ProvinceName,
                CityName = x.CityName,
                PostalCode = x.PostalCode,
                Address = x.Address,
                IsDefault = x.IsDefault
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<List<CustomerAddressDto>>> Create(CustomerAddressUpsertRequest request)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
        if (settings is { EnableSavedAddresses: false })
            return BadRequest("ذخیرهٔ آدرس توسط مدیر غیرفعال شده است.");

        var existing = await _db.CustomerAddresses
            .Where(x => x.AppUserId == userId.Value)
            .ToListAsync();

        var max = Math.Clamp(settings?.MaxSavedAddresses ?? 5, 1, 20);
        if (existing.Count >= max)
            return BadRequest($"حداکثر {max} آدرس قابل ذخیره است.");

        var address = new CustomerAddress
        {
            AppUserId = userId.Value,
            IsDefault = request.IsDefault || existing.Count == 0
        };
        Apply(address, request);

        if (address.IsDefault)
            foreach (var other in existing) other.IsDefault = false;

        _db.CustomerAddresses.Add(address);
        await _db.SaveChangesAsync();

        return Ok(await ListAsync(userId.Value));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<List<CustomerAddressDto>>> Update(int id, CustomerAddressUpsertRequest request)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var address = await _db.CustomerAddresses
            .FirstOrDefaultAsync(x => x.Id == id && x.AppUserId == userId.Value);
        if (address == null) return NotFound();

        Apply(address, request);
        address.UpdatedAt = DateTimeOffset.UtcNow;

        if (request.IsDefault)
        {
            var others = await _db.CustomerAddresses
                .Where(x => x.AppUserId == userId.Value && x.Id != id && x.IsDefault)
                .ToListAsync();
            foreach (var other in others) other.IsDefault = false;
            address.IsDefault = true;
        }

        await _db.SaveChangesAsync();
        return Ok(await ListAsync(userId.Value));
    }

    [HttpPost("{id:int}/default")]
    public async Task<ActionResult<List<CustomerAddressDto>>> SetDefault(int id)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var all = await _db.CustomerAddresses
            .Where(x => x.AppUserId == userId.Value)
            .ToListAsync();

        var target = all.FirstOrDefault(x => x.Id == id);
        if (target == null) return NotFound();

        foreach (var item in all) item.IsDefault = item.Id == id;
        target.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(await ListAsync(userId.Value));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<List<CustomerAddressDto>>> Delete(int id)
    {
        var userId = CurrentUserId();
        if (!userId.HasValue) return Unauthorized();

        var address = await _db.CustomerAddresses
            .FirstOrDefaultAsync(x => x.Id == id && x.AppUserId == userId.Value);
        if (address == null) return NotFound();

        var wasDefault = address.IsDefault;
        _db.CustomerAddresses.Remove(address);
        await _db.SaveChangesAsync();

        if (wasDefault)
        {
            var next = await _db.CustomerAddresses
                .Where(x => x.AppUserId == userId.Value)
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync();
            if (next != null)
            {
                next.IsDefault = true;
                await _db.SaveChangesAsync();
            }
        }

        return Ok(await ListAsync(userId.Value));
    }

    private async Task<List<CustomerAddressDto>> ListAsync(int userId) =>
        await _db.CustomerAddresses
            .AsNoTracking()
            .Where(x => x.AppUserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.UpdatedAt)
            .Select(x => new CustomerAddressDto
            {
                Id = x.Id,
                Title = x.Title,
                FullName = x.FullName,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                ProvinceId = x.ProvinceId,
                CityId = x.CityId,
                ProvinceName = x.ProvinceName,
                CityName = x.CityName,
                PostalCode = x.PostalCode,
                Address = x.Address,
                IsDefault = x.IsDefault
            })
            .ToListAsync();

    private static void Apply(CustomerAddress address, CustomerAddressUpsertRequest request)
    {
        address.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        address.FullName = request.FullName.Trim();
        address.PhoneNumber = request.PhoneNumber.Trim();
        address.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        address.ProvinceId = request.ProvinceId;
        address.CityId = request.CityId;
        address.ProvinceName = request.ProvinceName?.Trim();
        address.CityName = request.CityName?.Trim();
        address.PostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode.Trim();
        address.Address = request.Address.Trim();
    }

}
