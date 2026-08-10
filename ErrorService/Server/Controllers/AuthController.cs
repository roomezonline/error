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
    private readonly PasswordHasher<AppUser> _userHasher = new();
    private readonly PasswordHasher<WorkshopUser> _workshopHasher = new();

    public AuthController(ErrorServiceDbContext db, JwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
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
        user.CreatedAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        _db.Users.Add(user);
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

        // Find user in both tables simultaneously
        var user = await _db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phone);
        var workshopUser = await _db.WorkshopUsers.FirstOrDefaultAsync(x => x.PhoneNumber == phone);

        // Neither exists
        if (user == null && workshopUser == null)
            return BadRequest("موبایل یا رمز عبور اشتباه است");

        // Site user exists — verify password
        if (user != null)
        {
            if (!user.IsActive)
                return BadRequest("حساب کاربری غیرفعال است");

            var siteResult = _userHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (siteResult == PasswordVerificationResult.Failed)
            {
                // If workshop user also exists, try workshop password before failing
                if (workshopUser != null)
                {
                    var wsVerify = _workshopHasher.VerifyHashedPassword(workshopUser, workshopUser.PasswordHash, request.Password);
                    if (wsVerify == PasswordVerificationResult.Failed)
                        return BadRequest("موبایل یا رمز عبور اشتباه است");

                    if (!workshopUser.IsActive)
                        return BadRequest("حساب کاربری غیرفعال است");

                    var wsToken = await _jwt.CreateWorkshopTokenAsync(workshopUser);
                    return Ok(new UnifiedAuthResponse
                    {
                        Token = wsToken,
                        UserType = "workshop",
                        FullName = workshopUser.FullName,
                        PhoneNumber = workshopUser.PhoneNumber
                    });
                }
                return BadRequest("موبایل یا رمز عبور اشتباه است");
            }

            // Site user password matched
            // Check if user has super_admin role — super_admins ALWAYS get a site token
            var hasSuperAdmin = await _db.AppUserRoles
                .AnyAsync(x => x.UserId == user.Id && x.Role.Key == "super_admin");

            if (hasSuperAdmin)
            {
                var token = await _jwt.CreateTokenAsync(user);
                return Ok(new UnifiedAuthResponse
                {
                    Token = token,
                    UserType = "site_user",
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber
                });
            }

            // Non-super-admin site user: if they also have a workshop account, give workshop token
            if (workshopUser != null)
            {
                var wsVerify = _workshopHasher.VerifyHashedPassword(workshopUser, workshopUser.PasswordHash, request.Password);
                if (wsVerify != PasswordVerificationResult.Failed && workshopUser.IsActive)
                {
                    var wsToken = await _jwt.CreateWorkshopTokenAsync(workshopUser);
                    return Ok(new UnifiedAuthResponse
                    {
                        Token = wsToken,
                        UserType = "workshop",
                        FullName = workshopUser.FullName,
                        PhoneNumber = workshopUser.PhoneNumber
                    });
                }
            }

            // Regular site user only
            var siteToken = await _jwt.CreateTokenAsync(user);
            return Ok(new UnifiedAuthResponse
            {
                Token = siteToken,
                UserType = "site_user",
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber
            });
        }

        // Only workshop user exists
        if (!workshopUser!.IsActive)
            return BadRequest("حساب کاربری غیرفعال است");

        var verify = _workshopHasher.VerifyHashedPassword(workshopUser, workshopUser.PasswordHash, request.Password);
        if (verify == PasswordVerificationResult.Failed)
            return BadRequest("موبایل یا رمز عبور اشتباه است");

        var token2 = await _jwt.CreateWorkshopTokenAsync(workshopUser);
        return Ok(new UnifiedAuthResponse
        {
            Token = token2,
            UserType = "workshop",
            FullName = workshopUser.FullName,
            PhoneNumber = workshopUser.PhoneNumber
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> Me()
    {
        // Check if this is a workshop token (no NameIdentifier claim)
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

        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

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
