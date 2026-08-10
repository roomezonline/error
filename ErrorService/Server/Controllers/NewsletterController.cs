using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class NewsletterController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public NewsletterController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] NewsletterSubscribeRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var existing = await _db.NewsletterSubscriptions
            .FirstOrDefaultAsync(x => x.Email == email);

        if (existing != null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                await _db.SaveChangesAsync();
            }
            return Ok(new { message = "این ایمیل قبلاً ثبت شده است." });
        }

        _db.NewsletterSubscriptions.Add(new NewsletterSubscription
        {
            Email = email,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "ایمیل شما با موفقیت در خبرنامه ثبت شد." });
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpGet]
    public async Task<ActionResult<List<NewsletterSubscriptionDto>>> GetAll()
    {
        return await _db.NewsletterSubscriptions
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new NewsletterSubscriptionDto
            {
                Id = x.Id,
                Email = x.Email,
                IsActive = x.IsActive,
                CreatedAtFa = x.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm")
            })
            .ToListAsync();
    }

    [Authorize(Policy = "perm:admin.settings.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var sub = await _db.NewsletterSubscriptions.FindAsync(id);
        if (sub == null) return NotFound();

        _db.NewsletterSubscriptions.Remove(sub);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
