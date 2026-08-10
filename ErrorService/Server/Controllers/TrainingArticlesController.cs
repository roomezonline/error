using ErrorService.Server.Data;
using ErrorService.Server.Infrastructure;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TrainingArticlesController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;

    public TrainingArticlesController(ErrorServiceDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    [OutputCache(Duration = 300, VaryByQueryKeys = new[] { "onlyPublished" })]
    public async Task<ActionResult<List<TrainingArticleDto>>> GetAll(bool onlyPublished = false)
    {
        var query = _db.TrainingArticles.AsQueryable();
        if (onlyPublished) query = query.Where(x => x.IsPublished);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new TrainingArticleDto
            {
                Id = x.Id,
                Title = x.Title,
                Slug = x.Slug,
                Summary = x.Summary,
                CoverImageUrl = x.CoverImageUrl,
                IsPublished = x.IsPublished,
                IsPremium = x.IsPremium,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();
    }

    [HttpGet("slug/{slug}")]
    [OutputCache(Duration = 300, VaryByQueryKeys = new[] { "onlyApprovedComments" })]
    public async Task<ActionResult<TrainingArticleDto>> GetBySlug(string slug, bool onlyApprovedComments = true)
    {
        var query = _db.TrainingArticles
            .Include(x => x.Blocks.OrderBy(b => b.SortOrder))
            .AsQueryable();

        var article = await query.FirstOrDefaultAsync(x => x.Slug == slug);
        if (article == null) return NotFound();

        return await BuildArticleDto(article, onlyApprovedComments);
    }

    private async Task<TrainingArticleDto> BuildArticleDto(TrainingArticle article, bool onlyApprovedComments)
    {
        var commentQuery = _db.TrainingArticleComments
            .Where(c => c.ArticleId == article.Id);

        if (onlyApprovedComments)
            commentQuery = commentQuery.Where(c => c.IsApproved);

        var comments = await commentQuery
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new TrainingArticleCommentDto
            {
                Id = c.Id,
                ParentId = c.ParentId,
                FullName = c.FullName,
                Content = c.Content,
                IsApproved = c.IsApproved,
                CreatedAt = c.CreatedAt,
                Rating = c.Rating,
                CreatedAtFa = c.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            })
            .ToListAsync();

        var withRating = comments.Where(c => c.Rating.HasValue).ToList();
        var avg = withRating.Any() ? Math.Round(withRating.Average(c => c.Rating!.Value), 1) : 0;
        var tree = BuildTree(comments);

        return new TrainingArticleDto
        {
            Id = article.Id,
            Title = article.Title,
            Slug = article.Slug,
            Summary = article.Summary,
            CoverImageUrl = article.CoverImageUrl,
            IsPublished = article.IsPublished,
            IsPremium = article.IsPremium,
            CreatedAt = article.CreatedAt,
            AverageRating = avg,
            Blocks = article.Blocks.Select(b => new TrainingArticleBlockDto
            {
                Id = b.Id,
                BlockType = (TrainingBlockTypeDto)b.BlockType,
                Title = b.Title,
                Content = b.Content,
                Url = b.Url,
                ThumbnailUrl = b.ThumbnailUrl,
                SortOrder = b.SortOrder
            }).ToList(),
            Comments = tree
        };
    }

    [HttpPost("{id:int}/comments")]
    public async Task<IActionResult> PostComment(int id, [FromBody] TrainingArticleCommentRequest request)
    {
        var articleExists = await _db.TrainingArticles.AnyAsync(x => x.Id == id);
        if (!articleExists) return NotFound();

        if (request.ParentId.HasValue)
        {
            var parentExists = await _db.TrainingArticleComments.AnyAsync(x => x.Id == request.ParentId && x.ArticleId == id);
            if (!parentExists) return BadRequest("نظر والد یافت نشد");
        }

        _db.TrainingArticleComments.Add(new TrainingArticleComment
        {
            ArticleId = id,
            FullName = request.FullName.Trim(),
            Email = request.Email?.Trim().ToLowerInvariant(),
            Content = request.Content.Trim(),
            Rating = request.Rating,
            ParentId = request.ParentId,
            IsApproved = User.Identity?.IsAuthenticated == true
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "نظر شما با موفقیت ثبت شد و پس از تأیید نمایش داده خواهد شد." });
    }

    [Authorize(Policy = "perm:admin.academy.comments.manage")]
    [HttpPut("comments/{id:int}/approve")]
    public async Task<IActionResult> ToggleApproveComment(int id)
    {
        var comment = await _db.TrainingArticleComments.FindAsync(id);
        if (comment == null) return NotFound();
        comment.IsApproved = !comment.IsApproved;
        await _db.SaveChangesAsync();
        return Ok(new { isApproved = comment.IsApproved });
    }

    [Authorize(Policy = "perm:admin.academy.comments.manage")]
    [HttpPut("comments/{id:int}")]
    public async Task<IActionResult> EditComment(int id, [FromBody] CommentEditRequest request)
    {
        var comment = await _db.TrainingArticleComments.FindAsync(id);
        if (comment == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(request.FullName)) comment.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Content)) comment.Content = request.Content.Trim();
        if (request.Rating.HasValue) comment.Rating = request.Rating;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.academy.comments.manage")]
    [HttpDelete("comments/{id:int}")]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var comment = await _db.TrainingArticleComments.FindAsync(id);
        if (comment == null) return NotFound();
        _db.TrainingArticleComments.Remove(comment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.academy.comments.manage")]
    [HttpGet("all-comments")]
    public async Task<ActionResult<List<AcademyCommentAdminDto>>> GetAllComments()
    {
        return await _db.TrainingArticleComments
            .Include(c => c.Article)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new AcademyCommentAdminDto
            {
                Id = c.Id,
                ArticleId = c.ArticleId,
                ParentId = c.ParentId,
                FullName = c.FullName,
                Content = c.Content,
                IsApproved = c.IsApproved,
                CreatedAt = c.CreatedAt,
                CreatedAtFa = c.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm"),
                ArticleTitle = c.Article.Title
            })
            .ToListAsync();
    }

    [Authorize(Policy = "perm:admin.academy.articles.manage")]
    [HttpPost]
    public async Task<ActionResult<TrainingArticleDto>> Create(TrainingArticleUpsertRequest request)
    {
        var article = new TrainingArticle
        {
            Title = request.Title,
            Slug = await SlugService.ResolveUniqueAsync(
                _db.TrainingArticles.Where(a => a.Slug != null).Select(a => a.Slug!),
                request.Title),
            Summary = request.Summary,
            CoverImageUrl = request.CoverImageUrl,
            IsPublished = request.IsPublished,
            IsPremium = request.IsPremium
        };

        _db.TrainingArticles.Add(article);
        await _db.SaveChangesAsync();
        return Ok(new TrainingArticleDto { Id = article.Id, Title = article.Title, Slug = article.Slug });
    }

    [Authorize(Policy = "perm:admin.academy.articles.manage")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TrainingArticleUpsertRequest request)
    {
        var article = await _db.TrainingArticles.FindAsync(id);
        if (article == null) return NotFound();

        var oldTitle = article.Title;
        article.Title = request.Title;
        article.Slug = await SlugService.ResolveForUpdateAsync(
            _db.TrainingArticles.Where(a => a.Slug != null && a.Id != id).Select(a => a.Slug!),
            oldTitle, article.Slug, request.Title);
        article.Summary = request.Summary;
        FileCleanupHelper.DeleteOldFileIfChanged(article.CoverImageUrl, request.CoverImageUrl, _env);
        article.CoverImageUrl = request.CoverImageUrl;
        article.IsPublished = request.IsPublished;
        article.IsPremium = request.IsPremium;
        article.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.academy.articles.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var article = await _db.TrainingArticles.FindAsync(id);
        if (article == null) return NotFound();
        _db.TrainingArticles.Remove(article);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    #region Blocks

    [Authorize(Policy = "perm:admin.academy.articles.content.manage")]
    [HttpPost("{articleId:int}/blocks")]
    public async Task<ActionResult<TrainingArticleBlockDto>> AddBlock(int articleId, TrainingArticleBlockDto blockDto)
    {
        var article = await _db.TrainingArticles.AnyAsync(x => x.Id == articleId);
        if (!article) return NotFound();

        var block = new TrainingArticleBlock
        {
            ArticleId = articleId,
            BlockType = (TrainingBlockType)blockDto.BlockType,
            Title = blockDto.Title,
            Content = blockDto.Content,
            Url = blockDto.Url,
            ThumbnailUrl = blockDto.ThumbnailUrl,
            SortOrder = blockDto.SortOrder
        };

        _db.TrainingArticleBlocks.Add(block);
        await _db.SaveChangesAsync();

        blockDto.Id = block.Id;
        return Ok(blockDto);
    }

    [Authorize(Policy = "perm:admin.academy.articles.content.manage")]
    [HttpPut("blocks/{id:int}")]
    public async Task<IActionResult> UpdateBlock(int id, TrainingArticleBlockDto blockDto)
    {
        var block = await _db.TrainingArticleBlocks.FindAsync(id);
        if (block == null) return NotFound();

        block.BlockType = (TrainingBlockType)blockDto.BlockType;
        block.Title = blockDto.Title;
        block.Content = blockDto.Content;
        FileCleanupHelper.DeleteOldFileIfChanged(block.Url, blockDto.Url, _env);
        block.Url = blockDto.Url;
        FileCleanupHelper.DeleteOldFileIfChanged(block.ThumbnailUrl, blockDto.ThumbnailUrl, _env);
        block.ThumbnailUrl = blockDto.ThumbnailUrl;
        block.SortOrder = blockDto.SortOrder;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.academy.articles.content.manage")]
    [HttpDelete("blocks/{id:int}")]
    public async Task<IActionResult> DeleteBlock(int id)
    {
        var block = await _db.TrainingArticleBlocks.FindAsync(id);
        if (block == null) return NotFound();
        _db.TrainingArticleBlocks.Remove(block);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    #endregion

    private static List<TrainingArticleCommentDto> BuildTree(List<TrainingArticleCommentDto> flat)
    {
        var lookup = flat.ToLookup(x => x.ParentId);
        var roots = lookup[null].ToList();
        foreach (var root in roots)
            AttachReplies(root, lookup);
        return roots;
    }

    private static void AttachReplies(TrainingArticleCommentDto parent, ILookup<int?, TrainingArticleCommentDto> lookup)
    {
        var children = lookup[parent.Id].ToList();
        foreach (var child in children)
            AttachReplies(child, lookup);
        parent.Replies = children;
    }
}
