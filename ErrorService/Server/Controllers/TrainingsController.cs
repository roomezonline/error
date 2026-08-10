using ErrorService.Server.Data;
using ErrorService.Server.Infrastructure;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrainingsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    private const string VipRoleKey = "vip";

    public TrainingsController(ErrorServiceDbContext db, IWebHostEnvironment env, IConfiguration configuration)
    {
        _db = db;
        _env = env;
        _configuration = configuration;
    }

    [HttpGet("media/{fileName}")]
    public IActionResult GetMedia(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return NotFound();

        // prevent path traversal
        fileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(fileName)) return NotFound();

        var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var fullPath = Path.Combine(wwwroot, "uploads", "academy", fileName);
        if (!System.IO.File.Exists(fullPath)) return NotFound();

        var provider = new FileExtensionContentTypeProvider();
        string contentType;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        contentType = ext switch
        {
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".ogv" => "video/ogg",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
            ".ts" => "video/mp2t",
            ".m3u8" => "application/vnd.apple.mpegurl",
            _ => "application/octet-stream"
        };

        // Range requests are required for smooth video playback on many browsers
        Response.Headers["Accept-Ranges"] = "bytes";
        return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
    }

    [HttpGet("lessons/latest")]
    [OutputCache(Duration = 120, VaryByQueryKeys = new[] { "take" })]
    public async Task<ActionResult<List<TrainingLessonDto>>> GetLatestLessons(int take = 4)
    {
        return await _db.TrainingLessons
            .Where(x => x.IsPublished && x.Course.IsPublished)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new TrainingLessonDto
            {
                Id = x.Id,
                CourseId = x.CourseId,
                Title = x.Title,
                Summary = x.Summary,
                IsPublished = x.IsPublished,
                IsPremium = x.IsPremium,
                SortOrder = x.SortOrder,
                CourseTitle = x.Course.Title,
                CourseCoverImageUrl = x.Course.CoverImageUrl,
                CourseSlug = x.Course.Slug
            })
            .ToListAsync();
    }

    #region Courses

    [HttpGet("courses")]
    [OutputCache(Duration = 300, VaryByQueryKeys = new[] { "onlyPublished" })]
    public async Task<ActionResult<List<TrainingCourseDto>>> GetCourses(bool onlyPublished = false)
    {
        var query = _db.TrainingCourses.AsQueryable();
        if (onlyPublished) query = query.Where(x => x.IsPublished);

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new TrainingCourseDto
            {
                Id = x.Id,
                Title = x.Title,
                Slug = x.Slug,
                Summary = x.Summary,
                CoverImageUrl = x.CoverImageUrl,
                IsPublished = x.IsPublished,
                IsPremium = x.IsPremium,
                SortOrder = x.SortOrder,
                CreatedAt = x.CreatedAt,
                LessonsCount = x.Lessons.Count
            })
            .ToListAsync();
    }

    [HttpGet("courses/slug/{slug}")]
    [OutputCache(Duration = 300)]
    public async Task<ActionResult<TrainingCourseDto>> GetCourseBySlug(string slug)
    {
        var course = await _db.TrainingCourses
            .Include(x => x.Lessons)
            .FirstOrDefaultAsync(x => x.Slug == slug);

        if (course == null) return NotFound();

        return new TrainingCourseDto
        {
            Id = course.Id,
            Title = course.Title,
            Slug = course.Slug,
            Summary = course.Summary,
            CoverImageUrl = course.CoverImageUrl,
            IsPublished = course.IsPublished,
            IsPremium = course.IsPremium,
            SortOrder = course.SortOrder,
            CreatedAt = course.CreatedAt,
            LessonsCount = course.Lessons.Count
        };
    }

    [HttpGet("courses/{id:int}")]
    [OutputCache(Duration = 300)]
    public async Task<ActionResult<TrainingCourseDto>> GetCourse(int id)
    {
        var course = await _db.TrainingCourses
            .Include(x => x.Lessons)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (course == null) return NotFound();

        return new TrainingCourseDto
        {
            Id = course.Id,
            Title = course.Title,
            Slug = course.Slug,
            Summary = course.Summary,
            CoverImageUrl = course.CoverImageUrl,
            IsPublished = course.IsPublished,
            IsPremium = course.IsPremium,
            SortOrder = course.SortOrder,
            CreatedAt = course.CreatedAt,
            LessonsCount = course.Lessons.Count
        };
    }

    [HttpPost("courses")]
    public async Task<ActionResult<TrainingCourseDto>> CreateCourse(TrainingCourseUpsertRequest request)
    {
        var course = new TrainingCourse
        {
            Title = request.Title,
            Slug = await SlugService.ResolveUniqueAsync(
                _db.TrainingCourses.Where(c => c.Slug != null).Select(c => c.Slug!),
                request.Title),
            Summary = request.Summary,
            CoverImageUrl = request.CoverImageUrl,
            IsPublished = request.IsPublished,
            IsPremium = request.IsPremium,
            SortOrder = request.SortOrder
        };

        _db.TrainingCourses.Add(course);
        await _db.SaveChangesAsync();

        return Ok(new TrainingCourseDto { Id = course.Id, Title = course.Title, Slug = course.Slug });
    }

    [HttpPut("courses/{id:int}")]
    public async Task<IActionResult> UpdateCourse(int id, TrainingCourseUpsertRequest request)
    {
        var course = await _db.TrainingCourses.FindAsync(id);
        if (course == null) return NotFound();

        var oldTitle = course.Title;
        course.Title = request.Title;
        course.Slug = await SlugService.ResolveForUpdateAsync(
            _db.TrainingCourses.Where(c => c.Slug != null && c.Id != id).Select(c => c.Slug!),
            oldTitle, course.Slug, request.Title);
        course.Summary = request.Summary;
        FileCleanupHelper.DeleteOldFileIfChanged(course.CoverImageUrl, request.CoverImageUrl, _env);
        course.CoverImageUrl = request.CoverImageUrl;
        course.IsPublished = request.IsPublished;
        course.IsPremium = request.IsPremium;
        course.SortOrder = request.SortOrder;
        course.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("courses/{id:int}")]
    public async Task<IActionResult> DeleteCourse(int id)
    {
        var course = await _db.TrainingCourses.FindAsync(id);
        if (course == null) return NotFound();

        _db.TrainingCourses.Remove(course);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    #endregion

    #region Lessons

    [HttpGet("courses/{courseId:int}/lessons")]
    [OutputCache(Duration = 300, VaryByQueryKeys = new[] { "onlyPublished" })]
    public async Task<ActionResult<List<TrainingLessonDto>>> GetLessons(int courseId, bool onlyPublished = false)
    {
        var query = _db.TrainingLessons.Where(x => x.CourseId == courseId);
        if (onlyPublished) query = query.Where(x => x.IsPublished);

        return await query
            .OrderBy(x => x.SortOrder)
            .Select(x => new TrainingLessonDto
            {
                Id = x.Id,
                CourseId = x.CourseId,
                Title = x.Title,
                Summary = x.Summary,
                IsPublished = x.IsPublished,
                IsPremium = x.IsPremium,
                SortOrder = x.SortOrder
            })
            .ToListAsync();
    }

    [HttpGet("lessons/{id:int}")]
    public async Task<ActionResult<TrainingLessonDto>> GetLesson(int id)
    {
        var lesson = await _db.TrainingLessons
            .Include(x => x.Blocks.OrderBy(b => b.SortOrder))
            .Include(x => x.Attachments.OrderBy(a => a.SortOrder))
            .Include(x => x.Course)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (lesson == null) return NotFound();

        var isPremium = lesson.IsPremium;
        if (isPremium)
        {
            var isVip = User?.Identity?.IsAuthenticated == true && User.IsInRole(VipRoleKey);
            if (!isVip)
                return Forbid();
        }

        return new TrainingLessonDto
        {
            Id = lesson.Id,
            CourseId = lesson.CourseId,
            Title = lesson.Title,
            Summary = lesson.Summary,
            IsPublished = lesson.IsPublished,
            IsPremium = lesson.IsPremium,
            SortOrder = lesson.SortOrder,
            Blocks = lesson.Blocks.Select(b => new TrainingBlockDto
            {
                Id = b.Id,
                LessonId = b.LessonId,
                BlockType = (TrainingBlockTypeDto)b.BlockType,
                Title = b.Title,
                Content = b.Content,
                Url = b.Url,
                ThumbnailUrl = b.ThumbnailUrl,
                FileName = b.FileName,
                SizeBytes = b.SizeBytes,
                SortOrder = b.SortOrder
            }).ToList(),
            Attachments = lesson.Attachments.Select(a => new TrainingAttachmentDto
            {
                Id = a.Id,
                LessonId = a.LessonId,
                Title = a.Title,
                FileUrl = a.FileUrl,
                FileName = a.FileName,
                SizeBytes = a.SizeBytes,
                SortOrder = a.SortOrder
            }).ToList()
        };
    }

    [HttpPost("lessons")]
    public async Task<ActionResult<TrainingLessonDto>> CreateLesson(TrainingLessonUpsertRequest request)
    {
        var lesson = new TrainingLesson
        {
            CourseId = request.CourseId,
            Title = request.Title,
            Summary = request.Summary,
            IsPublished = request.IsPublished,
            IsPremium = request.IsPremium,
            SortOrder = request.SortOrder
        };

        _db.TrainingLessons.Add(lesson);
        await _db.SaveChangesAsync();

        return Ok(new TrainingLessonDto { Id = lesson.Id, Title = lesson.Title });
    }

    [HttpPut("lessons/{id:int}")]
    public async Task<IActionResult> UpdateLesson(int id, TrainingLessonUpsertRequest request)
    {
        var lesson = await _db.TrainingLessons.FindAsync(id);
        if (lesson == null) return NotFound();

        lesson.Title = request.Title;
        lesson.Summary = request.Summary;
        lesson.IsPublished = request.IsPublished;
        lesson.IsPremium = request.IsPremium;
        lesson.SortOrder = request.SortOrder;
        lesson.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("lessons/{id:int}")]
    public async Task<IActionResult> DeleteLesson(int id)
    {
        var lesson = await _db.TrainingLessons.FindAsync(id);
        if (lesson == null) return NotFound();

        _db.TrainingLessons.Remove(lesson);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    #endregion

    #region Blocks

    [HttpPost("blocks")]
    public async Task<ActionResult<TrainingBlockDto>> CreateBlock(TrainingBlockUpsertRequest request)
    {
        var block = new TrainingBlock
        {
            LessonId = request.LessonId,
            BlockType = (TrainingBlockType)request.BlockType,
            Title = request.Title,
            Content = request.Content,
            Url = request.Url,
            ThumbnailUrl = request.ThumbnailUrl,
            FileName = request.FileName,
            SortOrder = request.SortOrder
        };

        _db.TrainingBlocks.Add(block);
        await _db.SaveChangesAsync();

        return Ok(new TrainingBlockDto { Id = block.Id, BlockType = (TrainingBlockTypeDto)block.BlockType });
    }

    [HttpPut("blocks/{id:int}")]
    public async Task<IActionResult> UpdateBlock(int id, TrainingBlockUpsertRequest request)
    {
        var block = await _db.TrainingBlocks.FindAsync(id);
        if (block == null) return NotFound();

        block.BlockType = (TrainingBlockType)request.BlockType;
        block.Title = request.Title;
        block.Content = request.Content;
        FileCleanupHelper.DeleteOldFileIfChanged(block.Url, request.Url, _env);
        block.Url = request.Url;
        FileCleanupHelper.DeleteOldFileIfChanged(block.ThumbnailUrl, request.ThumbnailUrl, _env);
        block.ThumbnailUrl = request.ThumbnailUrl;
        block.FileName = request.FileName;
        block.SortOrder = request.SortOrder;
        block.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("blocks/{id:int}")]
    public async Task<IActionResult> DeleteBlock(int id)
    {
        var block = await _db.TrainingBlocks.FindAsync(id);
        if (block == null) return NotFound();

        _db.TrainingBlocks.Remove(block);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    #endregion

    #region Attachments

    [HttpPost("attachments")]
    public async Task<ActionResult<TrainingAttachmentDto>> CreateAttachment(TrainingAttachmentUpsertRequest request)
    {
        var attachment = new TrainingAttachment
        {
            LessonId = request.LessonId,
            Title = request.Title,
            FileUrl = request.FileUrl,
            FileName = request.FileName,
            SortOrder = request.SortOrder
        };

        _db.TrainingAttachments.Add(attachment);
        await _db.SaveChangesAsync();

        return Ok(new TrainingAttachmentDto { Id = attachment.Id, Title = attachment.Title });
    }

    [HttpPut("attachments/{id:int}")]
    public async Task<IActionResult> UpdateAttachment(int id, TrainingAttachmentUpsertRequest request)
    {
        var attachment = await _db.TrainingAttachments.FindAsync(id);
        if (attachment == null) return NotFound();

        attachment.Title = request.Title;
        FileCleanupHelper.DeleteOldFileIfChanged(attachment.FileUrl, request.FileUrl, _env);
        attachment.FileUrl = request.FileUrl;
        attachment.FileName = request.FileName;
        attachment.SortOrder = request.SortOrder;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("attachments/{id:int}")]
    public async Task<IActionResult> DeleteAttachment(int id)
    {
        var attachment = await _db.TrainingAttachments.FindAsync(id);
        if (attachment == null) return NotFound();

        _db.TrainingAttachments.Remove(attachment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    #endregion

    #region Upload

    [HttpPost("upload")]
    [RequestSizeLimit(100_000_000)] // 100MB for media
    public async Task<ActionResult<string>> Upload([FromForm] IFormFile file, CancellationToken cancellationToken = default)
    {
        try
        {
            if (file == null || file.Length <= 0) return BadRequest("فایل خالی است");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var relativeFolder = "uploads/academy";
            var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var uploadsRoot = Path.Combine(wwwroot, relativeFolder);

            if (!Directory.Exists(uploadsRoot)) Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsRoot, fileName);

            using (var outStream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(outStream, cancellationToken);
            }

            return Ok($"/api/trainings/media/{fileName}");
        }
        catch (Exception ex)
        {
            return BadRequest($"آپلود ناموفق بود: {ex.Message}");
        }
    }

    #endregion
}
