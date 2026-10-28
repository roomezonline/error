using ErrorService.Server.Data;
using ErrorService.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/site-stats")]
public sealed class SiteStatsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public SiteStatsController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [OutputCache(Duration = 600, Tags = new[] { "error-codes", "academy" })]
    public async Task<ActionResult<SiteStatsDto>> Get()
    {
        var completedServices = await _db.CustomerReceipts.AsNoTracking().CountAsync();

        // تجمعی همهٔ مانیتورینگ‌های ایجادشده در تمام کارگاه‌ها؛ چون حذف‌ها فیزیکی است،
        // بزرگ‌ترین شناسهٔ صادرشده و شناسه‌های بایگانی مبنای «همهٔ سوابق تا امروز» قرار می‌گیرند
        // تا آمار با حذف یا پایان یک مانیتورینگ کاهش پیدا نکند.
        var monitoringRows = await _db.MonitoringReceiptConnections.AsNoTracking().CountAsync();
        var maxConnectionId = await _db.MonitoringReceiptConnections.AsNoTracking()
            .MaxAsync(c => (int?)c.Id) ?? 0;
        var maxArchivedConnectionId = await _db.MonitoringDataArchives.AsNoTracking()
            .MaxAsync(a => (int?)a.MonitoringReceiptConnectionId) ?? 0;
        var monitoring = Math.Max(monitoringRows, Math.Max(maxConnectionId, maxArchivedConnectionId));

        var errorCodes = await _db.ErrorCodes.AsNoTracking().CountAsync();
        var courses = await _db.TrainingCourses.AsNoTracking().CountAsync(c => c.IsPublished);
        var articles = await _db.TrainingArticles.AsNoTracking().CountAsync(a => a.IsPublished);

        return Ok(new SiteStatsDto
        {
            MonitoringCount = monitoring,
            ErrorCodesCount = errorCodes,
            LearningCount = courses + articles,
            CompletedServicesCount = completedServices
        });
    }
}
