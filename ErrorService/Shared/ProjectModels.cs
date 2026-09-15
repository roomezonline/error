using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public enum ProjectFileType
{
    Document,
    Pcb,
    Map,
    CoverImage,
    Other
}

public enum SectionItemType
{
    Text,
    Image,
    File
}

public class ProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<ProjectImageDto> Images { get; set; } = new();
    public List<ProjectFileDto> Files { get; set; } = new();
    public List<ProjectSectionDto> Sections { get; set; } = new();
}

public class ProjectListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Summary { get; set; }
    public string? CoverImageUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int ImageCount { get; set; }
    public int FileCount { get; set; }
    public int SectionCount { get; set; }
}

public class ProjectImageDto
{
    public int Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
}

public class ProjectFileDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public ProjectFileType FileType { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
}

public class ProjectSectionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<ProjectSectionItemDto> Items { get; set; } = new();
}

public class ProjectSectionItemDto
{
    public int Id { get; set; }
    public SectionItemType ItemType { get; set; }
    public string? TextContent { get; set; }
    public string? MediaUrl { get; set; }
    public string? FileName { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public class ProjectUpsertRequest
{
    [Required(ErrorMessage = "نام پروژه الزامی است")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Summary { get; set; }

    public string? Description { get; set; }

    public string? CoverImageUrl { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ProjectImageAddRequest
{
    [Required(ErrorMessage = "آدرس تصویر الزامی است")]
    public string ImageUrl { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Caption { get; set; }

    public int SortOrder { get; set; }
}

public class ProjectFileAddRequest
{
    [Required(ErrorMessage = "آدرس فایل الزامی است")]
    public string FileUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "نام فایل الزامی است")]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    public ProjectFileType FileType { get; set; } = ProjectFileType.Other;

    [MaxLength(50)]
    public string? Version { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class ProjectFileUpdateRequest
{
    [MaxLength(200)]
    public string? FileName { get; set; }

    public string? FileUrl { get; set; }

    public ProjectFileType? FileType { get; set; }

    [MaxLength(50)]
    public string? Version { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

public class ProjectSectionUpsertRequest
{
    [Required(ErrorMessage = "عنوان بخش الزامی است")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

public class ProjectSectionItemAddRequest
{
    public SectionItemType ItemType { get; set; }

    public string? TextContent { get; set; }

    public string? MediaUrl { get; set; }

    [MaxLength(200)]
    public string? FileName { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }
}
