using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/roles")]
public sealed class RolesController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public RolesController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [Authorize(Policy = "perm:security.roles.manage")]
    [HttpGet("permissions")]
    public async Task<ActionResult<List<PermissionDto>>> GetPermissions()
    {
        var all = PermissionRegistry.All;
        var isSuperAdmin = User.IsInRole("super_admin");

        if (!isSuperAdmin)
        {
            // For workshop admins, only show permissions they actually possess
            var userPermissions = User.Claims
                .Where(c => c.Type == "perm")
                .Select(c => c.Value)
                .ToList();

            all = all.Where(x => userPermissions.Contains(x.Key, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        var list = all.Select(x => new PermissionDto
        {
            Key = x.Key,
            TitleFa = x.TitleFa,
            DescriptionFa = x.DescriptionFa,
            GroupFa = x.GroupFa
        }).ToList();

        return Ok(list);
    }

    [Authorize(Policy = "perm:security.roles.manage")]
    [HttpGet]
    public async Task<ActionResult<List<RoleDto>>> GetRoles([FromQuery] int? workshopId = null)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        int? targetWorkshopId = null;
        
        if (!isSuperAdmin)
        {
            targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
        }
        else
        {
            targetWorkshopId = workshopId;
        }

        var query = _db.Roles.AsNoTracking();
        
        if (targetWorkshopId.HasValue)
        {
            query = query.Where(x => x.WorkshopId == targetWorkshopId.Value || x.WorkshopId == null);
        }
        else
        {
            query = query.Where(x => x.WorkshopId == null);
        }

        // Non-super-admin: exclude super_admin role and filter by their max rank
        if (!isSuperAdmin)
        {
            var maxRank = ClaimsHelper.GetMaxRank(User);
            query = query.Where(x => x.Key != "super_admin" && x.Rank <= maxRank);
        }

        var roles = await query
            .OrderByDescending(x => x.IsSystem)
            .ThenBy(x => x.TitleFa)
            .ToListAsync();

        var roleIds = roles.Select(x => x.Id).ToList();
        var rp = await _db.RolePermissions
            .AsNoTracking()
            .Where(x => roleIds.Contains(x.RoleId))
            .Include(x => x.Permission)
            .ToListAsync();

        var result = roles.Select(r => new RoleDto
        {
            Id = r.Id,
            WorkshopId = r.WorkshopId,
            Key = r.Key,
            TitleFa = r.TitleFa,
            DescriptionFa = r.DescriptionFa,
            IsSystem = r.IsSystem,
            PermissionKeys = rp.Where(x => x.RoleId == r.Id).Select(x => x.Permission.Key).Distinct().OrderBy(x => x).ToList(),
            Rank = r.Rank
        }).ToList();

        return Ok(result);
    }

    [Authorize(Policy = "perm:security.roles.manage")]
    [HttpPost]
    public async Task<ActionResult<RoleDto>> CreateRole([FromBody] RoleUpsertRequest request)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        
        if (!isSuperAdmin && request.WorkshopId.HasValue)
            return BadRequest("فقط سوپرادمین می‌تواند نقش برای کارگاه دیگر ایجاد کند");

        var key = (request.Key ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest("کلید نقش الزامی است");

        int? targetWorkshopId = null;
        if (!isSuperAdmin)
        {
            targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
        }
        else
        {
            targetWorkshopId = request.WorkshopId;
        }

        var exists = await _db.Roles.AnyAsync(x => x.WorkshopId == targetWorkshopId && x.Key == key);
        if (exists)
            return BadRequest("این کلید نقش قبلاً ثبت شده است");

        var role = new Role
        {
            WorkshopId = targetWorkshopId,
            Key = key,
            TitleFa = (request.TitleFa ?? string.Empty).Trim(),
            DescriptionFa = string.IsNullOrWhiteSpace(request.DescriptionFa) ? null : request.DescriptionFa.Trim(),
            IsSystem = false,
            Rank = 0
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        await SetRolePermissions(role.Id, request.PermissionKeys);

        var dto = await BuildRoleDto(role.Id);
        return Ok(dto);
    }

    [Authorize(Policy = "perm:security.roles.manage")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] RoleUpsertRequest request)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(x => x.Id == id);
        if (role == null) return NotFound();
        
        var isSuperAdmin = User.IsInRole("super_admin");
        if (role.IsSystem && !isSuperAdmin) return BadRequest("نقش سیستمی قابل ویرایش نیست");

        role.TitleFa = (request.TitleFa ?? string.Empty).Trim();
        role.DescriptionFa = string.IsNullOrWhiteSpace(request.DescriptionFa) ? null : request.DescriptionFa.Trim();

        await _db.SaveChangesAsync();

        await SetRolePermissions(role.Id, request.PermissionKeys);
        return NoContent();
    }

    [Authorize(Policy = "perm:security.roles.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRole(int id)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(x => x.Id == id);
        if (role == null) return NotFound();
        
        var isSuperAdmin = User.IsInRole("super_admin");
        if (role.IsSystem && !isSuperAdmin) return BadRequest("نقش سیستمی قابل حذف نیست");
        
        // Prevent deletion of critical system roles even by super admin
        if (role.IsSystem && (role.Key == "super_admin"))
            return BadRequest("نقش سوپر ادمین قابل حذف نیست");

        var used = await _db.AppUserRoles.AnyAsync(x => x.RoleId == id) || await _db.WorkshopUserRoles.AnyAsync(x => x.RoleId == id);
        if (used) return BadRequest("این نقش به کاربران تخصیص داده شده و قابل حذف نیست");

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task SetRolePermissions(int roleId, List<string> keys)
    {
        keys ??= new();
        var normalized = keys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var isSuperAdmin = User.IsInRole("super_admin");
        if (!isSuperAdmin)
        {
            // Security: Enforce that a workshop admin cannot grant permissions they don't have
            var userPermissions = User.Claims
                .Where(c => c.Type == "perm")
                .Select(c => c.Value)
                .ToList();

            normalized = normalized.Intersect(userPermissions, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var permIds = await _db.Permissions
            .Where(x => normalized.Contains(x.Key))
            .Select(x => x.Id)
            .ToListAsync();

        var existing = await _db.RolePermissions.Where(x => x.RoleId == roleId).ToListAsync();
        _db.RolePermissions.RemoveRange(existing);

        _db.RolePermissions.AddRange(permIds.Select(pid => new Server.Models.RolePermission { RoleId = roleId, PermissionId = pid }));
        await _db.SaveChangesAsync();
    }

    private async Task<RoleDto> BuildRoleDto(int roleId)
    {
        var role = await _db.Roles.AsNoTracking().FirstAsync(x => x.Id == roleId);
        var perms = await _db.RolePermissions
            .AsNoTracking()
            .Where(x => x.RoleId == roleId)
            .Include(x => x.Permission)
            .Select(x => x.Permission.Key)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return new RoleDto
        {
            Id = role.Id,
            WorkshopId = role.WorkshopId,
            Key = role.Key,
            TitleFa = role.TitleFa,
            DescriptionFa = role.DescriptionFa,
            IsSystem = role.IsSystem,
            Rank = role.Rank,
            PermissionKeys = perms
        };
    }
}
