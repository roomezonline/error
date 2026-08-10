using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class AdminSiteUserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsVip { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AdminSiteUsersQuery
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsVip { get; set; }
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
