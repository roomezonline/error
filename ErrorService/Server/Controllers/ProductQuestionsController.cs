using System.Security.Claims;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/products/{productId:int}/questions")]
public sealed class ProductQuestionsController : ControllerBase
{
    private const string CacheTag = "product-questions";

    private readonly ErrorServiceDbContext _db;
    private readonly Microsoft.AspNetCore.OutputCaching.IOutputCacheStore _cacheStore;

    public ProductQuestionsController(ErrorServiceDbContext db, Microsoft.AspNetCore.OutputCaching.IOutputCacheStore cacheStore)
    {
        _db = db;
        _cacheStore = cacheStore;
    }

    private async Task<bool> IsQnaEnabledAsync()
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        return settings?.EnableProductQna ?? false;
    }

    private async Task EvictCacheAsync()
    {
        try { await _cacheStore.EvictByTagAsync(CacheTag, CancellationToken.None); } catch { }
    }

    private int? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("user_id")
                  ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }

    [HttpGet]
    [OutputCache(Duration = 60, VaryByRouteValueNames = new[] { "productId" }, Tags = new[] { CacheTag })]
    public async Task<ActionResult<List<ProductQuestionDto>>> GetQuestions(int productId)
    {
        if (!await IsQnaEnabledAsync()) return Ok(new List<ProductQuestionDto>());

        var me = CurrentUserId();
        var adminIds = await AdminUserIdsAsync();

        var list = await _db.ProductQuestions.AsNoTracking()
            .Where(q => q.ProductId == productId && q.IsApproved)
            .OrderByDescending(q => q.UpVotes)
            .ThenByDescending(q => q.CreatedAt)
            .Select(q => new
            {
                q.Id,
                q.ProductId,
                q.FullName,
                q.Question,
                q.Answer,
                q.IsAnswered,
                q.UpVotes,
                q.VotedUserIds,
                q.CreatedAt,
                q.AnsweredAt
            })
            .ToListAsync();

        var qids = list.Select(x => x.Id).ToList();
        var replyRows = await _db.ProductQuestionReplies.AsNoTracking()
            .Where(r => qids.Contains(r.QuestionId) && r.IsApproved)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new { r.Id, r.QuestionId, r.AppUserId, r.FullName, r.Body, r.CreatedAt })
            .ToListAsync();

        var repliesByQuestion = replyRows
            .GroupBy(r => r.QuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return list.Select(q => new ProductQuestionDto
        {
            Id = q.Id,
            ProductId = q.ProductId,
            FullName = q.FullName,
            Question = q.Question,
            Answer = q.Answer,
            IsAnswered = q.IsAnswered,
            UpVotes = q.UpVotes,
            HasVoted = me.HasValue && !string.IsNullOrEmpty(q.VotedUserIds)
                && ("," + q.VotedUserIds + ",").Contains("," + me.Value + ","),
            CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(q.CreatedAt, false, false),
            AnsweredAtFa = q.AnsweredAt.HasValue
                ? PersianDateHelper.ToPersianDateTimeString(q.AnsweredAt.Value, true, false)
                : null,
            Replies = repliesByQuestion.TryGetValue(q.Id, out var rs)
                ? rs.Select(r => new ProductQuestionReplyDto
                {
                    Id = r.Id,
                    QuestionId = r.QuestionId,
                    FullName = r.FullName,
                    Body = r.Body,
                    IsAdmin = r.AppUserId.HasValue && adminIds.Contains(r.AppUserId.Value),
                    IsMine = me.HasValue && r.AppUserId == me.Value,
                    IsApproved = true,
                    CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(r.CreatedAt, false, false)
                }).ToList()
                : new List<ProductQuestionReplyDto>()
        }).ToList();
    }

    [Authorize]
    [HttpPost("{id:int}/reply")]
    [EnableRateLimiting("commerce")]
    public async Task<IActionResult> Reply(int productId, int id, [FromBody] ProductQuestionReplyRequest request)
    {
        if (!await IsQnaEnabledAsync())
            return BadRequest("پرسش و پاسخ در حال حاضر غیرفعال است.");

        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var me = CurrentUserId();
        if (!me.HasValue) return Unauthorized();

        var question = await _db.ProductQuestions
            .FirstOrDefaultAsync(q => q.Id == id && q.ProductId == productId && q.IsApproved);
        if (question == null) return NotFound("پرسش یافت نشد");

        var name = await _db.Users
            .Where(u => u.Id == me.Value)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(name)) name = "کاربر";

        var requireApproval = await _db.SiteSettings.AsNoTracking()
            .Select(s => (bool?)s.RequireReviewApproval)
            .FirstOrDefaultAsync() ?? true;

        var reply = new ProductQuestionReply
        {
            QuestionId = question.Id,
            AppUserId = me.Value,
            FullName = name,
            Body = request.Body.Trim(),
            IsApproved = !requireApproval,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.ProductQuestionReplies.Add(reply);
        await _db.SaveChangesAsync();
        await EvictCacheAsync();

        return Ok(new
        {
            id = reply.Id,
            isApproved = reply.IsApproved,
            message = reply.IsApproved
                ? "پاسخ شما ثبت شد."
                : "پاسخ شما ثبت شد و پس از تأیید نمایش داده خواهد شد."
        });
    }

    private async Task<List<int>> AdminUserIdsAsync()
    {
        try
        {
            return await _db.AppUserRoles
                .Where(ur => _db.RolePermissions.Any(rp =>
                    rp.RoleId == ur.RoleId && rp.Permission.Key == "admin.products.manage"))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();
        }
        catch
        {
            return new List<int>();
        }
    }

    [Authorize]
    [HttpPost]
    [EnableRateLimiting("commerce")]
    public async Task<IActionResult> Ask(int productId, [FromBody] ProductQuestionRequest request)
    {
        if (!await IsQnaEnabledAsync())
            return BadRequest("پرسش و پاسخ در حال حاضر غیرفعال است.");

        var product = await _db.Products.FindAsync(productId);
        if (product == null) return NotFound("محصول یافت نشد");

        var me = CurrentUserId();
        var name = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(name) && me.HasValue)
        {
            name = await _db.Users
                .Where(u => u.Id == me.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync();
        }
        if (string.IsNullOrWhiteSpace(name)) name = "کاربر";

        var question = new ProductQuestion
        {
            ProductId = productId,
            AppUserId = me,
            FullName = name,
            Question = request.Question.Trim(),
            IsApproved = !(_db.SiteSettings.AsNoTracking().Select(s => (bool?)s.RequireReviewApproval).FirstOrDefault() ?? true),
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.ProductQuestions.Add(question);
        await _db.SaveChangesAsync();
        await EvictCacheAsync();

        return Ok(new
        {
            id = question.Id,
            message = question.IsApproved
                ? "پرسش شما ثبت شد."
                : "پرسش شما ثبت شد و پس از تأیید نمایش داده خواهد شد."
        });
    }

    [Authorize]
    [HttpPost("{id:int}/vote")]
    public async Task<IActionResult> Vote(int productId, int id)
    {
        if (!await IsQnaEnabledAsync()) return BadRequest("پرسش و پاسخ غیرفعال است.");

        var me = CurrentUserId();
        if (!me.HasValue) return Unauthorized();

        var question = await _db.ProductQuestions
            .FirstOrDefaultAsync(q => q.Id == id && q.ProductId == productId && q.IsApproved);
        if (question == null) return NotFound("پرسش یافت نشد");

        var ids = string.IsNullOrEmpty(question.VotedUserIds)
            ? new List<int>()
            : question.VotedUserIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var v) ? (int?)v : null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

        if (ids.Contains(me.Value))
        {
            ids.Remove(me.Value);
            question.UpVotes = Math.Max(0, question.UpVotes - 1);
        }
        else
        {
            ids.Add(me.Value);
            question.UpVotes += 1;
        }

        question.VotedUserIds = ids.Count == 0 ? null : string.Join(",", ids);
        await _db.SaveChangesAsync();

        return Ok(new { upVotes = question.UpVotes, voted = ids.Contains(me.Value) });
    }
}

