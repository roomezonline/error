using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/products/{productId:int}/reviews")]
public sealed class ProductReviewsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public ProductReviewsController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [OutputCache(Duration = 120, VaryByRouteValueNames = new[] { "productId" })]
    public async Task<ActionResult<ProductReviewSummaryDto>> GetReviews(int productId)
    {
        var reviews = await _db.ProductReviews
            .Where(x => x.ProductId == productId && x.IsApproved)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProductReviewDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ParentId = x.ParentId,
                FullName = x.FullName,
                Rating = x.Rating,
                Content = x.Content,
                IsApproved = x.IsApproved,
                CreatedAtFa = x.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            })
            .ToListAsync();

        var avg = reviews.Any() ? Math.Round(reviews.Average(x => x.Rating), 1) : 0;
        var tree = BuildTree(reviews);

        return new ProductReviewSummaryDto
        {
            AverageRating = avg,
            TotalCount = reviews.Count,
            Reviews = tree
        };
    }

    [HttpPost]
    public async Task<IActionResult> SubmitReview(int productId, [FromBody] ProductReviewRequest request)
    {
        var product = await _db.Products.AnyAsync(x => x.Id == productId);
        if (!product) return NotFound("محصول یافت نشد");

        if (request.ParentId.HasValue)
        {
            var parentExists = await _db.ProductReviews.AnyAsync(x => x.Id == request.ParentId && x.ProductId == productId);
            if (!parentExists) return BadRequest("نظر والد یافت نشد");
        }

        _db.ProductReviews.Add(new ProductReview
        {
            ProductId = productId,
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Rating = request.Rating,
            ParentId = request.ParentId,
            Content = request.Content.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            IsApproved = User.Identity?.IsAuthenticated == true
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "نظر شما با موفقیت ثبت شد و پس از تأیید نمایش داده خواهد شد." });
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpGet("all")]
    public async Task<ActionResult<List<ProductReviewAdminDto>>> GetAllReviews()
    {
        return await _db.ProductReviews
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProductReviewAdminDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductTitle = x.Product!.Name,
                ParentId = x.ParentId,
                FullName = x.FullName,
                Rating = x.Rating,
                Content = x.Content,
                IsApproved = x.IsApproved,
                CreatedAtFa = x.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            })
            .ToListAsync();
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpPut("{id:int}/approve")]
    public async Task<IActionResult> ToggleApprove(int productId, int id)
    {
        var review = await _db.ProductReviews.FirstOrDefaultAsync(x => x.Id == id && x.ProductId == productId);
        if (review == null) return NotFound();
        review.IsApproved = !review.IsApproved;
        await _db.SaveChangesAsync();
        return Ok(new { isApproved = review.IsApproved });
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int productId, int id, [FromBody] CommentEditRequest request)
    {
        var review = await _db.ProductReviews.FirstOrDefaultAsync(x => x.Id == id && x.ProductId == productId);
        if (review == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(request.FullName)) review.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Content)) review.Content = request.Content.Trim();
        if (request.Rating.HasValue) review.Rating = request.Rating.Value;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int productId, int id)
    {
        var review = await _db.ProductReviews.FirstOrDefaultAsync(x => x.Id == id && x.ProductId == productId);
        if (review == null) return NotFound();
        _db.ProductReviews.Remove(review);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static List<ProductReviewDto> BuildTree(List<ProductReviewDto> flat)
    {
        var lookup = flat.ToLookup(x => x.ParentId);
        var roots = lookup[null].ToList();
        foreach (var root in roots)
            AttachReplies(root, lookup);
        return roots;
    }

    private static void AttachReplies(ProductReviewDto parent, ILookup<int?, ProductReviewDto> lookup)
    {
        var children = lookup[parent.Id].ToList();
        foreach (var child in children)
            AttachReplies(child, lookup);
        parent.Replies = children;
    }
}
