using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class Province
{
    public int Id { get; set; }
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public ICollection<City> Cities { get; set; } = new List<City>();
}

public sealed class City
{
    public int Id { get; set; }
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    public int ProvinceId { get; set; }
    public Province Province { get; set; } = default!;
}

public sealed class Workshop
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string WorkshopName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string OwnerFullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(15)]
    public string MobileNumber { get; set; } = string.Empty;

    public int ProvinceId { get; set; }
    public Province Province { get; set; } = default!;

    public int CityId { get; set; }
    public City City { get; set; } = default!;

    [Required]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool ShowInCustomersRow { get; set; } = false;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Range(1, 100)]
    public int MaxMonitoringConnections { get; set; } = 2;
}