[ApiController]
[Route("api/questions")]
public sealed class QuestionsAdminController : ControllerBase
{
    private const string CacheTag = "product-questions";

    private readonly ErrorServiceDbContext _db;
    private readonly Microsoft.AspNetCore.OutputCaching.IOutputCacheStore _cacheStore;

    public QuestionsAdminController(ErrorServiceDbContext db, Microsoft.AspNetCore.OutputCaching.IOutputCacheStore cacheStore)
    {
        _db = db;
        _cacheStore = cacheStore;
    }

    private async Task EvictCacheAsync()
    {
        try { await _cacheStore.EvictByTagAsync(CacheTag, CancellationToken.None); } catch { }
    }

    [Authorize(Policy = "perm:admin.products.view")]
    [HttpGet("admin/all")]
    public async Task<ActionResult<List<ProductQuestionAdminDto>>> GetAll(
        bool? pendingOnly = null,
        int? productId = null,
        string? search = null,
        int page = 1,
        int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var query = _db.ProductQuestions.AsNoTracking().AsQueryable();

        if (pendingOnly == true)
            query = query.Where(q => !q.IsApproved || !q.IsAdminRead);
        if (pendingOnly == false)
            query = query.Where(q => q.IsApproved && q.IsAdminRead);
        if (productId.HasValue)
            query = query.Where(q => q.ProductId == productId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(q => q.Question.Contains(s) || q.FullName.Contains(s) || q.Product!.Name.Contains(s));
        }

        var total = await query.CountAsync();
        Response.Headers["X-Total-Count"] = total.ToString();

        var rows = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new
            {
                q.Id,
                q.ProductId,
                ProductTitle = q.Product!.Name,
                ProductImageUrl = q.Product!.MainImageUrl,
                q.FullName,
                q.Question,
                q.Answer,
                q.IsApproved,
                q.IsAnswered,
                q.IsAdminRead,
                q.UpVotes,
                q.CreatedAt
            })
            .ToListAsync();

