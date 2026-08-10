using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared;

public sealed class PopularBrandItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public sealed class PopularBrandItemUpsertRequest
{
    [Required(ErrorMessage = "عنوان الزامی است")]
    public string Title { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
