namespace ErrorService.Shared;

/// <summary>گزارش مالی/حسابداری فروشگاه برای یک بازهٔ تاریخی (شمسی).</summary>
public sealed class ShopAccountingDto
{
    public string DateFrom { get; set; } = "";
    public string DateTo { get; set; } = "";
    public string Preset { get; set; } = "custom";
    public int RangeDays { get; set; }
    public bool IncludeDaily { get; set; }

    public int OrdersCount { get; set; }
    public int ActiveCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int CompletedCount { get; set; }
    public int PendingCount { get; set; }
    public int ReceiptCount { get; set; }
    public int CancelledCount { get; set; }
    public int RejectedCount { get; set; }

    public int UnitsSold { get; set; }
    public int UnitsCancelled { get; set; }

    public decimal GrossAmount { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal SellAmount { get; set; }
    public decimal ConfirmedAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal ReceiptAmount { get; set; }
    public decimal CancelledAmount { get; set; }

    public int ReturnsCount { get; set; }
    public int RefundedCount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal AvgOrderAmount { get; set; }

    public List<ShopAccountingDayDto> Daily { get; set; } = new();
    public List<ShopAccountingStatusDto> Statuses { get; set; } = new();
}

public sealed class ShopAccountingDayDto
{
    public string Date { get; set; } = "";
    public string JalaliDate { get; set; } = "";
    public int OrdersCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int Units { get; set; }
    public decimal Amount { get; set; }
    public decimal ConfirmedAmount { get; set; }
    public decimal CancelledAmount { get; set; }
    public decimal RefundAmount { get; set; }
}

public sealed class ShopAccountingStatusDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>جزئیات یک روز مشخص: سفارش‌ها، اقلام، قیمت‌ها و مرجوعی‌های همان روز.</summary>
public sealed class ShopAccountingDayDetailDto
{
    public string Date { get; set; } = "";
    public string JalaliDate { get; set; } = "";

    public int OrdersCount { get; set; }
    public int ActiveCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int CancelledCount { get; set; }
    public int Units { get; set; }

    public decimal Amount { get; set; }
    public decimal ConfirmedAmount { get; set; }
    public decimal CancelledAmount { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal RefundAmount { get; set; }

    public List<ShopAccountingOrderDto> Orders { get; set; } = new();
    public List<ShopAccountingReturnDto> Returns { get; set; } = new();
}

public sealed class ShopAccountingOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Time { get; set; } = "";
    public string StatusKey { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public bool IsCancelled { get; set; }
    public bool IsConfirmed { get; set; }
    public string? CouponCode { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Payable { get; set; }

    public List<ShopAccountingItemDto> Items { get; set; } = new();
}

public sealed class ShopAccountingItemDto
{
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

public sealed class ShopAccountingReturnDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public string Time { get; set; } = "";
    public string Reason { get; set; } = "";
    public bool IsRefunded { get; set; }
    public decimal RefundAmount { get; set; }
}
