using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public class RegisterRequest
{
    [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
    [MinLength(3, ErrorMessage = "نام کوتاه است")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت موبایل صحیح نیست")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور الزامی است")]
    [MinLength(4, ErrorMessage = "رمز عبور کوتاه است")]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت موبایل صحیح نیست")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور الزامی است")]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public UserProfileDto User { get; set; } = new();
}

public class UserProfileDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "رمز عبور فعلی الزامی است")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور جدید الزامی است")]
    [MinLength(4, ErrorMessage = "رمز عبور کوتاه است")]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class UnifiedAuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty; // "workshop" or "site_user"
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
    [MinLength(3, ErrorMessage = "نام کوتاه است")]
    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
}
