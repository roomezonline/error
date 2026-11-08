using ErrorService.Server.Data;
using ErrorService.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/faq")]
public sealed class FaqController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public FaqController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [OutputCache(Duration = 60)]
    public async Task<ActionResult<List<SiteFaqEntryDto>>> Get(CancellationToken ct)
    {
        var items = await _db.SiteFaqEntries
            .AsNoTracking()
            .Where(x => x.IsEnabled)
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
}
