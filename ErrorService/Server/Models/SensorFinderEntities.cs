using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class SensorFinderDevice
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<SensorFinderRecord> Records { get; set; } = new List<SensorFinderRecord>();
}

public sealed class SensorFinderRecord
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public SensorFinderDevice Device { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string SensorName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SensorType { get; set; }

    [MaxLength(100)]
    public string? SizeText { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<SensorFinderImage> Images { get; set; } = new List<SensorFinderImage>();
}

public sealed class SensorFinderImage
{
    public int Id { get; set; }

    public int RecordId { get; set; }
    public SensorFinderRecord Record { get; set; } = default!;

    [Required]
    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Title { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
