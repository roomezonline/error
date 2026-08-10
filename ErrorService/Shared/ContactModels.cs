using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class ContactMessageRequest
{
    [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
    [MinLength(2, ErrorMessage = "نام کوتاه است")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت موبایل صحیح نیست")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "فرمت ایمیل صحیح نیست")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "موضوع پیام الزامی است")]
    [MinLength(3, ErrorMessage = "موضوع کوتاه است")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "متن پیام الزامی است")]
    [MinLength(5, ErrorMessage = "متن پیام کوتاه است")]
    public string Message { get; set; } = string.Empty;
}

public sealed class ContactMessageDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public string CreatedAtFa { get; set; } = string.Empty;
}
