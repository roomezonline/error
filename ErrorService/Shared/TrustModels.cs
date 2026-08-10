using System;
using System.Collections.Generic;

namespace ErrorService.Shared;

public class TestimonialDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerRole { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Rating { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class PortfolioProjectDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BeforeImageUrl { get; set; }
    public string? AfterImageUrl { get; set; }
    public string? DeviceType { get; set; }
    public string? Brand { get; set; }
    public DateTimeOffset CompletionDate { get; set; }
}
