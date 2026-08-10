using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ErrorService.Server.Services;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        var isSuperAdmin = context.User.Claims
            .Any(c => c.Type == ClaimTypes.Role && string.Equals(c.Value, "super_admin", StringComparison.OrdinalIgnoreCase));

        if (isSuperAdmin)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var hasPermission = context.User.Claims
            .Any(c => c.Type == "perm" && string.Equals(c.Value, requirement.PermissionKey, StringComparison.OrdinalIgnoreCase));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
