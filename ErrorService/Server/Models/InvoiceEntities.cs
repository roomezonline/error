using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class WorkshopInvoiceCatalogItem
{
    public int Id { get; set; }
    
    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = default!;

    public int? DeviceTypeId { get; set; }
    public DeviceType? DeviceType { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public bool SendSms { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
