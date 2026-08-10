using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StoriesController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public StoriesController(ErrorServiceDbContext db, IWebHostEnvironment env, IConfiguration configuration)
    {
        _db = db;
        _env = env;
        _configuration = configuration;
    }

    private string? GetClientIp()
    {
        var forwarded = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        var ip = HttpContext.Connection.RemoteIpAddress;
        return ip?.ToString();
    }

    private string GetStoriesRelativeFolder()
    {
        var relativeFolder = _configuration["Uploads:StoriesRelativePath"];
        if (string.IsNullOrWhiteSpace(relativeFolder)) relativeFolder = "uploads/stories";
        return relativeFolder.Trim().TrimStart('~').TrimStart('/').TrimEnd('/');
    }

    [HttpGet]
    [OutputCache(Duration = 300)]
    public async Task<ActionResult<List<StoryDto>>> GetPublicStories()
    {
        try
        {
            var now = DateTime.Now;
            var twentyFourHoursAgo = now.AddHours(-24);

            var activeStories = await _db.Stories
                .Where(s => s.IsActive && s.CreatedAt >= twentyFourHoursAgo)
                .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new StoryDto
                    {
                        Id = s.Id,
                        MediaUrl = s.MediaUrl,
                        IsVideo = s.IsVideo,
                        Title = s.Title,
                        LinkUrl = s.LinkUrl,
                        DisplayDuration = s.DisplayDuration,
                        IsActive = s.IsActive,
                        ViewCount = s.ViewCount,
                        LikeCount = s.LikeCount,
                        CreatedAt = s.CreatedAt
                    })
                .ToListAsync();

            if (activeStories.Count == 0)
            {
                activeStories = await _db.Stories
                    .Where(s => s.IsActive)
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(15)
                    .Select(s => new StoryDto
                    {
                        Id = s.Id,
                        MediaUrl = s.MediaUrl,
                        IsVideo = s.IsVideo,
                        Title = s.Title,
                        LinkUrl = s.LinkUrl,
                        DisplayDuration = s.DisplayDuration,
                        IsActive = s.IsActive,
                        ViewCount = s.ViewCount,
                        LikeCount = s.LikeCount,
                        CreatedAt = s.CreatedAt
                    })
                    .ToListAsync();
            }

            return activeStories;
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPost("{id:int}/view")]
    public async Task<IActionResult> IncrementView(int id)
    {
        var story = await _db.Stories.FindAsync(id);
        if (story == null) return NotFound();

        story.ViewCount++;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.stories.manage")]
    [HttpGet("admin")]
    public async Task<ActionResult<List<StoryDto>>> GetAllAdmin()
    {
        return await _db.Stories
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new StoryDto
            {
                Id = s.Id,
                MediaUrl = s.MediaUrl,
                IsVideo = s.IsVideo,
                Title = s.Title,
                LinkUrl = s.LinkUrl,
                DisplayDuration = s.DisplayDuration,
                IsActive = s.IsActive,
                ViewCount = s.ViewCount,
                LikeCount = s.LikeCount,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();
    }

    [HttpPost("{id:int}/like")]
    public async Task<ActionResult<object>> ToggleLike(int id)
    {
        var story = await _db.Stories.FindAsync(id);
        if (story == null) return NotFound();

        var ip = GetClientIp();
        if (string.IsNullOrWhiteSpace(ip))
            return BadRequest(new { message = "نمی‌توان آدرس IP را تشخیص داد" });

        var existingLike = await _db.StoryLikes
            .FirstOrDefaultAsync(l => l.StoryId == id && l.IpAddress == ip);

        if (existingLike != null)
        {
            _db.StoryLikes.Remove(existingLike);
            story.LikeCount = Math.Max(0, story.LikeCount - 1);
            await _db.SaveChangesAsync();
            return Ok(new { liked = false, likeCount = story.LikeCount });
        }
        else
        {
            _db.StoryLikes.Add(new StoryLike
            {
                StoryId = id,
                IpAddress = ip,
                CreatedAt = DateTime.Now
            });
            story.LikeCount++;
            await _db.SaveChangesAsync();
            return Ok(new { liked = true, likeCount = story.LikeCount });
        }
    }

    [Authorize(Policy = "perm:admin.stories.manage")]
    [HttpPost("admin")]
    public async Task<ActionResult<StoryDto>> Create(StoryUpsertRequest request)
    {
        var story = new Story
        {
            MediaUrl = request.MediaUrl,
            IsVideo = request.IsVideo,
            Title = request.Title,
            LinkUrl = request.LinkUrl,
            DisplayDuration = request.DisplayDuration,
            IsActive = request.IsActive,
            CreatedAt = DateTime.Now
        };

        _db.Stories.Add(story);
        await _db.SaveChangesAsync();

        return Ok(new StoryDto { Id = story.Id });
    }

    [Authorize(Policy = "perm:admin.stories.manage")]
    [HttpPut("admin/{id:int}")]
    public async Task<IActionResult> Update(int id, StoryUpsertRequest request)
    {
        var story = await _db.Stories.FindAsync(id);
        if (story == null) return NotFound();

        story.MediaUrl = request.MediaUrl;
        story.IsVideo = request.IsVideo;
        story.Title = request.Title;
        story.LinkUrl = request.LinkUrl;
        story.DisplayDuration = request.DisplayDuration;
        story.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.stories.manage")]
    [HttpDelete("admin/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var story = await _db.Stories.FindAsync(id);
        if (story == null) return NotFound();

        _db.Stories.Remove(story);
        await _db.SaveChangesAsync();

        // Optional: Delete file from disk
        try
        {
            var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var relativeFolder = GetStoriesRelativeFolder();
            var expectedPrefix = "/" + relativeFolder.TrimEnd('/') + "/";
            if (!string.IsNullOrWhiteSpace(story.MediaUrl)
                && story.MediaUrl.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var fullPath = Path.Combine(wwwroot, story.MediaUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            }
        }
        catch { }

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.stories.manage")]
    [HttpPost("admin/upload")]
    public async Task<ActionResult<string>> Upload([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("فایل خالی است");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var relativeFolder = "uploads/stories";
        var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var uploadsRoot = Path.Combine(wwwroot, relativeFolder);

        if (!Directory.Exists(uploadsRoot)) Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        return Ok($"/{relativeFolder}/{fileName}");
    }
}
