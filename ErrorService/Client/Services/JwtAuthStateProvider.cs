using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Text.Json;

namespace ErrorService.Client.Services;

public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthService _auth;

    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public JwtAuthStateProvider(AuthService auth)
    {
        _auth = auth;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var claims = new List<Claim>();

        // 1. Check for public auth token
        var authToken = await _auth.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(authToken))
        {
            var authIdentity = JwtParser.ParseClaimsFromJwt(authToken);
            claims.AddRange(authIdentity.Claims);
        }

        // 2. Check for workshop token
        var workshopToken = await _auth.GetWorkshopTokenAsync();
        if (!string.IsNullOrWhiteSpace(workshopToken))
        {
            var workshopIdentity = JwtParser.ParseClaimsFromJwt(workshopToken);
            // Merge claims, avoiding duplicates if necessary (though they usually differ by type)
            foreach (var claim in workshopIdentity.Claims)
            {
                if (!claims.Any(c => c.Type == claim.Type && c.Value == claim.Value))
                {
                    claims.Add(claim);
                }
            }
        }

        if (!claims.Any())
            return new AuthenticationState(Anonymous);

        var finalIdentity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new AuthenticationState(new ClaimsPrincipal(finalIdentity));
    }

    public void NotifyAuthStateChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}

internal static class JwtParser
{
    public static ClaimsIdentity ParseClaimsFromJwt(string jwt)
    {
        // NOTE: This does not validate signature; API validates. This is only for UI state.
        // If token is invalid/expired, API calls will fail and user can re-login.
        var claims = new List<Claim>();

        var parts = jwt.Split('.');
        if (parts.Length < 2) return new ClaimsIdentity();

        var payload = parts[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        using var doc = JsonDocument.Parse(jsonBytes);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return new ClaimsIdentity();

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            var key = prop.Name;
            var value = prop.Value;

            // common claim keys in .NET
            if (key.EndsWith("/nameidentifier", StringComparison.OrdinalIgnoreCase))
            {
                AddClaimValue(claims, ClaimTypes.NameIdentifier, value);
                continue;
            }

            if (key.EndsWith("/name", StringComparison.OrdinalIgnoreCase))
            {
                AddClaimValue(claims, ClaimTypes.Name, value);
                continue;
            }

            // .NET JwtSecurityToken maps ClaimTypes.Name to "unique_name" short key
            if (string.Equals(key, "unique_name", StringComparison.OrdinalIgnoreCase))
            {
                AddClaimValue(claims, ClaimTypes.Name, value);
                continue;
            }

            // .NET JwtSecurityToken maps ClaimTypes.NameIdentifier to "nameid" short key
            if (string.Equals(key, "nameid", StringComparison.OrdinalIgnoreCase))
            {
                AddClaimValue(claims, ClaimTypes.NameIdentifier, value);
                continue;
            }

            if (key.EndsWith("/mobilephone", StringComparison.OrdinalIgnoreCase))
            {
                AddClaimValue(claims, ClaimTypes.MobilePhone, value);
                continue;
            }

            // role claim can appear as "role" or schema-based claim
            if (string.Equals(key, "role", StringComparison.OrdinalIgnoreCase) ||
                key.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
            {
                AddClaimValue(claims, ClaimTypes.Role, value);
                continue;
            }

            AddClaimValue(claims, key, value);
        }

        return new ClaimsIdentity(claims, authenticationType: "jwt");
    }

    private static void AddClaimValue(List<Claim> claims, string claimType, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                AddClaimValue(claims, claimType, item);
            return;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var s = element.GetString();
            if (!string.IsNullOrWhiteSpace(s))
                claims.Add(new Claim(claimType, s));
            return;
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            claims.Add(new Claim(claimType, element.ToString()));
            return;
        }

        if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
        {
            claims.Add(new Claim(claimType, element.GetBoolean().ToString()));
            return;
        }

        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            return;

        claims.Add(new Claim(claimType, element.ToString()));
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        base64 = base64.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
