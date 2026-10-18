using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/admin-users")]
public sealed class AdminUsersController : ControllerBase
{
    private const string VipRoleKey = "vip";

    private readonly ErrorServiceDbContext _db;
    private readonly PasswordHasher<AppUser> _hasher = new();

    public AdminUsersController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [Authorize(Policy = "perm:admin.users.manage")]
    [HttpGet]
    public async Task<ActionResult<List<AdminSiteUserDto>>> GetAll([FromQuery] AdminSiteUsersQuery query)
    {
        query ??= new();
        var take = query.Take <= 0 ? 200 : Math.Min(query.Take, 1000);

        var q = _db.Users.AsNoTracking().AsQueryable();

        // کاربران حذف‌نرم‌شده به‌صورت پیش‌فرض پنهان‌اند؛ فقط با درخواست صریح نمایش داده می‌شوند.
        if (!query.IncludeDeleted)
            q = q.Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(x => x.FullName.Contains(s) || x.PhoneNumber.Contains(s));
        }

        if (query.IsActive.HasValue)
        {
            q = q.Where(x => x.IsActive == query.IsActive.Value);
        }

        var users = await q
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync();

        var ids = users.Select(x => x.Id).ToList();
        var vipUserIds = await _db.AppUserRoles
            .AsNoTracking()
            .Where(x => ids.Contains(x.UserId) && x.Role.Key == VipRoleKey)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync();

        var vipSet = vipUserIds.ToHashSet();

        var result = users.Select(x => new AdminSiteUserDto
        {
            Id = x.Id,
            FullName = x.FullName,
            PhoneNumber = x.PhoneNumber,
            IsActive = x.IsActive,
            IsDeleted = x.IsDeleted,
            DeletedAt = x.DeletedAt,
            IsVip = vipSet.Contains(x.Id),
            PasswordPlain = x.PasswordPlain,
            CreatedAt = x.CreatedAt,
            ChatAiDisabled = x.ChatAiDisabled,
            ChatAiDailyLimitOverride = x.ChatAiDailyLimitOverride
        }).ToList();

        if (query.IsVip.HasValue)
        {
            result = result.Where(x => x.IsVip == query.IsVip.Value).ToList();
        }

        return Ok(result);
    }

    [Authorize(Policy = "perm:admin.users.manage")]
    [HttpPut("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] AdminResetSiteUserPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (user == null) return NotFound();

        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        user.PasswordPlain = request.NewPassword;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.users.manage")]
    [HttpPut("{id:int}/set-active")]
    public async Task<IActionResult> SetActive(int id, [FromBody] AdminSetSiteUserActiveRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (user == null) return NotFound();

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.users.manage")]
    [HttpPut("{id:int}/set-vip")]
    public async Task<IActionResult> SetVip(int id, [FromBody] AdminSetSiteUserVipRequest request)
    {
        var userExists = await _db.Users.AsNoTracking().AnyAsync(x => x.Id == id && !x.IsDeleted);
        if (!userExists) return NotFound();

        var vipRoleId = await EnsureVipRoleAsync();

        var existing = await _db.AppUserRoles.FirstOrDefaultAsync(x => x.UserId == id && x.RoleId == vipRoleId);

        if (request.IsVip)
        {
            if (existing == null)
            {
                _db.AppUserRoles.Add(new AppUserRole { UserId = id, RoleId = vipRoleId });
                await _db.SaveChangesAsync();
            }
        }
        else
        {
            if (existing != null)
            {
                _db.AppUserRoles.Remove(existing);
                await _db.SaveChangesAsync();
            }
        }

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.users.manage")]
    [HttpPut("{id:int}/set-ai")]
    public async Task<IActionResult> SetAi(int id, [FromBody] AdminSetSiteUserAiRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        if (user == null) return NotFound();

        user.ChatAiDisabled = request.Disabled;
        user.ChatAiDailyLimitOverride = request.DailyLimitOverride < 0 ? null : request.DailyLimitOverride;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.users.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user == null) return NotFound();
        if (user.IsDeleted) return NoContent();

        // حذف نرم: کاربر پنهان و غیرفعال می‌شود اما رکورد و سوابقش (سفارش، چت، رسید و…) حفظ می‌گردد
        // تا در صورت اشتباه قابل بازگردانی باشد و ارجاع‌های دیتابیس یتیم نشوند.
        user.IsDeleted = true;
        user.DeletedAt = DateTimeOffset.UtcNow;
        user.IsActive = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.users.manage")]
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user == null) return NotFound();

        user.IsDeleted = false;
        user.DeletedAt = null;
        user.IsActive = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<int> EnsureVipRoleAsync()
    {
        var role = await _db.Roles.FirstOrDefaultAsync(x => x.WorkshopId == null && x.Key == VipRoleKey);
        if (role != null) return role.Id;

        role = new Role
        {
            WorkshopId = null,
            Key = VipRoleKey,
            TitleFa = "کاربر VIP",
            DescriptionFa = "دسترسی به محتوای VIP سایت",
            IsSystem = false
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        return role.Id;
    }
}
