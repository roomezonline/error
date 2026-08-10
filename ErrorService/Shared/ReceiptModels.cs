using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public static class CustomerReceiptStatuses
{
    public const string Pending = "pending";
    public const string NotRepairable = "not_repairable";
    public const string Repaired = "repaired";
    public const string Delivered = "delivered";

    public static readonly string[] All =
    {
        Pending,
        NotRepairable,
        Repaired,
        Delivered
    };
}

public sealed class WorkshopCustomerDto
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsBadPayer { get; set; }
}

public sealed class WorkshopCustomerUpsertRequest
{
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام خانوادگی الزامی است")]
    [MinLength(2, ErrorMessage = "نام خانوادگی کوتاه است")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "فرمت موبایل صحیح نیست")]
    public string Mobile { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "آدرس طولانی است")]
    public string? Address { get; set; }
}

/// <summary>ویرایش مشتری — شماره موبایل قابل تغییر است (با چک تکراری نبودن در کارگاه).</summary>
public sealed class WorkshopCustomerUpdateRequest
{
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام خانوادگی الزامی است")]
    [MinLength(2, ErrorMessage = "نام خانوادگی کوتاه است")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل نامعتبر است")]
    public string Mobile { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "آدرس طولانی است")]
    public string? Address { get; set; }
}

public sealed class CustomerReceiptDto
{
    public int Id { get; set; }

    public int WorkshopId { get; set; }
    public string WorkshopName { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
    public string CustomerMobile { get; set; } = string.Empty;
    public string? CustomerAddress { get; set; }
    public bool CustomerIsBadPayer { get; set; }

    public int DeviceTypeId { get; set; }
    public string DeviceTypeName { get; set; } = string.Empty;

    public int? DeviceBrandId { get; set; }
    public string? DeviceBrandName { get; set; }

    public string ProblemDescription { get; set; } = string.Empty;
    public string? ReceiptImageUrl { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    public string Status { get; set; } = CustomerReceiptStatuses.Pending;

    public int? TechnicianId { get; set; }
    public string? TechnicianName { get; set; }

    public int? CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? CommitmentStatus { get; set; }
    public bool HasActiveMonitoring { get; set; }
    public int MonitoringHistoryCount { get; set; }
}

public sealed class CustomerReceiptCreateRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب مشتری الزامی است")]
    public int CustomerId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب نوع دستگاه الزامی است")]
    public int DeviceTypeId { get; set; }

    public int? DeviceBrandId { get; set; }

    [Required(ErrorMessage = "توضیحات مشکل الزامی است")]
    [MinLength(5, ErrorMessage = "توضیحات کوتاه است")]
    [MaxLength(3000, ErrorMessage = "توضیحات طولانی است")]
    public string ProblemDescription { get; set; } = string.Empty;

    public string Status { get; set; } = CustomerReceiptStatuses.Pending;

    public int? TechnicianId { get; set; }
}

public sealed class ReceiptStatusUpdateRequest
{
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = CustomerReceiptStatuses.Pending;
}

public sealed class CustomerReceiptBillingDto
{
    public int CustomerReceiptId { get; set; }
    public long BillTotal { get; set; }
    public long Discount { get; set; }
    public long Prepaid { get; set; }
    public long Paid { get; set; }
    public int? SelectedBankAccountId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<InvoiceItemDto> Items { get; set; } = new();
}

public sealed class CustomerReceiptBillingUpsertRequest
{
    [Range(0, long.MaxValue)]
    public long BillTotal { get; set; }

    [Range(0, long.MaxValue)]
    public long Discount { get; set; }

    [Range(0, long.MaxValue)]
    public long Prepaid { get; set; }

    [Range(0, long.MaxValue)]
    public long Paid { get; set; }

    public int? SelectedBankAccountId { get; set; }

    public List<InvoiceItemDto> Items { get; set; } = new();
}

public sealed class InvoiceItemDto
{
    public string Title { get; set; } = string.Empty;
    public string ActionDescription { get; set; } = string.Empty;
    public long UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int? TechnicianId { get; set; }
}

public sealed class CustomerFinancialSummaryDto
{
    public long TotalBill { get; set; }
    public long TotalDiscount { get; set; }
    public long TotalPrepaid { get; set; }
    public long TotalPaid { get; set; }
    public long TotalRemaining { get; set; }
    public int TotalReceipts { get; set; }
}

public sealed class PrintSettingsDto
{
    public string? HeaderTitle { get; set; }
    public string? WorkshopName { get; set; }
    public bool ShowPrintDate { get; set; } = true;
    public bool ShowInvoiceNumber { get; set; } = true;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
}

public sealed class PrintSettingsUpsertRequest
{
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
}

public sealed class PrintInvoiceRequest
{
    public int ReceiptId { get; set; }
    public string? HeaderTitle { get; set; }
    public string? WorkshopName { get; set; }
    public bool ShowPrintDate { get; set; } = true;
    public bool ShowInvoiceNumber { get; set; } = true;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
}

public sealed class BulkPrintInvoiceRequest
{
    public string CustomerMobile { get; set; } = string.Empty;
    public string DateFrom { get; set; } = string.Empty; // Jalali date
    public string DateTo { get; set; } = string.Empty; // Jalali date
    public string? HeaderTitle { get; set; }
    public string? WorkshopName { get; set; }
    public bool ShowPrintDate { get; set; } = true;
    public bool ShowInvoiceNumber { get; set; } = true;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
}


