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
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly NotificationEventService _notificationEvents;
    private readonly PasswordHasher<AppUser> _userHasher = new();
    private readonly PasswordHasher<WorkshopUser> _workshopHasher = new();

    public AuthController(ErrorServiceDbContext db, JwtTokenService jwt, NotificationEventService notificationEvents)
    {
        _db = db;
        _jwt = jwt;
        _notificationEvents = notificationEvents;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var phone = request.PhoneNumber.Trim();

        var exists = await _db.Users.AnyAsync(x => x.PhoneNumber == phone);
        if (exists)
            return BadRequest("این شماره موبایل قبلاً ثبت شده است");

        var user = new AppUser
        {
            FullName = request.FullName.Trim(),
            PhoneNumber = phone,
        };

        user.PasswordHash = _userHasher.HashPassword(user, request.Password);
        user.PasswordPlain = request.Password;
        user.CreatedAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _notificationEvents.NotifyAsync(
            eventType: "user.registered",
            values: new Dictionary<string, string?>
            {
                ["Phone"] = user.PhoneNumber,
                ["FullName"] = user.FullName
            },
            idempotencyKey: $"user.registered:{user.Id}",
            fallbackSeverity: NotificationSeverity.Success,
            fallbackTitle: "به ارورسرویس خوش آمدید",
            fallbackBody: $"حساب کاربری شما با شماره {user.PhoneNumber} ساخته شد.",
            appUserId: user.Id);
        await _db.SaveChangesAsync();

        var token = await _jwt.CreateTokenAsync(user);
        return Ok(new AuthResponse
        {
            Token = token,
            User = new UserProfileDto { Id = user.Id, FullName = user.FullName, PhoneNumber = user.PhoneNumber }
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var phone = request.PhoneNumber.Trim();
        var user = await _db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phone);
        if (user == null)
            return BadRequest("موبایل یا رمز عبور اشتباه است");

        if (!user.IsActive)
            return BadRequest("حساب کاربری غیرفعال است");

        var result = _userHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return BadRequest("موبایل یا رمز عبور اشتباه است");

        var token = await _jwt.CreateTokenAsync(user);
        return Ok(new AuthResponse
        {
            Token = token,
            User = new UserProfileDto { Id = user.Id, FullName = user.FullName, PhoneNumber = user.PhoneNumber }
        });
    }

    [HttpPost("unified-login")]
    public async Task<ActionResult<UnifiedAuthResponse>> UnifiedLogin([FromBody] LoginRequest request)
    {
        var phone = request.PhoneNumber.Trim();

        var user = await _db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phone);
        var workshopUser = await _db.WorkshopUsers.FirstOrDefaultAsync(x => x.PhoneNumber == phone);

        if (user == null && workshopUser == null)
            return BadRequest("موبایل یا رمز عبور اشتباه است");

        if (user != null && !user.IsActive)
            return BadRequest("حساب کاربری غیرفعال است");

        if (user == null && workshopUser != null && !workshopUser.IsActive)
            return BadRequest("حساب کاربری غیرفعال است");

        var siteMatch = user != null
            && _userHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) != PasswordVerificationResult.Failed;

        var workshopMatch = workshopUser != null && workshopUser.IsActive
            && _workshopHasher.VerifyHashedPassword(workshopUser, workshopUser.PasswordHash, request.Password) != PasswordVerificationResult.Failed;

        if (!siteMatch && !workshopMatch)
            return BadRequest("موبایل یا رمز عبور اشتباه است");

        if (workshopUser != null && !workshopUser.IsActive)
            workshopUser = null;

        // Workshop-only member: auto-create the site side so profile/cart/notifications work too.
        if (user == null)
        {
            user = new AppUser
            {
                FullName = workshopUser!.FullName.Trim(),
                PhoneNumber = phone,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            user.PasswordHash = _userHasher.HashPassword(user, Guid.NewGuid().ToString("N"));
            _db.Users.Add(user);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                user = await _db.Users.FirstAsync(x => x.PhoneNumber == phone);
            }
        }

        var token = workshopUser != null
            ? await _jwt.CreateCombinedTokenAsync(user, workshopUser)
            : await _jwt.CreateTokenAsync(user);

        var siteRoleCount = await _db.AppUserRoles
            .CountAsync(x => x.UserId == user.Id);

        return Ok(new UnifiedAuthResponse
        {
            Token = token,
            UserType = workshopUser != null && siteRoleCount == 0 ? "workshop" : "site_user",
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> Me()
    {
        // Site identity wins when present (combined tokens carry both sides).
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userIdStr) && int.TryParse(userIdStr, out var siteUserId))
        {
            var user = await _db.Users.FindAsync(siteUserId);
            if (user != null)
            {
                return Ok(new UserProfileDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    Email = user.Email,
                    Address = user.Address,
                    PostalCode = user.PostalCode
                });
            }
        }

        // Pure workshop token (legacy sessions without a site identity).
        var workshopIdStr = User.FindFirst("workshop_user_id")?.Value;
        if (!string.IsNullOrWhiteSpace(workshopIdStr) && int.TryParse(workshopIdStr, out var workshopUserId))
        {
            var wsUser = await _db.WorkshopUsers.FindAsync(workshopUserId);
            if (wsUser == null) return Unauthorized();
            return Ok(new UserProfileDto
            {
                Id = wsUser.Id,
                FullName = wsUser.FullName,
                PhoneNumber = wsUser.PhoneNumber
            });
        }

        return Unauthorized();
    }

    [Authorize]
    [HttpGet("auth-state")]
    public ActionResult<AuthStateDto> GetAuthState()
    {
        return Ok(new AuthStateDto
        {
            IsSuperAdmin = User.IsInRole("super_admin")
        });
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        user.FullName = request.FullName.Trim();
        user.Email = request.Email?.Trim();
        user.Address = request.Address?.Trim();
        user.PostalCode = request.PostalCode?.Trim();
        user.UpdatedAt = DateTimeOffset.UtcNow;
        
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        var verify = _userHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verify == PasswordVerificationResult.Failed)
            return BadRequest("رمز عبور فعلی اشتباه است");

        user.PasswordHash = _userHasher.HashPassword(user, request.NewPassword);
        user.PasswordPlain = request.NewPassword;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private int GetUserId()
    {
        var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(idStr) || !int.TryParse(idStr, out var id))
            throw new InvalidOperationException("Invalid user id claim");
        return id;
    }
}
