using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class BankAccountDto
{
    public int Id { get; set; }
    public int WorkshopId { get; set; } = 1;
    public string Title { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string? OwnerName { get; set; }
    public string? CardNumber { get; set; }
    public string? AccountNumber { get; set; }
    public string? Iban { get; set; }
    public string? IconUrl { get; set; }
    public bool ShowInGateway { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

public sealed class BankAccountUpsertRequest
{
    [Required(ErrorMessage = "عنوان الزامی است")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public int WorkshopId { get; set; } = 1;

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
}
