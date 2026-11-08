using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class CustomerAddressDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int? ProvinceId { get; set; }
    public int? CityId { get; set; }
    public string? ProvinceName { get; set; }
    public string? CityName { get; set; }
    public string? PostalCode { get; set; }
    public string Address { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public sealed class CustomerAddressUpsertRequest
{
    [MaxLength(100)]
    public string? Title { get; set; }

    [Required(ErrorMessage = "نام گیرنده الزامی است")]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    public int? ProvinceId { get; set; }
    public int? CityId { get; set; }

    [MaxLength(100)]
    public string? ProvinceName { get; set; }

    [MaxLength(100)]
    public string? CityName { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [Required(ErrorMessage = "آدرس پستی الزامی است")]
    [MaxLength(1000)]
    public string Address { get; set; } = string.Empty;

    public bool IsDefault { get; set; }
}

public sealed class PriceDropAlertMineDto
{
    public List<int> ActiveProductIds { get; set; } = new();
}