        var ids = rows.Select(x => x.Id).ToList();
        var replyRows = await _db.ProductQuestionReplies.AsNoTracking()
            .Where(r => ids.Contains(r.QuestionId))
            .OrderBy(r => r.CreatedAt)
            .Select(r => new { r.Id, r.QuestionId, r.AppUserId, r.FullName, r.Body, r.IsApproved, r.CreatedAt })
            .ToListAsync();

        var adminIds = await AdminUserIdsAsync();

        var items = rows.Select(q => new ProductQuestionAdminDto
        {
            Id = q.Id,
            ProductId = q.ProductId,
            ProductTitle = q.ProductTitle,
            ProductImageUrl = q.ProductImageUrl,
            FullName = q.FullName,
            Question = q.Question,
            Answer = q.Answer,
            IsApproved = q.IsApproved,
            IsAnswered = q.IsAnswered,
            IsAdminRead = q.IsAdminRead,
            UpVotes = q.UpVotes,
            CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(q.CreatedAt, true, false),
            Replies = replyRows.Where(r => r.QuestionId == q.Id).Select(r => new ProductQuestionReplyDto
            {
                Id = r.Id,
                QuestionId = r.QuestionId,
                FullName = r.FullName,
                Body = r.Body,
                IsAdmin = r.AppUserId.HasValue && adminIds.Contains(r.AppUserId.Value),
                IsApproved = r.IsApproved,
                CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(r.CreatedAt, true, false)
            }).ToList()
        }).ToList();

