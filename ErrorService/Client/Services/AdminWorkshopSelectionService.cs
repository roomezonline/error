namespace ErrorService.Client.Services;

public sealed class AdminWorkshopSelectionService
{
    private const string StorageKey = "admin_selected_workshop_id";

    private readonly LocalStorageService _storage;

    public AdminWorkshopSelectionService(LocalStorageService storage)
    {
        _storage = storage;
    }

    public int? SelectedWorkshopId { get; private set; }

    public event Action<int?>? OnChanged;

    public async Task InitializeAsync()
    {
        var raw = await _storage.GetAsync(StorageKey);
        if (string.IsNullOrWhiteSpace(raw))
        {
            SelectedWorkshopId = null;
            return;
        }

        if (int.TryParse(raw, out var id) && id > 0)
            SelectedWorkshopId = id;
        else
            SelectedWorkshopId = null;
    }

    public async Task SetSelectedWorkshopIdAsync(int? workshopId)
    {
        SelectedWorkshopId = workshopId;

        if (workshopId.HasValue)
            await _storage.SetAsync(StorageKey, workshopId.Value.ToString());
        else
            await _storage.RemoveAsync(StorageKey);

        OnChanged?.Invoke(SelectedWorkshopId);
    }
}
