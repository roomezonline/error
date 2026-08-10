using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

public class JwtTokenService
{
    private readonly IConfiguration _config;
    private readonly ErrorServiceDbContext _db;

    public JwtTokenService(IConfiguration config, ErrorServiceDbContext db)
    {
        _config = config;
        _db = db;
    }

    public async Task<string> CreateTokenAsync(AppUser user, CancellationToken ct = default)
    {
        var key = _config["Auth:JwtKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Missing Auth:JwtKey in configuration.");

        var issuer = _config["Auth:Issuer"] ?? "ErrorService";
        var audience = _config["Auth:Audience"] ?? "ErrorService";
        var expiresMinutes = int.TryParse(_config["Auth:TokenMinutes"], out var m) ? m : 60 * 24 * 7;

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var roleKeys = await _db.AppUserRoles
            .Where(x => x.UserId == user.Id)
            .Select(x => x.Role.Key)
            .Distinct()
            .ToListAsync(ct);

        var permKeys = await _db.AppUserRoles
            .Where(x => x.UserId == user.Id)
            .SelectMany(x => _db.RolePermissions.Where(rp => rp.RoleId == x.RoleId))
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToListAsync(ct);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.MobilePhone, user.PhoneNumber)
        };

        foreach (var role in roleKeys)
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var perm in permKeys)
            claims.Add(new Claim("perm", perm));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> CreateWorkshopTokenAsync(WorkshopUser user, CancellationToken ct = default)
    {
        var key = _config["Auth:JwtKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Missing Auth:JwtKey in configuration.");

        var issuer = _config["Auth:Issuer"] ?? "ErrorService";
        var audience = _config["Auth:Audience"] ?? "ErrorService";
        var expiresMinutes = int.TryParse(_config["Auth:TokenMinutes"], out var m) ? m : 60 * 24 * 7;

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var roleKeys = await _db.WorkshopUserRoles
            .Where(x => x.WorkshopUserId == user.Id)
            .Select(x => x.Role.Key)
            .Distinct()
            .ToListAsync(ct);

        var permKeys = await _db.WorkshopUserRoles
            .Where(x => x.WorkshopUserId == user.Id)
            .SelectMany(x => _db.RolePermissions.Where(rp => rp.RoleId == x.RoleId))
            .Select(rp => rp.Permission.Key)
            .Distinct()
            .ToListAsync(ct);

        var maxRank = await _db.WorkshopUserRoles
            .Where(x => x.WorkshopUserId == user.Id)
            .SelectMany(x => _db.Roles.Where(r => r.Id == x.RoleId))
            .MaxAsync(r => (int?)r.Rank, ct) ?? 0;

        var claims = new List<Claim>
        {
            new("workshop_user_id", user.Id.ToString()),
            new("workshop_id", user.WorkshopId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.MobilePhone, user.PhoneNumber),
            new("max_rank", maxRank.ToString())
        };

        foreach (var role in roleKeys)
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var perm in permKeys)
            claims.Add(new Claim("perm", perm));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