        return items;
    }

    [Authorize(Policy = "perm:admin.products.view")]
    [HttpGet("admin/summary")]
    public async Task<ActionResult<List<ProductQuestionProductSummaryDto>>> GetProductSummary(string? search = null)
    {
        var query = _db.ProductQuestions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(q => q.Question.Contains(s) || q.FullName.Contains(s) || q.Product!.Name.Contains(s));
        }

        var rows = await query
            .GroupBy(q => new { q.ProductId, ProductTitle = q.Product!.Name, q.Product!.MainImageUrl, q.Product!.Slug })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductTitle,
                g.Key.MainImageUrl,
                g.Key.Slug,
                TotalCount = g.Count(),
                UnansweredCount = g.Count(x => !x.IsAnswered),
                PendingCount = g.Count(x => !x.IsApproved),
                UnreadCount = g.Count(x => !x.IsAdminRead),
                LastCreatedAt = g.Max(x => x.CreatedAt)
            })
            .OrderByDescending(x => x.UnreadCount)
            .ThenByDescending(x => x.LastCreatedAt)
            .ToListAsync();

        return rows.Select(x => new ProductQuestionProductSummaryDto
        {
            ProductId = x.ProductId,
            ProductTitle = x.ProductTitle,
            ProductImageUrl = x.MainImageUrl,
            ProductSlug = x.Slug,
            TotalCount = x.TotalCount,
            UnansweredCount = x.UnansweredCount,
            PendingCount = x.PendingCount,
            UnreadCount = x.UnreadCount,
            LastCreatedAtFa = PersianDateHelper.ToPersianDateTimeString(x.LastCreatedAt, true, false)
        }).ToList();
    }

    [Authorize(Policy = "perm:admin.products.manage")]
    [HttpPost("reply/{id:int}/approve")]
    public async Task<IActionResult> ToggleReplyApprove(int id)
    {
        var reply = await _db.ProductQuestionReplies.FindAsync(id);
        if (reply == null) return NotFound("پاسخ یافت نشد");
        reply.IsApproved = !reply.IsApproved;
        await _db.SaveChangesAsync();
        await EvictCacheAsync();
        return Ok(new { id = reply.Id, isApproved = reply.IsApproved });
    }

    [Authorize(Policy = "perm:admin.products.manage")]
    [HttpDelete("reply/{id:int}")]
    public async Task<IActionResult> DeleteReply(int id)
    {
        var reply = await _db.ProductQuestionReplies.FindAsync(id);
        if (reply == null) return NotFound("پاسخ یافت نشد");
        _db.ProductQuestionReplies.Remove(reply);
        await _db.SaveChangesAsync();
        await EvictCacheAsync();
        return Ok(new { deleted = true });
    }

    private async Task<List<int>> AdminUserIdsAsync()
    {
        try
        {
            return await _db.AppUserRoles
                .Where(ur => _db.RolePermissions.Any(rp =>
                    rp.RoleId == ur.RoleId && rp.Permission.Key == "admin.products.manage"))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();
        }
        catch
        {
            return new List<int>();
        }
    }

    [Authorize(Policy = "perm:admin.products.view")]
    [HttpPost("admin/read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var unread = await _db.ProductQuestions.Where(q => !q.IsAdminRead).ToListAsync();
        foreach (var q in unread) q.IsAdminRead = true;
        await _db.SaveChangesAsync();
        return Ok(new { updated = unread.Count });
    }

    [Authorize(Policy = "perm:admin.products.manage")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ProductQuestionAnswerRequest request)
    {
        var question = await _db.ProductQuestions.FindAsync(id);
        if (question == null) return NotFound("پرسش یافت نشد");

        if (request.Answer != null)
        {
            var trimmed = request.Answer.Trim();
            if (trimmed.Length == 0)
            {
                question.Answer = null;
                question.IsAnswered = false;
                question.AnsweredAt = null;
            }
            else
            {
                question.Answer = trimmed;
                question.IsAnswered = true;
                question.AnsweredAt = DateTimeOffset.UtcNow;
                question.IsApproved = true;
            }
        }

        if (request.IsApproved.HasValue)
            question.IsApproved = request.IsApproved.Value;

        question.IsAdminRead = true;
        await _db.SaveChangesAsync();
        await EvictCacheAsync();
        return Ok(new
        {
            isApproved = question.IsApproved,
            isAnswered = question.IsAnswered,
            answer = question.Answer
        });
    }

    [Authorize(Policy = "perm:admin.products.manage")]
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> ToggleApprove(int id)
    {
        var question = await _db.ProductQuestions.FindAsync(id);
        if (question == null) return NotFound("پرسش یافت نشد");
        question.IsApproved = !question.IsApproved;
        question.IsAdminRead = true;
        await _db.SaveChangesAsync();
        await EvictCacheAsync();
        return Ok(new { isApproved = question.IsApproved });
    }

    [Authorize(Policy = "perm:admin.products.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var question = await _db.ProductQuestions.FindAsync(id);
        if (question == null) return NotFound("پرسش یافت نشد");
        _db.ProductQuestions.Remove(question);
        await _db.SaveChangesAsync();
        await EvictCacheAsync();
        return NoContent();
    }
}
