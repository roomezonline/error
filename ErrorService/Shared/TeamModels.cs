using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class TeamMemberDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
}

public sealed class TeamMemberUpsertRequest
{
    [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Role { get; set; }

    [MaxLength(1000)]
    public string? Bio { get; set; }

    [MaxLength(500)]
    public string? PhotoUrl { get; set; }

    public int SortOrder { get; set; } = 0;

    public bool IsPublished { get; set; } = true;
}
