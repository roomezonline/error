namespace ErrorService.Shared;

public class SearchResultItemDto
{
    public string Type { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Url { get; set; } = string.Empty;
}

public class SearchResponseDto
{
    public List<SearchResultItemDto> Products { get; set; } = new();
    public List<SearchResultItemDto> News { get; set; } = new();
    public List<SearchResultItemDto> ErrorCodes { get; set; } = new();
    public List<SearchResultItemDto> Courses { get; set; } = new();
}
