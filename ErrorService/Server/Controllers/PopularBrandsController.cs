using ErrorService.Server.Data;
using ErrorService.Server.Infrastructure;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/popular-brands")]
public sealed class PopularBrandsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public PopularBrandsController(ErrorServiceDbContext db, IWebHostEnvironment env, IConfiguration configuration)
    {
        _db = db;
        _env = env;
        _configuration = configuration;
    }

    [HttpGet("active")]
    [OutputCache(Duration = 1800)]
    public async Task<ActionResult<List<PopularBrandItemDto>>> GetActive(CancellationToken cancellationToken)
    {
        var items = await _db.PopularBrandItems
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.Id)
            .Select(x => new PopularBrandItemDto
            {
                Id = x.Id,
                Title = x.Title,
                ImageUrl = x.ImageUrl,
                LinkUrl = x.LinkUrl,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [Authorize(Policy = "perm:admin.popularbrands.manage")]
    [HttpGet]
    public async Task<ActionResult<List<PopularBrandItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.PopularBrandItems
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.Id)
            .Select(x => new PopularBrandItemDto
            {
                Id = x.Id,
                Title = x.Title,
                ImageUrl = x.ImageUrl,
                LinkUrl = x.LinkUrl,
                SortOrder = x.SortOrder,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [Authorize(Policy = "perm:admin.popularbrands.manage")]
    [HttpPost]
    public async Task<ActionResult<PopularBrandItemDto>> Create([FromBody] PopularBrandItemUpsertRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new PopularBrandItem
        {
            Title = request.Title.Trim(),
            ImageUrl = request.ImageUrl?.Trim(),
            LinkUrl = request.LinkUrl?.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.PopularBrandItems.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new PopularBrandItemDto
        {
            Id = entity.Id,
            Title = entity.Title,
            ImageUrl = entity.ImageUrl,
            LinkUrl = entity.LinkUrl,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive
        });
    }

    [Authorize(Policy = "perm:admin.popularbrands.manage")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<PopularBrandItemDto>> Update(int id, [FromBody] PopularBrandItemUpsertRequest request, CancellationToken cancellationToken)
    {
        var entity = await _db.PopularBrandItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return NotFound();

        entity.Title = request.Title.Trim();
        FileCleanupHelper.DeleteOldFileIfChanged(entity.ImageUrl, request.ImageUrl?.Trim(), _env);
        entity.ImageUrl = request.ImageUrl?.Trim();
        entity.LinkUrl = request.LinkUrl?.Trim();
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new PopularBrandItemDto
        {
            Id = entity.Id,
            Title = entity.Title,
            ImageUrl = entity.ImageUrl,
            LinkUrl = entity.LinkUrl,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive
        });
    }

    [Authorize(Policy = "perm:admin.popularbrands.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.PopularBrandItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return NotFound();

        _db.PopularBrandItems.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.popularbrands.manage")]
    [HttpPost("upload")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<string>> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length <= 0)
            return BadRequest("فایل خالی است یا دریافت نشد");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".bin";

        var safeExt = ext.ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".svg" };
        if (!allowed.Contains(safeExt))
            return BadRequest("فرمت فایل مجاز نیست. فقط jpg, jpeg, png, webp, svg");

        var relativeFolder = _configuration["Uploads:PopularBrandsRelativePath"];
        if (string.IsNullOrWhiteSpace(relativeFolder))
            relativeFolder = "uploads/popular-brands";

        relativeFolder = relativeFolder.Trim().TrimStart('~').TrimStart('/').TrimEnd('/');

        var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        if (!Directory.Exists(wwwroot))
            Directory.CreateDirectory(wwwroot);

        var uploadsRoot = Path.Combine(wwwroot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{safeExt}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using (var outStream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(outStream, cancellationToken);
        }

        var publicUrl = "/" + relativeFolder.Replace("\\", "/") + "/" + fileName;
        return Ok(publicUrl);
    }
}
