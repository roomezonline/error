using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class Permission
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string TitleFa { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? DescriptionFa { get; set; }

    [MaxLength(200)]
    public string? GroupFa { get; set; }
}

public sealed class Role
{
    public int Id { get; set; }

    public int? WorkshopId { get; set; }
    public Workshop? Workshop { get; set; }

    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string TitleFa { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? DescriptionFa { get; set; }

    public bool IsSystem { get; set; }

    public int Rank { get; set; } = 0;
}

public sealed class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = default!;

    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
}

public sealed class AppUserRole
{
    public int UserId { get; set; }
    public AppUser User { get; set; } = default!;

    public int RoleId { get; set; }
    public Role Role { get; set; } = default!;
}

public sealed class WorkshopUser
{
    public int Id { get; set; }

    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public bool IsTechnician { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WorkshopUserRole
{
    public int WorkshopUserId { get; set; }
    public WorkshopUser WorkshopUser { get; set; } = default!;

    public int RoleId { get; set; }
    public Role Role { get; set; } = default!;
}
