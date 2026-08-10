using System.ComponentModel.DataAnnotations;

namespace ErrorService.Server.Models;

public class Testimonial
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? CustomerRole { get; set; } // e.g., تعمیرکار، خانه دار

    [Required]
    [MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    public int Rating { get; set; } = 5;

    public bool IsApproved { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class PortfolioProject
{
    public int Id { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? BeforeImageUrl { get; set; }

    [MaxLength(500)]
    public string? AfterImageUrl { get; set; }

    [MaxLength(200)]
    public string? DeviceType { get; set; }

    [MaxLength(200)]
    public string? Brand { get; set; }

    public DateTimeOffset CompletionDate { get; set; } = DateTimeOffset.UtcNow;

    public bool IsPublished { get; set; } = true;
}
