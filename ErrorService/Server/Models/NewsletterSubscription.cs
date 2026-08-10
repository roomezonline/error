using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class NewsletterSubscription
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
