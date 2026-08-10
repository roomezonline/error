using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class NewsletterSubscribeRequest
{
    [Required(ErrorMessage = "ایمیل الزامی است")]
    [EmailAddress(ErrorMessage = "فرمت ایمیل صحیح نیست")]
    public string Email { get; set; } = string.Empty;
}

public sealed class NewsletterSubscriptionDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string CreatedAtFa { get; set; } = string.Empty;
}
