using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class WorkshopCustomer
{
    public int Id { get; set; }

    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = default!;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Mobile { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Address { get; set; }

    public bool IsBadPayer { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<CustomerReceipt> Receipts { get; set; } = new List<CustomerReceipt>();
}

public sealed class CustomerReceipt
{
    public int Id { get; set; }

    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = default!;

    public int CustomerId { get; set; }
    public WorkshopCustomer Customer { get; set; } = default!;

    public int DeviceTypeId { get; set; }
    public DeviceType DeviceType { get; set; } = default!;

    public int? DeviceBrandId { get; set; }
    public DeviceBrand? DeviceBrand { get; set; }

    [Required]
    [MaxLength(3000)]
    public string ProblemDescription { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ReceiptImageUrl { get; set; }

    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "pending";

    public int? TechnicianId { get; set; }
    public WorkshopUser? Technician { get; set; }

    public int? CreatedByUserId { get; set; }
    public WorkshopUser? CreatedByUser { get; set; }

    public CustomerReceiptBilling? Billing { get; set; }
    public DigitalCommitment? DigitalCommitment { get; set; }
    public ICollection<MonitoringReceiptConnection> MonitoringReceiptConnections { get; set; } = new List<MonitoringReceiptConnection>();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CustomerReceiptBilling
{
    public int Id { get; set; }

    public int CustomerReceiptId { get; set; }
    public CustomerReceipt CustomerReceipt { get; set; } = default!;

    public long BillTotal { get; set; }
    public long Discount { get; set; }
    public long Prepaid { get; set; }
    public long Paid { get; set; }

    public int? SelectedBankAccountId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<CustomerReceiptInvoiceItem> Items { get; set; } = new List<CustomerReceiptInvoiceItem>();
}

public sealed class CustomerReceiptInvoiceItem
{
    public int Id { get; set; }
    public int CustomerReceiptBillingId { get; set; }
    public CustomerReceiptBilling Billing { get; set; } = default!;

    public int? CatalogItemId { get; set; }
    public WorkshopInvoiceCatalogItem? CatalogItem { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string ActionDescription { get; set; } = string.Empty;

    public long UnitPrice { get; set; }
    public int Quantity { get; set; }

    public int? TechnicianId { get; set; }
    public WorkshopUser? Technician { get; set; }
}

public sealed class PrintSettings
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string? HeaderTitle { get; set; }

    [MaxLength(200)]
    public string? WorkshopName { get; set; }

    public bool ShowPrintDate { get; set; } = true;
    public bool ShowInvoiceNumber { get; set; } = true;

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
