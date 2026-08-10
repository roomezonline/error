using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class StoryLike
{
    public int Id { get; set; }

    public int StoryId { get; set; }

    [Required]
    [MaxLength(50)]
    public string IpAddress { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Story Story { get; set; } = null!;
}
