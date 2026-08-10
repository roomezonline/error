using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
public sealed class MonitoringPlansController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public MonitoringPlansController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet("api/monitoring-plans")]
    [Authorize(Policy = "perm:admin.monitoring.view")]
    public async Task<ActionResult<List<MonitoringPlanDto>>> GetAll([FromQuery] bool? activeOnly = null)
    {
        var q = _db.MonitoringPlans.AsNoTracking().AsQueryable();

        if (activeOnly == true)
            q = q.Where(x => x.IsActive);

        var list = await q
            .OrderBy(x => x.Months)
            .Select(x => new MonitoringPlanDto
            {
                Id = x.Id,
                Title = x.Title,
                Months = x.Months,
                Price = x.Price,
                IsActive = x.IsActive,
                CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(x.CreatedAt, false)
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost("api/monitoring-plans")]
    [Authorize(Policy = "perm:admin.monitoring.plans.manage")]
    public async Task<ActionResult<MonitoringPlanDto>> Create([FromBody] MonitoringPlanUpsertRequest request)
    {
        if (await _db.MonitoringPlans.AnyAsync(x => x.Months == request.Months && x.IsActive))
            return BadRequest("پلنی با این تعداد ماه قبلاً ثبت شده است");

        var entity = new MonitoringPlan
        {
            Title = request.Title,
            Months = request.Months,
            Price = request.Price,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.MonitoringPlans.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new MonitoringPlanDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Months = entity.Months,
            Price = entity.Price,
            IsActive = entity.IsActive,
            CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(entity.CreatedAt, false)
        });
    }

    [HttpPut("api/monitoring-plans/{id:int}")]
    [Authorize(Policy = "perm:admin.monitoring.plans.manage")]
    public async Task<ActionResult<MonitoringPlanDto>> Update(int id, [FromBody] MonitoringPlanUpsertRequest request)
    {
        var entity = await _db.MonitoringPlans.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        if (await _db.MonitoringPlans.AnyAsync(x => x.Id != id && x.Months == request.Months && x.IsActive))
            return BadRequest("پلن دیگری با این تعداد ماه فعال است");

        entity.Title = request.Title;
        entity.Months = request.Months;
        entity.Price = request.Price;

        await _db.SaveChangesAsync();

        return Ok(new MonitoringPlanDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Months = entity.Months,
            Price = entity.Price,
            IsActive = entity.IsActive,
            CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(entity.CreatedAt, false)
        });
    }

    [HttpPatch("api/monitoring-plans/{id:int}/toggle")]
    [Authorize(Policy = "perm:admin.monitoring.plans.manage")]
    public async Task<IActionResult> Toggle(int id)
    {
        var entity = await _db.MonitoringPlans.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        entity.IsActive = !entity.IsActive;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("api/monitoring-plans/{id:int}")]
    [Authorize(Policy = "perm:admin.monitoring.plans.manage")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.MonitoringPlans.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        _db.MonitoringPlans.Remove(entity);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
