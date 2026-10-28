using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/products/{productId:int}/reviews")]
public sealed class ProductReviewsController : ControllerBase
{
    private const string ReviewCacheTag = "product-reviews";

    private readonly ErrorServiceDbContext _db;
    private readonly IOutputCacheStore _cacheStore;
    private readonly NotificationEventService _notificationEvents;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public ProductReviewsController(
        ErrorServiceDbContext db,
        IOutputCacheStore cacheStore,
        NotificationEventService notificationEvents,
        IWebHostEnvironment env,
        IConfiguration configuration)
    {
        _db = db;
        _cacheStore = cacheStore;
        _notificationEvents = notificationEvents;
        _env = env;
        _configuration = configuration;
    }

    private async Task EvictReviewsCacheAsync()
    {
        try { await _cacheStore.EvictByTagAsync(ReviewCacheTag, CancellationToken.None); }
        catch { }
        ErrorService.Server.Infrastructure.SeoFallbackMiddleware.InvalidateHtmlCache();
    }

    private async Task<bool> GetRequireApprovalAsync()
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        return settings?.RequireReviewApproval ?? true;
    }

    private int? GetCurrentUserId()
    {
        if (User.Identity?.IsAuthenticated != true) return null;
        var raw = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(raw, out var id) ? id : null;
    }

    private async Task<(bool ReviewsEnabled, bool MediaEnabled)> GetStoreTogglesAsync()
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        return (settings?.EnableProductReviews ?? true, settings?.EnableReviewMedia ?? false);
    }

    [Authorize]
    [HttpPost("upload")]
    [RequestSizeLimit(25_000_000)]
    public async Task<ActionResult<string>> UploadMedia(int productId, IFormFile? file, CancellationToken cancellationToken)
    {
        var (_, mediaEnabled) = await GetStoreTogglesAsync();
        if (!mediaEnabled)
            return BadRequest("پیوست عکس/ویدیو در دیدگاه غیرفعال است.");

        if (file == null || file.Length <= 0)
            return BadRequest("فایل خالی است یا دریافت نشد");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var images = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var videos = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm", ".mov" };
        if (!images.Contains(ext) && !videos.Contains(ext))
            return BadRequest("فرمت فایل مجاز نیست. فقط تصویر (jpg, png, webp, gif) یا ویدیو (mp4, webm, mov)");

        var relativeFolder = "uploads/reviews";
        var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        if (!Directory.Exists(wwwroot)) Directory.CreateDirectory(wwwroot);
        var uploadsRoot = Path.Combine(wwwroot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(uploadsRoot)) Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsRoot, fileName);
        await using (var outStream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(outStream, cancellationToken);
        }

        return Ok($"/{relativeFolder}/{fileName}");
    }

    [HttpGet]
    [OutputCache(Duration = 120, VaryByRouteValueNames = new[] { "productId" }, Tags = new[] { ReviewCacheTag })]
    public async Task<ActionResult<ProductReviewSummaryDto>> GetReviews(int productId)
    {
        var (_, mediaEnabled) = await GetStoreTogglesAsync();
        var rows = await _db.ProductReviews
            .AsNoTracking()
            .Where(x => x.ProductId == productId && x.IsApproved)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.ProductId,
                x.ParentId,
                x.FullName,
                x.Email,
                x.UserId,
                x.Rating,
                x.Content,
                x.MediaUrl,
                x.IsApproved,
                x.CreatedAt
            })
            .ToListAsync();

        var reviews = rows
            .Select(x => new ProductReviewDto
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ParentId = x.ParentId,
                FullName = x.FullName,
                Rating = x.Rating,
                Content = x.Content,
                MediaUrl = mediaEnabled ? x.MediaUrl : null,
                IsApproved = x.IsApproved,
                CreatedAtFa = x.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            })
            .ToList();

        await MarkVerifiedBuyersAsync(productId, rows.Select(x => (x.Id, x.UserId, x.Email)).ToList(), reviews);

        var avg = reviews.Any() ? Math.Round(reviews.Average(x => x.Rating), 1) : 0;
        var tree = BuildTree(reviews);

        return new ProductReviewSummaryDto
        {
            AverageRating = avg,
            TotalCount = reviews.Count,
            Reviews = tree
        };
    }

    private async Task MarkVerifiedBuyersAsync(
        int productId,
        List<(int Id, int? UserId, string Email)> rows,
        List<ProductReviewDto> reviews)
    {
        if (rows.Count == 0) return;

        var userIds = rows.Where(r => r.UserId.HasValue)
            .Select(r => r.UserId!.Value)
            .Distinct()
            .ToList();
        var emails = rows.Where(r => !r.UserId.HasValue && !string.IsNullOrWhiteSpace(r.Email))
            .Select(r => r.Email.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        var verifiedUserIds = new HashSet<int>();
        var verifiedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (userIds.Count > 0)
        {
            var found = await _db.Orders
                .AsNoTracking()
                .Where(o => o.UserId != null
                            && userIds.Contains(o.UserId.Value)
                            && (o.Status == OrderStatus.Approved || o.Status == OrderStatus.Completed)
                            && o.Items.Any(i => i.ProductId == productId))
                .Select(o => o.UserId!.Value)
                .Distinct()
                .ToListAsync();
            verifiedUserIds = found.ToHashSet();
        }

        if (emails.Count > 0)
        {
            var found = await _db.Orders
                .AsNoTracking()
                .Where(o => o.Email != null
                            && emails.Contains(o.Email.ToLower())
                            && (o.Status == OrderStatus.Approved || o.Status == OrderStatus.Completed)
                            && o.Items.Any(i => i.ProductId == productId))
                .Select(o => o.Email!.ToLower())
                .Distinct()
                .ToListAsync();
            verifiedEmails = new HashSet<string>(found, StringComparer.OrdinalIgnoreCase);
        }

        var byId = reviews.ToDictionary(x => x.Id);
        foreach (var row in rows)
        {
            var verified = (row.UserId.HasValue && verifiedUserIds.Contains(row.UserId.Value))
                           || (!string.IsNullOrWhiteSpace(row.Email)
                               && verifiedEmails.Contains(row.Email.Trim().ToLowerInvariant()));
            if (verified && byId.TryGetValue(row.Id, out var dto))
                dto.IsVerifiedBuyer = true;
        }
    }

    [HttpPost]
    [EnableRateLimiting("commerce")]
    public async Task<IActionResult> SubmitReview(int productId, [FromBody] ProductReviewRequest request)
    {
        var (reviewsEnabled, mediaEnabled) = await GetStoreTogglesAsync();
        if (!reviewsEnabled)
            return BadRequest("ثبت دیدگاه در حال حاضر غیرفعال است.");

        var productTitle = await _db.Products
            .Where(x => x.Id == productId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync();
        if (productTitle == null) return NotFound("محصول یافت نشد");

        if (request.ParentId.HasValue)
        {
            var parentExists = await _db.ProductReviews.AnyAsync(x => x.Id == request.ParentId && x.ProductId == productId);
            if (!parentExists) return BadRequest("نظر والد یافت نشد");
        }

        var requireApproval = await GetRequireApprovalAsync();
        var isAdminSubmitter = User.Identity?.IsAuthenticated == true
            && (User.IsInRole("super_admin") || User.HasClaim("perm", "admin.settings.manage"));

        var review = new ProductReview
        {
            ProductId = productId,
            UserId = GetCurrentUserId(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Rating = request.Rating,
            ParentId = request.ParentId,
            Content = request.Content.Trim(),
            MediaUrl = mediaEnabled && !string.IsNullOrWhiteSpace(request.MediaUrl)
                ? request.MediaUrl.Trim()
                : null,
            CreatedAt = DateTimeOffset.UtcNow,
            IsApproved = isAdminSubmitter || !requireApproval,
            IsAdminRead = isAdminSubmitter && request.ParentId.HasValue
        };
        _db.ProductReviews.Add(review);
        await _db.SaveChangesAsync();

        if (!review.IsApproved)
        {
            await _notificationEvents.NotifyAsync(
                eventType: "review.created",
                values: new Dictionary<string, string?>
                {
                    ["CustomerName"] = review.FullName,
                    ["ProductTitle"] = productTitle
                },
                idempotencyKey: $"review.created:{review.Id}",
                fallbackSeverity: NotificationSeverity.Info,
                fallbackTitle: "نظر جدید ثبت شد",
                fallbackBody: $"{review.FullName} برای محصول «{productTitle}» نظر ثبت کرده است و در انتظار تأیید است.",
                fallbackUrl: "/admin/product-reviews");
            await _db.SaveChangesAsync();
        }

        await EvictReviewsCacheAsync();

        var message = requireApproval && !isAdminSubmitter
            ? "نظر شما با موفقیت ثبت شد و پس از تأیید نمایش داده خواهد شد."
            : "نظر شما با موفقیت ثبت شد.";
        return Ok(new { message });
    }

    [HttpGet("moderation-settings")]
    public async Task<ActionResult<ReviewModerationSettingsDto>> GetModerationSettings()
    {
        var requireApproval = await GetRequireApprovalAsync();
        return new ReviewModerationSettingsDto { RequireApproval = requireApproval };
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpPut("moderation-settings")]
    public async Task<IActionResult> SaveModerationSettings([FromBody] ReviewModerationSettingsDto dto)
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SiteSettings();
            _db.SiteSettings.Add(settings);
        }
        settings.RequireReviewApproval = dto.RequireApproval;
        await _db.SaveChangesAsync();
        return Ok(new { requireApproval = settings.RequireReviewApproval });
    }

    [Authorize]
    [HttpGet("all")]
    public async Task<ActionResult<List<ProductReviewAdminDto>>> GetAllReviews()
    {
        if (!User.IsInRole("super_admin")
            && !User.HasClaim("perm", "admin.settings.manage")
            && !User.HasClaim("perm", "admin.news.view"))
        {
            return Forbid();
        }

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
                MediaUrl = x.MediaUrl,
                IsApproved = x.IsApproved,
                IsAdminRead = x.IsAdminRead,
                CreatedAtFa = x.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            })
            .ToListAsync();
    }

    [Authorize]
    [HttpPost("read-admin-all")]
    public async Task<IActionResult> MarkAllAsReadByAdmin()
    {
        if (!User.IsInRole("super_admin")
            && !User.HasClaim("perm", "admin.settings.manage")
            && !User.HasClaim("perm", "admin.news.view"))
        {
            return Forbid();
        }

        var unread = await _db.ProductReviews.Where(x => !x.IsAdminRead).ToListAsync();
        if (unread.Count == 0) return Ok(new { updated = 0 });
        foreach (var r in unread) r.IsAdminRead = true;
        await _db.SaveChangesAsync();
        return Ok(new { updated = unread.Count });
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpPost("{id:int}/read-admin")]
    public async Task<IActionResult> MarkAsReadByAdmin(int productId, int id)
    {
        var review = await _db.ProductReviews.FirstOrDefaultAsync(x => x.Id == id && x.ProductId == productId);
        if (review == null) return NotFound();
        if (!review.IsAdminRead)
        {
            review.IsAdminRead = true;
            await _db.SaveChangesAsync();
        }
        return Ok(new { isRead = review.IsAdminRead });
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpPut("{id:int}/approve")]
    public async Task<IActionResult> ToggleApprove(int productId, int id)
    {
        var review = await _db.ProductReviews.FirstOrDefaultAsync(x => x.Id == id && x.ProductId == productId);
        if (review == null) return NotFound();
        review.IsApproved = !review.IsApproved;
        review.IsAdminRead = true;
        await _db.SaveChangesAsync();
        await EvictReviewsCacheAsync();
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
        await EvictReviewsCacheAsync();
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
