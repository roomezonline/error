using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class SensorFinderDeviceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class SensorFinderImageDto
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public int SortOrder { get; set; }
}

public sealed class SensorFinderRecordDto
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;

    public string SensorName { get; set; } = string.Empty;
    public string? SensorType { get; set; }
    public string? SizeText { get; set; }
    public string? Notes { get; set; }

    public List<SensorFinderImageDto> Images { get; set; } = new();
}

public sealed class SensorFinderDeviceUpsertRequest
{
    [Required(ErrorMessage = "نام دستگاه الزامی است")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;
}

public sealed class SensorFinderRecordUpsertRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "انتخاب دستگاه الزامی است")]
    public int DeviceId { get; set; }

    [Required(ErrorMessage = "نام سنسور الزامی است")]
    [MaxLength(200)]
    public string SensorName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SensorType { get; set; }

    [MaxLength(100)]
    public string? SizeText { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public sealed class SensorFinderSearchRequest
{
    public int DeviceId { get; set; }
}
