using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class BankAccount
{
    public int Id { get; set; }

    public int WorkshopId { get; set; } = 1;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? BankName { get; set; }

    [MaxLength(200)]
    public string? OwnerName { get; set; }

    [MaxLength(32)]
    public string? CardNumber { get; set; }

    [MaxLength(64)]
    public string? AccountNumber { get; set; }

    [MaxLength(64)]
    public string? Iban { get; set; }

    [MaxLength(500)]
    public string? IconUrl { get; set; }

    public bool ShowInGateway { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
