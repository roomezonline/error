using ErrorService.Server.Data;
using ErrorService.Server.Infrastructure;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SlidersController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public SlidersController(ErrorServiceDbContext db, IWebHostEnvironment env, IConfiguration configuration)
    {
        _db = db;
        _env = env;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<List<SliderItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.SliderItems
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.Id)
            .Select(x => new SliderItemDto
            {
                Id = x.Id,
                Title = x.Title,
                Subtitle = x.Subtitle,
                ImageUrl = x.ImageUrl,
                MobileImageUrl = x.MobileImageUrl,
                LinkUrl = x.LinkUrl,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return items;
    }

    [HttpGet("active")]
    [OutputCache(Duration = 1800)]
    public async Task<ActionResult<List<SliderItemDto>>> GetActive(CancellationToken cancellationToken)
    {
        var items = await _db.SliderItems
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.Id)
            .Select(x => new SliderItemDto
            {
                Id = x.Id,
                Title = x.Title,
                Subtitle = x.Subtitle,
                ImageUrl = x.ImageUrl,
                MobileImageUrl = x.MobileImageUrl,
                LinkUrl = x.LinkUrl,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return items;
    }

    [Authorize(Policy = "perm:admin.sliders.manage")]
    [HttpPost]
    public async Task<ActionResult<SliderItemDto>> Create([FromBody] SliderUpsertRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new SliderItem
        {
            Title = request.Title,
            Subtitle = request.Subtitle,
            ImageUrl = request.ImageUrl,
            MobileImageUrl = request.MobileImageUrl,
            LinkUrl = request.LinkUrl,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.SliderItems.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return new SliderItemDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Subtitle = entity.Subtitle,
            ImageUrl = entity.ImageUrl,
            MobileImageUrl = entity.MobileImageUrl,
            LinkUrl = entity.LinkUrl,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive
        };
    }

    [Authorize(Policy = "perm:admin.sliders.manage")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<SliderItemDto>> Update(int id, [FromBody] SliderUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.SliderItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Title = request.Title;
        entity.Subtitle = request.Subtitle;
        FileCleanupHelper.DeleteOldFileIfChanged(entity.ImageUrl, request.ImageUrl, _env);
        entity.ImageUrl = request.ImageUrl;
        FileCleanupHelper.DeleteOldFileIfChanged(entity.MobileImageUrl, request.MobileImageUrl, _env);
        entity.MobileImageUrl = request.MobileImageUrl;
        entity.LinkUrl = request.LinkUrl;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return new SliderItemDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Subtitle = entity.Subtitle,
            ImageUrl = entity.ImageUrl,
            MobileImageUrl = entity.MobileImageUrl,
            LinkUrl = entity.LinkUrl,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive
        };
    }

    [Authorize(Policy = "perm:admin.sliders.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.SliderItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        _db.SliderItems.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.sliders.manage")]
    [HttpPost("upload")]
    [RequestSizeLimit(15_000_000)]
    public async Task<ActionResult<string>> Upload([FromForm] IFormFile file, [FromQuery] bool force = false, [FromQuery] string? variant = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (file == null || file.Length <= 0)
            {
                return BadRequest("فایل خالی است یا دریافت نشد");
            }

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".bin";
            }

            var safeExt = ext.ToLowerInvariant();
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(safeExt))
            {
                return BadRequest("فرمت فایل مجاز نیست. فقط jpg, jpeg, png, webp");
            }

            var relativeFolder = _configuration["Uploads:SlidersRelativePath"];
            if (string.IsNullOrWhiteSpace(relativeFolder))
            {
                relativeFolder = "uploads/sliders";
            }

            relativeFolder = relativeFolder.Trim().TrimStart('~').TrimStart('/').TrimEnd('/');
            
            // Ensure wwwroot exists
            var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            if (!Directory.Exists(wwwroot))
            {
                Directory.CreateDirectory(wwwroot);
            }

            var uploadsRoot = Path.Combine(wwwroot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(uploadsRoot))
            {
                Directory.CreateDirectory(uploadsRoot);
            }

            var fileName = $"{Guid.NewGuid():N}{safeExt}";
            var fullPath = Path.Combine(uploadsRoot, fileName);

            await using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;

            SixLabors.ImageSharp.ImageInfo? info;
            try
            {
                info = await Image.IdentifyAsync(buffer, cancellationToken);
            }
            catch (Exception ex)
            {
                return BadRequest($"خطا در شناسایی تصویر: {ex.Message}");
            }

            if (info is null)
            {
                return BadRequest("فایل تصویر معتبر نیست");
            }

            var width = info.Width;
            var height = info.Height;

            var v = (variant ?? "desktop").Trim().ToLowerInvariant();
            if (v != "desktop" && v != "mobile")
            {
                return BadRequest("variant نامعتبر است. فقط desktop یا mobile");
            }

            if (!force)
            {
                if (v == "desktop")
                {
                    var isStandard = (width == 2880 && height == 600) || (width == 2880 && height == 540) || (width == 2880 && height == 700);
                    if (!isStandard)
                    {
                        return BadRequest("WARN_SIZE: سایز استاندارد دسکتاپ 2880x600 پیکسل است. آیا با همین ابعاد ادامه می‌دهید؟");
                    }
                }
                else
                {
                    if (width < 320 || height < 240)
                    {
                        return BadRequest("WARN_SIZE: عکس موبایل بسیار کوچک است (حداقل 1080x540 پیشنهاد می‌شود). آیا با همین کیفیت ادامه می‌دهید؟");
                    }

                    var ratio = width / (double)height;
                    if (ratio < 0.4 || ratio > 2.6)
                    {
                        return BadRequest("WARN_RATIO: نسبت تصویر موبایل استاندارد نیست (پیشنهاد: 2:1 یا 1080x540). آیا با همین ظاهر ادامه می‌دهید؟");
                    }
                }
            }

            buffer.Position = 0;
            await using (var outStream = System.IO.File.Create(fullPath))
            {
                await buffer.CopyToAsync(outStream, cancellationToken);
            }

            var publicUrl = $"/{relativeFolder}/{fileName}";
            return Ok(publicUrl);
        }
        catch (Exception ex)
        {
            return BadRequest($"آپلود تصویر ناموفق بود: {ex.Message}");
        }
    }
}
