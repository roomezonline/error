using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class ProvinceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ProvinceId { get; set; }
}

public sealed class WorkshopDto
{
    public int Id { get; set; }
    public string WorkshopName { get; set; } = string.Empty;
    public string OwnerFullName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public int ProvinceId { get; set; }
    public string? ProvinceName { get; set; }
    public int CityId { get; set; }
    public string? CityName { get; set; }
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? ImageUrl { get; set; }
    public bool ShowInCustomersRow { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MaxMonitoringConnections { get; set; } = 2;
}

public sealed class WorkshopUpsertRequest
{
    [Required(ErrorMessage = "نام کارگاه الزامی است")]
    [MaxLength(200)]
    public string WorkshopName { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام صاحب امتیاز الزامی است")]
    [MaxLength(200)]
    public string OwnerFullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره موبایل الزامی است")]
    [MaxLength(15)]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "شماره موبایل معتبر نیست")]
    public string MobileNumber { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب استان الزامی است")]
    public int ProvinceId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "انتخاب شهر الزامی است")]
    public int CityId { get; set; }

    [Required(ErrorMessage = "آدرس الزامی است")]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool ShowInCustomersRow { get; set; } = false;

    public bool IsActive { get; set; } = true;

    [Range(1, 100, ErrorMessage = "تعداد مجاز مانیتورینگ باید بین ۱ تا ۱۰۰ باشد")]
    public int MaxMonitoringConnections { get; set; } = 2;
}

public sealed class UpdateBadPayerRequest
{
    public bool IsBadPayer { get; set; }
}
