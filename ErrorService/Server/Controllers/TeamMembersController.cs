using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TeamMembersController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;

    public TeamMembersController(ErrorServiceDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    [OutputCache(Duration = 3600, VaryByQueryKeys = new[] { "onlyPublished" })]
    public async Task<ActionResult<List<TeamMemberDto>>> GetAll(bool onlyPublished = false)
    {
        var query = _db.TeamMembers.AsQueryable();
        if (onlyPublished) query = query.Where(x => x.IsPublished);

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new TeamMemberDto
            {
                Id = x.Id,
                FullName = x.FullName,
                Role = x.Role,
                Bio = x.Bio,
                PhotoUrl = x.PhotoUrl,
                SortOrder = x.SortOrder,
                IsPublished = x.IsPublished
            })
            .ToListAsync();
    }

    [HttpGet("{id:int}")]
    [OutputCache(Duration = 3600)]
    public async Task<ActionResult<TeamMemberDto>> GetById(int id)
    {
        var member = await _db.TeamMembers.FindAsync(id);
        if (member == null) return NotFound();

        return new TeamMemberDto
        {
            Id = member.Id,
            FullName = member.FullName,
            Role = member.Role,
            Bio = member.Bio,
            PhotoUrl = member.PhotoUrl,
            SortOrder = member.SortOrder,
            IsPublished = member.IsPublished
        };
    }

    [HttpPost]
    public async Task<ActionResult<TeamMemberDto>> Create(TeamMemberUpsertRequest request)
    {
        var member = new TeamMember
        {
            FullName = request.FullName,
            Role = request.Role,
            Bio = request.Bio,
            PhotoUrl = request.PhotoUrl,
            SortOrder = request.SortOrder,
            IsPublished = request.IsPublished
        };

        _db.TeamMembers.Add(member);
        await _db.SaveChangesAsync();

        return Ok(new TeamMemberDto { Id = member.Id, FullName = member.FullName });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TeamMemberUpsertRequest request)
    {
        var member = await _db.TeamMembers.FindAsync(id);
        if (member == null) return NotFound();

        member.FullName = request.FullName;
        member.Role = request.Role;
        member.Bio = request.Bio;
        member.PhotoUrl = request.PhotoUrl;
        member.SortOrder = request.SortOrder;
        member.IsPublished = request.IsPublished;
        member.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var member = await _db.TeamMembers.FindAsync(id);
        if (member == null) return NotFound();

        _db.TeamMembers.Remove(member);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("upload")]
    public async Task<ActionResult<string>> Upload([FromForm] IFormFile file)
    {
        try
        {
            if (file == null || file.Length <= 0) return BadRequest("فایل خالی است");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var relativeFolder = "uploads/team";
            var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var uploadsRoot = Path.Combine(wwwroot, relativeFolder);

            if (!Directory.Exists(uploadsRoot)) Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsRoot, fileName);

            using (var outStream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(outStream);
            }

            return Ok($"/{relativeFolder}/{fileName}");
        }
        catch (Exception ex)
        {
            return BadRequest($"آپلود ناموفق بود: {ex.Message}");
        }
    }
}
