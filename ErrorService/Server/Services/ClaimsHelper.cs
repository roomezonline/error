using System.Security.Claims;

namespace ErrorService.Server.Services;

public static class ClaimsHelper
{
    public static int GetUserId(ClaimsPrincipal user)
    {
        var idStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(idStr) || !int.TryParse(idStr, out var id))
            throw new InvalidOperationException("Invalid user id claim");
        return id;
    }

    public static int GetWorkshopId(ClaimsPrincipal user)
    {
        var idStr = user.FindFirst("workshop_id")?.Value;
        if (string.IsNullOrWhiteSpace(idStr) || !int.TryParse(idStr, out var id))
            throw new InvalidOperationException("Invalid workshop id claim");
        return id;
    }

    public static int GetWorkshopUserId(ClaimsPrincipal user)
    {
        var idStr = user.FindFirst("workshop_user_id")?.Value;
        if (string.IsNullOrWhiteSpace(idStr) || !int.TryParse(idStr, out var id))
            throw new InvalidOperationException("Invalid workshop user id claim");
        return id;
    }

    public static int GetMaxRank(ClaimsPrincipal user)
    {
        var rankStr = user.FindFirst("max_rank")?.Value;
        if (string.IsNullOrWhiteSpace(rankStr) || !int.TryParse(rankStr, out var rank))
            return 0;
        return rank;
    }
}
