using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class DeviceType
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ICollection<DeviceBrand> Brands { get; set; } = new List<DeviceBrand>();
}

public sealed class DeviceBrand
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int DeviceTypeId { get; set; }

    public DeviceType DeviceType { get; set; } = default!;

    public int SortOrder { get; set; }
}
