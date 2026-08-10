using System.ComponentModel.DataAnnotations;
using ErrorService.Shared;

namespace ErrorService.Server.Models;

public sealed class DigitalCommitment
{
    public int Id { get; set; }

    public int CustomerReceiptId { get; set; }
    public CustomerReceipt CustomerReceipt { get; set; } = default!;

    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = default!;

    public int CustomerId { get; set; }
    public WorkshopCustomer Customer { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string CustomerFullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string CustomerMobile { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DeviceTypeName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DeviceBrandName { get; set; }

    public int ReceiptNumber { get; set; }

    [Required]
    [MaxLength(20)]
    public string RegisteredAtFa { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string WorkshopName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? WorkshopLogoUrl { get; set; }

    [MaxLength(2000)]
    public string? ExtraBodyText { get; set; }

    [Required]
    [MaxLength(4000)]
    public string FullBodyText { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = CommitmentStatuses.Pending;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SignedAt { get; set; }

    [MaxLength(50)]
    public string? SenderLineNumber { get; set; }

    [MaxLength(20)]
    public string? VerificationCode { get; set; }

    public int? CreatedByUserId { get; set; }
    public WorkshopUser? CreatedByUser { get; set; }
}
