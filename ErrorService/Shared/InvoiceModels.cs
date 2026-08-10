namespace ErrorService.Shared;

public sealed class CatalogItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool SendSms { get; set; }

    public int? DeviceTypeId { get; set; }
}

public sealed class CatalogItemCreateRequest
{
    public string Title { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool SendSms { get; set; } = true;

    public int? DeviceTypeId { get; set; }
}
