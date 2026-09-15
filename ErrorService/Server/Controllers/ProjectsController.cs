using System.IO.Compression;
using System.Text.Json;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProjectsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;

    private static readonly HashSet<string> AllowedImageExts = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> AllowedFileExts = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".zip", ".rar", ".7z", ".txt", ".csv", ".json", ".xml", ".kicad_pcb", ".kicad_sch", ".brd", ".sch", ".gbr", ".drl", ".zip" };

    public ProjectsController(ErrorServiceDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    private string ProjectsRoot => Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads", "projects");

    private string GetProjectFolder(int id) => Path.Combine(ProjectsRoot, id.ToString());

    private void EnsureProjectFolders(int id)
    {
        var root = GetProjectFolder(id);
        foreach (var sub in new[] { "cover", "images", "files", "maps", "pcb" })
            Directory.CreateDirectory(Path.Combine(root, sub));
    }

    private static string Slugify(string text)
    {
        var slug = text.Trim().ToLowerInvariant();
        slug = slug.Replace(" ", "-");
        var allowed = slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray();
        return new string(allowed).Trim('-');
    }

    private static string NormalizeType(string? type) => (type ?? "files").ToLowerInvariant() switch
    {
        "cover" => "cover",
        "images" => "images",
        "maps" => "maps",
        "pcb" => "pcb",
        _ => "files"
    };

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    private async Task<List<ProjectSection>> LoadSectionsAsync(int projectId)
        => await _db.ProjectSections
            .Where(s => s.ProjectId == projectId)
            .Include(s => s.Items.OrderBy(i => i.SortOrder))
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

    // ──────────────────── CRUD ────────────────────

    [Authorize(Policy = "perm:admin.projects.view")]
    [HttpGet]
    public async Task<ActionResult<List<ProjectListDto>>> GetProjects([FromQuery] string? search)
    {
        var q = _db.Projects.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(p => p.Name.Contains(search) || (p.Summary != null && p.Summary.Contains(search)));

        var list = await q.OrderByDescending(p => p.SortOrder).ThenByDescending(p => p.CreatedAt)
            .Select(p => new ProjectListDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Summary = p.Summary,
                CoverImageUrl = p.CoverImageUrl,
                SortOrder = p.SortOrder,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                ImageCount = p.Images.Count,
                FileCount = p.Files.Count,
                SectionCount = p.Sections.Count
            }).ToListAsync();

        return Ok(list);
    }

    [Authorize(Policy = "perm:admin.projects.view")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProjectDto>> GetProject(int id)
    {
        var p = await _db.Projects.AsNoTracking()
            .Include(x => x.Images.OrderBy(i => i.SortOrder))
            .Include(x => x.Files.OrderByDescending(f => f.UploadedAt))
            .Include(x => x.Sections).ThenInclude(s => s.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(x => x.Id == id);

        if (p == null) return NotFound();

        return Ok(new ProjectDto
        {
            Id = p.Id,
            Name = p.Name,
            Slug = p.Slug,
            Summary = p.Summary,
            Description = p.Description,
            CoverImageUrl = p.CoverImageUrl,
            SortOrder = p.SortOrder,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            Images = p.Images.Select(i => new ProjectImageDto
            {
                Id = i.Id,
                ImageUrl = i.ImageUrl,
                Caption = i.Caption,
                SortOrder = i.SortOrder
            }).ToList(),
            Files = p.Files.Select(f => new ProjectFileDto
            {
                Id = f.Id,
                FileName = f.FileName,
                FileUrl = f.FileUrl,
                FileType = f.FileType,
                Version = f.Version,
                Description = f.Description,
                UploadedAt = f.UploadedAt
            }).ToList(),
            Sections = p.Sections.Select(s => new ProjectSectionDto
            {
                Id = s.Id,
                Title = s.Title,
                SortOrder = s.SortOrder,
                Items = s.Items.Select(i => new ProjectSectionItemDto
                {
                    Id = i.Id,
                    ItemType = i.ItemType,
                    TextContent = i.TextContent,
                    MediaUrl = i.MediaUrl,
                    FileName = i.FileName,
                    Description = i.Description,
                    SortOrder = i.SortOrder
                }).ToList()
            }).ToList()
        });
    }

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpPost]
    public async Task<ActionResult<ProjectDto>> CreateProject([FromBody] ProjectUpsertRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            Name = request.Name,
            Slug = Slugify(request.Name),
            Summary = request.Summary,
            Description = request.Description,
            CoverImageUrl = request.CoverImageUrl,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        EnsureProjectFolders(project.Id);

        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Slug = project.Slug,
            Summary = project.Summary,
            Description = project.Description,
            CoverImageUrl = project.CoverImageUrl,
            SortOrder = project.SortOrder,
            IsActive = project.IsActive,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        });
    }

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProject(int id, [FromBody] ProjectUpsertRequest request)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound();

        project.Name = request.Name;
        project.Slug = Slugify(request.Name);
        project.Summary = request.Summary;
        project.Description = request.Description;
        project.CoverImageUrl = request.CoverImageUrl;
        project.SortOrder = request.SortOrder;
        project.IsActive = request.IsActive;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound();

        await using var tx = await _db.Database.BeginTransactionAsync();

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        DeleteDirectoryIfExists(GetProjectFolder(id));

        return NoContent();
    }

    // ──────────────────── Upload ────────────────────

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromQuery] int? projectId, [FromQuery] string? type)
    {
        if (file == null || file.Length <= 0) return BadRequest("فایل خالی است.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var subFolder = NormalizeType(type);
        var isImage = subFolder is "cover" or "images" or "maps";

        if (isImage && !AllowedImageExts.Contains(ext))
            return BadRequest("فرمت فایل مجاز نیست. فقط تصاویر jpg, jpeg, png, webp");

        if (!isImage && !AllowedImageExts.Contains(ext) && !AllowedFileExts.Contains(ext))
            return BadRequest("فرمت فایل مجاز نیست.");

        var folder = subFolder;
        if (projectId.HasValue)
        {
            var projectFolder = GetProjectFolder(projectId.Value);
            folder = Path.Combine(projectFolder, subFolder);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        }
        else
        {
            folder = Path.Combine(ProjectsRoot, "_temp");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        }

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(folder, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        if (projectId.HasValue)
            return Ok($"/uploads/projects/{projectId}/{subFolder}/{fileName}");

        return Ok($"/uploads/projects/_temp/{fileName}");
    }

    // ──────────────────── Images ────────────────────

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpPost("{id:int}/images")]
    public async Task<IActionResult> AddImage(int id, [FromBody] ProjectImageAddRequest request)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound();

        var image = new ProjectImage
        {
            ProjectId = id,
            ImageUrl = request.ImageUrl,
            Caption = request.Caption,
            SortOrder = request.SortOrder
        };

        _db.ProjectImages.Add(image);
        await _db.SaveChangesAsync();

        return Ok(new ProjectImageDto
        {
            Id = image.Id,
            ImageUrl = image.ImageUrl,
            Caption = image.Caption,
            SortOrder = image.SortOrder
        });
    }

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpDelete("{id:int}/images/{imageId:int}")]
    public async Task<IActionResult> DeleteImage(int id, int imageId)
    {
        var image = await _db.ProjectImages.FirstOrDefaultAsync(x => x.Id == imageId && x.ProjectId == id);
        if (image == null) return NotFound();

        _db.ProjectImages.Remove(image);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ──────────────────── Files ────────────────────

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpPost("{id:int}/files")]
    public async Task<IActionResult> AddFile(int id, [FromBody] ProjectFileAddRequest request)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound();

        var file = new ProjectFile
        {
            ProjectId = id,
            FileName = request.FileName,
            FileUrl = request.FileUrl,
            FileType = request.FileType,
            Version = request.Version,
            Description = request.Description,
            UploadedAt = DateTimeOffset.UtcNow
        };

        _db.ProjectFiles.Add(file);
        await _db.SaveChangesAsync();

        return Ok(new ProjectFileDto
        {
            Id = file.Id,
            FileName = file.FileName,
            FileUrl = file.FileUrl,
            FileType = file.FileType,
            Version = file.Version,
            Description = file.Description,
            UploadedAt = file.UploadedAt
        });
    }

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpPut("{id:int}/files/{fileId:int}")]
    public async Task<IActionResult> UpdateFile(int id, int fileId, [FromBody] ProjectFileUpdateRequest request)
    {
        var file = await _db.ProjectFiles.FirstOrDefaultAsync(x => x.Id == fileId && x.ProjectId == id);
        if (file == null) return NotFound();

        if (request.FileName != null) file.FileName = request.FileName;
        if (request.FileUrl != null) file.FileUrl = request.FileUrl;
        if (request.FileType.HasValue) file.FileType = request.FileType.Value;
        if (request.Version != null) file.Version = request.Version;
        if (request.Description != null) file.Description = request.Description;

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpDelete("{id:int}/files/{fileId:int}")]
    public async Task<IActionResult> DeleteFile(int id, int fileId)
    {
        var file = await _db.ProjectFiles.FirstOrDefaultAsync(x => x.Id == fileId && x.ProjectId == id);
        if (file == null) return NotFound();

        _db.ProjectFiles.Remove(file);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ──────────────────── Sections ────────────────────

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpPost("{id:int}/sections")]
    public async Task<IActionResult> AddSection(int id, [FromBody] ProjectSectionUpsertRequest request)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound();

        var section = new ProjectSection
        {
            ProjectId = id,
            Title = request.Title,
            SortOrder = request.SortOrder
        };

        _db.ProjectSections.Add(section);
        await _db.SaveChangesAsync();

        return Ok(new ProjectSectionDto
        {
            Id = section.Id,
            Title = section.Title,
            SortOrder = section.SortOrder
        });
    }

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpPut("{id:int}/sections/{sectionId:int}")]
    public async Task<IActionResult> UpdateSection(int id, int sectionId, [FromBody] ProjectSectionUpsertRequest request)
    {
        var section = await _db.ProjectSections.FirstOrDefaultAsync(x => x.Id == sectionId && x.ProjectId == id);
        if (section == null) return NotFound();

        section.Title = request.Title;
        section.SortOrder = request.SortOrder;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpDelete("{id:int}/sections/{sectionId:int}")]
    public async Task<IActionResult> DeleteSection(int id, int sectionId)
    {
        var section = await _db.ProjectSections.FirstOrDefaultAsync(x => x.Id == sectionId && x.ProjectId == id);
        if (section == null) return NotFound();

        _db.ProjectSections.Remove(section);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ──────────────────── Section Items ────────────────────

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpPost("{id:int}/sections/{sectionId:int}/items")]
    public async Task<IActionResult> AddSectionItem(int id, int sectionId, [FromBody] ProjectSectionItemAddRequest request)
    {
        var section = await _db.ProjectSections.FirstOrDefaultAsync(x => x.Id == sectionId && x.ProjectId == id);
        if (section == null) return NotFound();

        var item = new ProjectSectionItem
        {
            SectionId = sectionId,
            ItemType = request.ItemType,
            TextContent = request.TextContent,
            MediaUrl = request.MediaUrl,
            FileName = request.FileName,
            Description = request.Description,
            SortOrder = request.SortOrder
        };

        _db.ProjectSectionItems.Add(item);
        await _db.SaveChangesAsync();

        return Ok(new ProjectSectionItemDto
        {
            Id = item.Id,
            ItemType = item.ItemType,
            TextContent = item.TextContent,
            MediaUrl = item.MediaUrl,
            FileName = item.FileName,
            Description = item.Description,
            SortOrder = item.SortOrder
        });
    }

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpDelete("{id:int}/sections/{sectionId:int}/items/{itemId:int}")]
    public async Task<IActionResult> DeleteSectionItem(int id, int sectionId, int itemId)
    {
        var item = await _db.ProjectSectionItems
            .FirstOrDefaultAsync(x => x.Id == itemId && x.SectionId == sectionId);
        if (item == null) return NotFound();

        _db.ProjectSectionItems.Remove(item);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // ──────────────────── Export ZIP ────────────────────

    [Authorize(Policy = "perm:admin.projects.manage")]
    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> ExportProject(int id)
    {
        var project = await _db.Projects
            .Include(p => p.Images.OrderBy(i => i.SortOrder))
            .Include(p => p.Files)
            .Include(p => p.Sections).ThenInclude(s => s.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null) return NotFound();

        var projectRoot = GetProjectFolder(id);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var info = new
            {
                project.Name,
                project.Slug,
                project.Summary,
                project.Description,
                project.CoverImageUrl,
                project.CreatedAt,
                project.UpdatedAt,
                Images = project.Images.Select(i => new { i.ImageUrl, i.Caption, i.SortOrder }),
                Files = project.Files.Select(f => new { f.FileName, f.FileUrl, FileType = f.FileType.ToString(), f.Version, f.Description, f.UploadedAt }),
                Sections = project.Sections.Select(s => new
                {
                    s.Title,
                    s.SortOrder,
                    Items = s.Items.Select(i => new { ItemType = i.ItemType.ToString(), i.TextContent, i.MediaUrl, i.FileName, i.Description, i.SortOrder })
                })
            };

            var infoEntry = archive.CreateEntry("project-info.json");
            using (var infoStream = infoEntry.Open())
            {
                await JsonSerializer.SerializeAsync(infoStream, info, new JsonSerializerOptions { WriteIndented = true });
            }

            if (Directory.Exists(projectRoot))
            {
                foreach (var dir in Directory.GetDirectories(projectRoot))
                {
                    var dirName = Path.GetFileName(dir);
                    foreach (var filePath in Directory.GetFiles(dir))
                    {
                        archive.CreateEntryFromFile(filePath, $"{dirName}/{Path.GetFileName(filePath)}");
                    }
                }
            }
        }

        memoryStream.Seek(0, SeekOrigin.Begin);
        var slug = string.IsNullOrWhiteSpace(project.Slug) ? $"project-{id}" : project.Slug;
        var zipName = $"{slug}-{DateTime.Now:yyyyMMdd}.zip";
        return File(memoryStream.ToArray(), "application/zip", zipName);
    }
}
