using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/workshop-auth")]
public sealed class WorkshopAuthController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly PasswordHasher<WorkshopUser> _hasher = new();

    public WorkshopAuthController(ErrorServiceDbContext db, JwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    public async Task<ActionResult<WorkshopAuthResponse>> Login([FromBody] WorkshopLoginRequest request)
    {
        var phone = request.PhoneNumber.Trim();
        var user = await _db.WorkshopUsers
            .FirstOrDefaultAsync(x => x.PhoneNumber == phone);

        if (user == null)
            return BadRequest("موبایل یا رمز عبور اشتباه است");

        if (!user.IsActive)
            return BadRequest("حساب کاربری غیرفعال است");

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
            return BadRequest("موبایل یا رمز عبور اشتباه است");

        var token = await _jwt.CreateWorkshopTokenAsync(user);
        var dto = await BuildUserDto(user.Id);

        return Ok(new WorkshopAuthResponse { Token = token, User = dto });
    }

    [Authorize]
    [HttpGet("users")]
    public async Task<ActionResult<List<WorkshopUserDto>>> GetUsers([FromQuery] int? workshopId = null)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        int? targetWorkshopId = null;
        
        if (!isSuperAdmin)
        {
            if (!User.HasClaim("perm", "workshop.users.manage")) return Forbid();
            targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
        }
        else
        {
            targetWorkshopId = workshopId;
        }

        var query = _db.WorkshopUsers.AsNoTracking();
        
        if (targetWorkshopId.HasValue)
        {
            query = query.Where(x => x.WorkshopId == targetWorkshopId.Value);
        }

        var users = await query
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.FullName)
            .ToListAsync();

        var ids = users.Select(x => x.Id).ToList();
        var roles = await _db.WorkshopUserRoles
            .AsNoTracking()
            .Where(x => ids.Contains(x.WorkshopUserId))
            .Include(x => x.Role)
            .ToListAsync();

        var result = users.Select(u => new WorkshopUserDto
        {
            Id = u.Id,
            WorkshopId = u.WorkshopId,
            FullName = u.FullName,
            PhoneNumber = u.PhoneNumber,
            IsActive = u.IsActive,
            IsTechnician = u.IsTechnician,
            RoleKeys = roles.Where(r => r.WorkshopUserId == u.Id).Select(r => r.Role.Key).Distinct().OrderBy(x => x).ToList()
        }).ToList();

        return Ok(result);
    }

    [Authorize(Policy = "perm:workshop.users.manage")]
    [HttpPost("users")]
    public async Task<ActionResult<WorkshopUserDto>> CreateUser([FromBody] WorkshopUserUpsertRequest request)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        int workshopId;

        if (isSuperAdmin && request.WorkshopId.HasValue)
        {
            workshopId = request.WorkshopId.Value;
        }
        else
        {
            workshopId = ClaimsHelper.GetWorkshopId(User);
        }

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("رمز عبور الزامی است");

        var phone = request.PhoneNumber.Trim();
        var exists = await _db.WorkshopUsers.AnyAsync(x => x.WorkshopId == workshopId && x.PhoneNumber == phone);
        if (exists)
            return BadRequest("این شماره موبایل قبلاً برای این کارگاه ثبت شده است");

        var user = new WorkshopUser
        {
            WorkshopId = workshopId,
            FullName = request.FullName.Trim(),
            PhoneNumber = phone,
            IsActive = request.IsActive,
            IsTechnician = request.IsTechnician,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.WorkshopUsers.Add(user);
        await _db.SaveChangesAsync();

        await SetWorkshopUserRoles(user.Id, request.RoleKeys);
        return Ok(await BuildUserDto(user.Id));
    }

    [Authorize(Policy = "perm:workshop.users.manage")]
    [HttpPut("users/{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] WorkshopUserUpsertRequest request)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        var userQuery = _db.WorkshopUsers.AsQueryable();
        
        if (!isSuperAdmin)
        {
            var workshopId = ClaimsHelper.GetWorkshopId(User);
            userQuery = userQuery.Where(x => x.WorkshopId == workshopId);
        }

        var user = await userQuery.FirstOrDefaultAsync(x => x.Id == id);
        if (user == null) return NotFound();

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber.Trim();
        user.IsActive = request.IsActive;
        user.IsTechnician = request.IsTechnician;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        if (isSuperAdmin && request.WorkshopId.HasValue)
        {
            user.WorkshopId = request.WorkshopId.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
        }

        await _db.SaveChangesAsync();

        await SetWorkshopUserRoles(user.Id, request.RoleKeys);
        return NoContent();
    }

    public sealed class DeleteCheckResponse
    {
        public bool CanHardDelete { get; set; }
        public bool HasTransactionalRefs { get; set; }
    }

    [Authorize(Policy = "perm:workshop.users.manage")]
    [HttpGet("users/{id:int}/delete-check")]
    public async Task<ActionResult<DeleteCheckResponse>> DeleteCheck(int id)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        var userQuery = _db.WorkshopUsers.AsQueryable();

        if (!isSuperAdmin)
        {
            var workshopId = ClaimsHelper.GetWorkshopId(User);
            userQuery = userQuery.Where(x => x.WorkshopId == workshopId);
        }

        var userExists = await userQuery.AsNoTracking().AnyAsync(x => x.Id == id);
        if (!userExists) return NotFound();

        var hasRefs = await HasAnyTransactionalRefsAsync(id);
        return Ok(new DeleteCheckResponse
        {
            HasTransactionalRefs = hasRefs,
            CanHardDelete = !hasRefs
        });
    }

    [Authorize(Policy = "perm:workshop.users.manage")]
    [HttpPut("users/{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        var userQuery = _db.WorkshopUsers.AsQueryable();

        if (!isSuperAdmin)
        {
            var workshopId = ClaimsHelper.GetWorkshopId(User);
            userQuery = userQuery.Where(x => x.WorkshopId == workshopId);
        }

        var user = await userQuery.FirstOrDefaultAsync(x => x.Id == id);
        if (user == null) return NotFound();

        user.IsActive = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> HasAnyTransactionalRefsAsync(int workshopUserId)
    {
        var inReceipts = await _db.CustomerReceipts.AsNoTracking().AnyAsync(x =>
            x.TechnicianId == workshopUserId || x.CreatedByUserId == workshopUserId);
        if (inReceipts) return true;

        var inInvoiceItems = await _db.Set<CustomerReceiptInvoiceItem>().AsNoTracking().AnyAsync(x =>
            x.TechnicianId == workshopUserId);
        return inInvoiceItems;
    }

    [Authorize(Policy = "perm:workshop.users.manage")]
    [HttpPut("users/{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        var userQuery = _db.WorkshopUsers.AsQueryable();

        if (!isSuperAdmin)
        {
            var workshopId = ClaimsHelper.GetWorkshopId(User);
            userQuery = userQuery.Where(x => x.WorkshopId == workshopId);
        }

        var user = await userQuery.FirstOrDefaultAsync(x => x.Id == id);
        if (user == null) return NotFound();

        user.PasswordHash = _hasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = "perm:workshop.users.manage")]
    [HttpPut("users/{id:int}/toggle-status")]
    public async Task<IActionResult> ToggleUserStatus(int id)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        var userQuery = _db.WorkshopUsers.AsQueryable();

        if (!isSuperAdmin)
        {
            var workshopId = ClaimsHelper.GetWorkshopId(User);
            userQuery = userQuery.Where(x => x.WorkshopId == workshopId);
        }

        var user = await userQuery.FirstOrDefaultAsync(x => x.Id == id);
        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = "perm:workshop.users.manage")]
    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser(int id, [FromQuery] bool force = false)
    {
        var isSuperAdmin = User.IsInRole("super_admin");
        var userQuery = _db.WorkshopUsers.AsQueryable();

        if (!isSuperAdmin)
        {
            var workshopId = ClaimsHelper.GetWorkshopId(User);
            userQuery = userQuery.Where(x => x.WorkshopId == workshopId);
        }

        var user = await userQuery.FirstOrDefaultAsync(x => x.Id == id);
        if (user == null) return NotFound();

        var hasRefs = await HasAnyTransactionalRefsAsync(id);
        if (hasRefs && !force)
        {
            return Conflict("این پرسنل دارای سابقه رسید/فاکتور است و امکان حذف کامل ندارد. ابتدا آن را غیرفعال کنید.");
        }

        // Remove roles first due to FK constraints
        var roles = await _db.WorkshopUserRoles.Where(x => x.WorkshopUserId == id).ToListAsync();
        _db.WorkshopUserRoles.RemoveRange(roles);

        _db.WorkshopUsers.Remove(user);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private async Task SetWorkshopUserRoles(int workshopUserId, List<string> roleKeys)
    {
        roleKeys ??= new();
        var normalized = roleKeys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        var isSuperAdmin = User.IsInRole("super_admin");
        int? workshopId = null;
        
        if (!isSuperAdmin)
        {
            workshopId = ClaimsHelper.GetWorkshopId(User);
        }
        else
        {
            var user = await _db.WorkshopUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == workshopUserId);
            workshopId = user?.WorkshopId;
        }

        var roleIds = await _db.Roles
            .Where(x => normalized.Contains(x.Key) && (x.WorkshopId == workshopId || x.WorkshopId == null))
            .Select(x => x.Id)
            .ToListAsync();

        var existing = await _db.WorkshopUserRoles.Where(x => x.WorkshopUserId == workshopUserId).ToListAsync();
        _db.WorkshopUserRoles.RemoveRange(existing);

        _db.WorkshopUserRoles.AddRange(roleIds.Select(rid => new WorkshopUserRole { WorkshopUserId = workshopUserId, RoleId = rid }));
        await _db.SaveChangesAsync();
    }

    private async Task<WorkshopUserDto> BuildUserDto(int id)
    {
        var user = await _db.WorkshopUsers.AsNoTracking().FirstAsync(x => x.Id == id);
        var roles = await _db.WorkshopUserRoles
            .AsNoTracking()
            .Where(x => x.WorkshopUserId == id)
            .Include(x => x.Role)
            .Select(x => x.Role.Key)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return new WorkshopUserDto
        {
            Id = user.Id,
            WorkshopId = user.WorkshopId,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            IsTechnician = user.IsTechnician,
            RoleKeys = roles
        };
    }
}
