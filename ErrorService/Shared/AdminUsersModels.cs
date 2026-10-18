using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class AdminSiteUserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsVip { get; set; }
    public string? PasswordPlain { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool ChatAiDisabled { get; set; }
    public int? ChatAiDailyLimitOverride { get; set; }
}

public sealed class AdminSiteUsersQuery
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsVip { get; set; }
    public bool IncludeDeleted { get; set; }
    public int Take { get; set; } = 200;
}

public sealed class AdminResetSiteUserPasswordRequest
{
    [Required(ErrorMessage = "رمز عبور جدید الزامی است")]
    [MinLength(4, ErrorMessage = "رمز عبور کوتاه است")]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class AdminSetSiteUserActiveRequest
{
    public bool IsActive { get; set; }
}

public sealed class AdminSetSiteUserVipRequest
{
    public bool IsVip { get; set; }
}

public sealed class AdminSetSiteUserAiRequest
{
    public bool Disabled { get; set; }
    public int? DailyLimitOverride { get; set; }
}
