using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/news/{newsId:int}/comments")]
public sealed class NewsCommentsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public NewsCommentsController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [OutputCache(Duration = 120, VaryByRouteValueNames = new[] { "newsId" })]
    public async Task<ActionResult<CommentSummaryDto>> GetComments(int newsId)
    {
        var comments = await _db.NewsComments
            .Where(x => x.NewsId == newsId && x.IsApproved)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CommentDto
            {
                Id = x.Id,
                OwnerId = x.NewsId,
                ParentId = x.ParentId,
                FullName = x.FullName,
                Content = x.Content,
                Rating = x.Rating,
                CreatedAtFa = x.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            })
            .ToListAsync();

        var withRating = comments.Where(c => c.Rating.HasValue).ToList();
        var avg = withRating.Any() ? Math.Round(withRating.Average(c => c.Rating!.Value), 1) : 0;

        var tree = BuildTree(comments);

        return new CommentSummaryDto
        {
            AverageRating = avg,
            TotalCount = comments.Count,
            Comments = tree
        };
    }

    [HttpPost]
    public async Task<IActionResult> PostComment(int newsId, [FromBody] CommentRequest request)
    {
        var exists = await _db.News.AnyAsync(x => x.Id == newsId);
        if (!exists) return NotFound("خبر یافت نشد");

        if (request.Rating.HasValue && (request.Rating.Value < 1 || request.Rating.Value > 5))
            return BadRequest("امتیاز باید بین ۱ تا ۵ باشد");

        if (request.ParentId.HasValue)
        {
            var parentExists = await _db.NewsComments.AnyAsync(x => x.Id == request.ParentId && x.NewsId == newsId);
            if (!parentExists) return BadRequest("نظر والد یافت نشد");
        }

        _db.NewsComments.Add(new NewsComment
        {
            NewsId = newsId,
            FullName = request.FullName.Trim(),
            Email = request.Email?.Trim().ToLowerInvariant(),
            Rating = request.Rating,
            ParentId = request.ParentId,
            Content = request.Content.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            IsApproved = User.Identity?.IsAuthenticated == true
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "نظر شما با موفقیت ثبت شد و پس از تأیید نمایش داده خواهد شد." });
    }

    [Authorize(Policy = "perm:admin.news.comments.manage")]
    [HttpGet("all")]
    public async Task<ActionResult<List<NewsCommentAdminDto>>> GetAllComments()
    {
        return await _db.NewsComments
            .Include(x => x.News)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new NewsCommentAdminDto
            {
                Id = x.Id,
                NewsId = x.NewsId,
                NewsTitle = x.News.Title,
                ParentId = x.ParentId,
                FullName = x.FullName,
                Content = x.Content,
                Rating = x.Rating,
                IsApproved = x.IsApproved,
                CreatedAtFa = x.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            })
            .ToListAsync();
    }

    [Authorize(Policy = "perm:admin.news.comments.manage")]
    [HttpPut("{id:int}/approve")]
    public async Task<IActionResult> ToggleApprove(int newsId, int id)
    {
        var comment = await _db.NewsComments.FirstOrDefaultAsync(x => x.Id == id && x.NewsId == newsId);
        if (comment == null) return NotFound();
        comment.IsApproved = !comment.IsApproved;
        await _db.SaveChangesAsync();
        return Ok(new { isApproved = comment.IsApproved });
    }

    [Authorize(Policy = "perm:admin.news.comments.manage")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int newsId, int id, [FromBody] CommentEditRequest request)
    {
        var comment = await _db.NewsComments.FirstOrDefaultAsync(x => x.Id == id && x.NewsId == newsId);
        if (comment == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(request.FullName)) comment.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Content)) comment.Content = request.Content.Trim();
        if (request.Rating.HasValue) comment.Rating = request.Rating;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.news.comments.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int newsId, int id)
    {
        var comment = await _db.NewsComments.FirstOrDefaultAsync(x => x.Id == id && x.NewsId == newsId);
        if (comment == null) return NotFound();
        _db.NewsComments.Remove(comment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static List<CommentDto> BuildTree(List<CommentDto> flat)
    {
        var lookup = flat.ToLookup(x => x.ParentId);
        var roots = lookup[null].ToList();
        foreach (var root in roots)
            AttachReplies(root, lookup);
        return roots;
    }

    private static void AttachReplies(CommentDto parent, ILookup<int?, CommentDto> lookup)
    {
        var children = lookup[parent.Id].ToList();
        foreach (var child in children)
            AttachReplies(child, lookup);
        parent.Replies = children;
    }
}
