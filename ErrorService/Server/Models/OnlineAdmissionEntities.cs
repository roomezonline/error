using System.ComponentModel.DataAnnotations;
using ErrorService.Shared.Models;

namespace ErrorService.Server.Models;

public sealed class OnlineAdmissionRequest
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // expertise, repair

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "pending";

    [Required]
    [MaxLength(200)]
    public string CustomerFullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string CustomerMobile { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? CustomerAddress { get; set; }

    public int? ProvinceId { get; set; }
    public Province? Province { get; set; }

    public int? CityId { get; set; }
    public City? City { get; set; }

    public int DeviceTypeId { get; set; }
    public DeviceType DeviceType { get; set; } = default!;

    public int? DeviceBrandId { get; set; }
    public DeviceBrand? DeviceBrand { get; set; }

    [Required]
    [MaxLength(3000)]
    public string ProblemDescription { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? DeviceImageUrl { get; set; }

    [MaxLength(500)]
    public string? ReceiptImageUrl { get; set; }
    
    public long? ExpertiseAmount { get; set; }

    [MaxLength(2000)]
    public string? AdminNote { get; set; }

    public int? AssignedWorkshopId { get; set; }
    public Workshop? AssignedWorkshop { get; set; }

    public int? CreatedReceiptId { get; set; }
    public CustomerReceipt? CreatedReceipt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class AdmissionExpertiseSetting
{
    public int Id { get; set; }
    public long Amount { get; set; }
    public string? HtmlDescription { get; set; }
    public bool IsEnabled { get; set; } = true;
}
