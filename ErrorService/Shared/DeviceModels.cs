using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class DeviceTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class DeviceBrandDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DeviceTypeId { get; set; }
    public string? DeviceTypeName { get; set; }
    public int SortOrder { get; set; }
}

public sealed class DeviceBrandUpsertRequest
{
    [Required(ErrorMessage = "نام برند الزامی است")]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب نوع دستگاه الزامی است")]
    public int DeviceTypeId { get; set; }

    public int SortOrder { get; set; }
}
