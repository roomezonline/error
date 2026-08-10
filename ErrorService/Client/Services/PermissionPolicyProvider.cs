using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ErrorService.Client.Services;

public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    private const string PolicyPrefix = "perm:";

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : base(options)
    {
    }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!string.IsNullOrWhiteSpace(policyName) && policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permKey = policyName.Substring(PolicyPrefix.Length);

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx =>
                    ctx.User.IsInRole("super_admin") ||
                    ctx.User.Claims.Any(c =>
                        string.Equals(c.Type, "perm", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Value, permKey, StringComparison.OrdinalIgnoreCase)))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return base.GetPolicyAsync(policyName);
    }
}
