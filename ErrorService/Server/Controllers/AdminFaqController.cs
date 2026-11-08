using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/admin/faq")]
[Authorize(Policy = "perm:admin.faq.manage")]
public sealed class AdminFaqController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public AdminFaqController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<SiteFaqEntryDto>>> GetAll(CancellationToken ct)
    {
        var items = await _db.SiteFaqEntries
            .AsNoTracking()
            .Select(x => new SiteFaqEntryDto
            {
                Id = x.Id,
                Question = x.Question,
                Answer = x.Answer,
                CategoryKey = x.CategoryKey,
                Keywords = x.Keywords,
                SortOrder = x.SortOrder,
                IsEnabled = x.IsEnabled
            })
            .ToListAsync(ct);

        items = items
            .OrderBy(x => SiteFaqCatalog.OrderOf(x.CategoryKey))
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToList();

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<SiteFaqEntryDto>> Create([FromBody] SiteFaqUpsertRequest req, CancellationToken ct)
    {
        var error = Validate(req);
        if (error != null) return BadRequest(error);

        var entity = new SiteFaqEntry
        {
            Question = req.Question.Trim(),
            Answer = req.Answer.Trim(),
            CategoryKey = NormalizeCategory(req.CategoryKey),
            Keywords = string.IsNullOrWhiteSpace(req.Keywords) ? null : req.Keywords.Trim(),
            SortOrder = req.SortOrder,
            IsEnabled = req.IsEnabled,
            CreatedAt = DateTime.Now
        };

        _db.SiteFaqEntries.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(entity));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SiteFaqEntryDto>> Update(int id, [FromBody] SiteFaqUpsertRequest req, CancellationToken ct)
    {
        var error = Validate(req);
        if (error != null) return BadRequest(error);

        var entity = await _db.SiteFaqEntries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();

        entity.Question = req.Question.Trim();
        entity.Answer = req.Answer.Trim();
        entity.CategoryKey = NormalizeCategory(req.CategoryKey);
        entity.Keywords = string.IsNullOrWhiteSpace(req.Keywords) ? null : req.Keywords.Trim();
        entity.SortOrder = req.SortOrder;
        entity.IsEnabled = req.IsEnabled;
        entity.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(entity));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entity = await _db.SiteFaqEntries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return NotFound();

        _db.SiteFaqEntries.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static string? Validate(SiteFaqUpsertRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Question))
            return "متن سوال الزامی است.";
        if (req.Question.Trim().Length > 300)
            return "متن سوال نباید بیشتر از ۳۰۰ کاراکتر باشد.";
        if (string.IsNullOrWhiteSpace(req.Answer))
            return "متن پاسخ الزامی است.";
        if (req.Answer.Trim().Length > 4000)
            return "متن پاسخ نباید بیشتر از ۴۰۰۰ کاراکتر باشد.";
        if (!string.IsNullOrWhiteSpace(req.Keywords) && req.Keywords.Trim().Length > 500)
            return "کلیدواژه‌ها نباید بیشتر از ۵۰۰ کاراکتر باشند.";
        if (req.SortOrder is < 0 or > 9999)
            return "ترتیب نمایش باید بین ۰ تا ۹۹۹۹ باشد.";

        return null;
    }

    private static string NormalizeCategory(string? key)
        => SiteFaqCatalog.Find(key)?.Key ?? SiteFaqCatalog.DefaultCategoryKey;

    private static SiteFaqEntryDto ToDto(SiteFaqEntry x) => new()
    {
        Id = x.Id,
        Question = x.Question,
        Answer = x.Answer,
        CategoryKey = x.CategoryKey,
        Keywords = x.Keywords,
        SortOrder = x.SortOrder,
        IsEnabled = x.IsEnabled
    };
}
