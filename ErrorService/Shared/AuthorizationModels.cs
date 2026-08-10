using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class PermissionDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string TitleFa { get; set; } = string.Empty;
    public string? DescriptionFa { get; set; }
    public string? GroupFa { get; set; }
}

public sealed class RoleDto
{
    public int Id { get; set; }
    public int? WorkshopId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string TitleFa { get; set; } = string.Empty;
    public string? DescriptionFa { get; set; }
    public bool IsSystem { get; set; }
    public int Rank { get; set; }
    public List<string> PermissionKeys { get; set; } = new();
}

public sealed class RoleUpsertRequest
{
    [Required(ErrorMessage = "کلید نقش الزامی است")]
    [MinLength(3, ErrorMessage = "کلید نقش کوتاه است")]
    public string Key { get; set; } = string.Empty;

    [Required(ErrorMessage = "عنوان نقش الزامی است")]
    public string TitleFa { get; set; } = string.Empty;

    public string? DescriptionFa { get; set; }

    public List<string> PermissionKeys { get; set; } = new();

    public int? WorkshopId { get; set; }
}

public sealed class WorkshopUserDto
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsTechnician { get; set; }
    public List<string> RoleKeys { get; set; } = new();
}

public sealed class WorkshopUserUpsertRequest
{
    [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت موبایل صحیح نیست")]
    public string PhoneNumber { get; set; } = string.Empty;

    [MinLength(4, ErrorMessage = "رمز عبور کوتاه است")]
    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsTechnician { get; set; } = false;
    public List<string> RoleKeys { get; set; } = new();

    public int? WorkshopId { get; set; }
}

public sealed class WorkshopLoginRequest
{
    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت موبایل صحیح نیست")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور الزامی است")]
    public string Password { get; set; } = string.Empty;
}

public sealed class WorkshopAuthResponse
{
    public string Token { get; set; } = string.Empty;
    public WorkshopUserDto User { get; set; } = new();
}

public sealed class ResetPasswordRequest
{
    [Required(ErrorMessage = "رمز عبور جدید الزامی است")]
    [MinLength(4, ErrorMessage = "رمز عبور کوتاه است")]
    public string NewPassword { get; set; } = string.Empty;
}
