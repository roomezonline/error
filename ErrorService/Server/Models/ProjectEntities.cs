using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErrorService.Shared;

namespace ErrorService.Server.Models;

public class Project
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Slug { get; set; }

    [MaxLength(500)]
    public string? Summary { get; set; }

    public string? Description { get; set; }

    [MaxLength(500)]
    public string? CoverImageUrl { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ProjectImage> Images { get; set; } = new List<ProjectImage>();

    public ICollection<ProjectFile> Files { get; set; } = new List<ProjectFile>();

    public ICollection<ProjectSection> Sections { get; set; } = new List<ProjectSection>();
}

public class ProjectImage
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = default!;

    [Required]
    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Caption { get; set; }

    public int SortOrder { get; set; }
}

public class ProjectFile
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FileUrl { get; set; } = string.Empty;

    public ProjectFileType FileType { get; set; } = ProjectFileType.Other;

    [MaxLength(50)]
    public string? Version { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ProjectSection
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project Project { get; set; } = default!;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ICollection<ProjectSectionItem> Items { get; set; } = new List<ProjectSectionItem>();
}

public class ProjectSectionItem
{
    public int Id { get; set; }

    public int SectionId { get; set; }
    public ProjectSection Section { get; set; } = default!;

    public SectionItemType ItemType { get; set; }

    public string? TextContent { get; set; }

    [MaxLength(500)]
    public string? MediaUrl { get; set; }

    [MaxLength(200)]
    public string? FileName { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }
}
