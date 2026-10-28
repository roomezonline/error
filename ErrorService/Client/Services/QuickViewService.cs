namespace ErrorService.Client.Services;

/// <summary>
/// مدیریت مودال «نمایش سریع» محصول. یک نمونهٔ سراسری در MainLayout رندر
/// می‌شود و همهٔ کارت‌ها فقط همین سرویس را صدا می‌زنند.
/// </summary>
public sealed class QuickViewService
{
    public int? ProductId { get; private set; }

    public bool IsOpen => ProductId.HasValue;

    public event Action? OnChange;

    public void Open(int productId)
    {
        if (productId <= 0) return;
        ProductId = productId;
        OnChange?.Invoke();
    }

    public void Close()
    {
        if (!ProductId.HasValue) return;
        ProductId = null;
        OnChange?.Invoke();
    }
}
