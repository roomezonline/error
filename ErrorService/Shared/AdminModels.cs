namespace ErrorService.Shared;

public sealed class ProductReviewAdminDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductTitle { get; set; } = "";
    public int? ParentId { get; set; }
    public string FullName { get; set; } = "";
    public int Rating { get; set; }
    public string Content { get; set; } = "";
    public bool IsApproved { get; set; }
    public string CreatedAtFa { get; set; } = "";
}

public sealed class NewsCommentAdminDto
{
    public int Id { get; set; }
    public int NewsId { get; set; }
    public string NewsTitle { get; set; } = "";
    public int? ParentId { get; set; }
    public string FullName { get; set; } = "";
    public string Content { get; set; } = "";
    public int? Rating { get; set; }
    public bool IsApproved { get; set; }
    public string CreatedAtFa { get; set; } = "";
}

public sealed class CommentEditRequest
{
    public string? FullName { get; set; }
    public string? Content { get; set; }
    public int? Rating { get; set; }
}

public sealed class AcademyCommentAdminDto
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public int? ParentId { get; set; }
    public string FullName { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsApproved { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string ArticleTitle { get; set; } = "";
    public string CreatedAtFa { get; set; } = "";
}
