using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public sealed class Story
{
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string MediaUrl { get; set; } = string.Empty;

    public bool IsVideo { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    [MaxLength(500)]
    public string? LinkUrl { get; set; }

    public int DisplayDuration { get; set; } = 5; // seconds

    public bool IsActive { get; set; } = true;

    public int ViewCount { get; set; } = 0;

    public int LikeCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<StoryLike> Likes { get; set; } = new List<StoryLike>();
}
