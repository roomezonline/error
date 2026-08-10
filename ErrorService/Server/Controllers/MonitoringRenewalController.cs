using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
public sealed class MonitoringRenewalController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public MonitoringRenewalController(ErrorServiceDbContext db, IWebHostEnvironment env, IConfiguration configuration)
    {
        _db = db;
        _env = env;
        _configuration = configuration;
    }

    [Authorize(Policy = "perm:admin.monitoring.view")]
    [HttpPost("api/monitoring-devices/assignments/{assignmentId:int}/request-renewal")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<RenewalRequestDto>> RequestRenewal(
        int assignmentId,
        [FromForm] int planId,
        [FromForm] string? note,
        IFormFile? file)
    {
        var plan = await _db.MonitoringPlans.FirstOrDefaultAsync(x => x.Id == planId && x.IsActive);
        if (plan == null)
            return BadRequest("پلن انتخابی یافت نشد یا غیرفعال است");

        var assignment = await _db.MonitoringDeviceAssignments
            .Include(x => x.MonitoringDevice)
            .Include(x => x.Workshop)
            .FirstOrDefaultAsync(x => x.Id == assignmentId);

        if (assignment == null)
            return NotFound("انتساب یافت نشد");

        var isSuperAdmin = User.IsInRole("super_admin");
        if (!isSuperAdmin)
        {
            var userWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (userWorkshopId <= 0) return Forbid();
            if (userWorkshopId != assignment.WorkshopId) return Forbid();
        }

        string? receiptUrl = null;
        if (file != null && file.Length > 0)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
            if (!allowed.Contains(ext))
                return BadRequest("فرمت فایل مجاز نیست. فرمت‌های مجاز: jpg, png, webp, pdf");

            var relativeFolder = _configuration.GetValue<string>("Uploads:RenewalReceiptsRelativePath") ?? "uploads/renewal-receipts";
            var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var dir = Path.Combine(wwwroot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(dir, fileName);
            await using (var stream = System.IO.File.Create(fullPath))
                await file.CopyToAsync(stream);

            receiptUrl = "/" + relativeFolder.Replace("\\", "/") + "/" + fileName;
        }

        var entity = new MonitoringRenewalRequest
        {
            AssignmentId = assignmentId,
            WorkshopId = assignment.WorkshopId,
            PlanMonths = plan.Months,
            MonitoringPlanId = plan.Id,
            PriceAtRequest = plan.Price,
            PaymentReceiptUrl = receiptUrl,
            Note = note,
            Status = RenewalRequestStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.MonitoringRenewalRequests.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new RenewalRequestDto
        {
            Id = entity.Id,
            AssignmentId = entity.AssignmentId,
            DeviceTitle = assignment.MonitoringDevice.Title,
            DeviceNumber = assignment.MonitoringDevice.DeviceNumber,
            WorkshopId = entity.WorkshopId,
            WorkshopName = assignment.Workshop.WorkshopName,
            PlanMonths = entity.PlanMonths,
            PlanTitle = plan.Title,
            PriceAtRequest = entity.PriceAtRequest,
            PaymentReceiptUrl = entity.PaymentReceiptUrl,
            Note = entity.Note,
            Status = entity.Status.ToString(),
            CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(entity.CreatedAt, false)
        });
    }

    [Authorize(Policy = "perm:admin.monitoring.view")]
    [HttpGet("api/monitoring-devices/assignments/{assignmentId:int}/renewal-history")]
    public async Task<ActionResult<List<RenewalRequestDto>>> GetRenewalHistory(int assignmentId)
    {
        var assignment = await _db.MonitoringDeviceAssignments
            .Include(x => x.MonitoringDevice)
            .FirstOrDefaultAsync(x => x.Id == assignmentId);
        if (assignment == null) return NotFound();

        var isSuperAdmin = User.IsInRole("super_admin");
        if (!isSuperAdmin)
        {
            var userWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (userWorkshopId <= 0) return Forbid();
            if (userWorkshopId != assignment.WorkshopId) return Forbid();
        }

        var list = await _db.MonitoringRenewalRequests
            .AsNoTracking()
            .Include(x => x.MonitoringPlan)
            .Where(x => x.AssignmentId == assignmentId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new RenewalRequestDto
            {
                Id = x.Id,
                AssignmentId = x.AssignmentId,
                PlanMonths = x.PlanMonths,
                PlanTitle = x.MonitoringPlan != null ? x.MonitoringPlan.Title : null,
                PriceAtRequest = x.PriceAtRequest,
                PaymentReceiptUrl = x.PaymentReceiptUrl,
                Note = x.Note,
                Status = x.Status.ToString(),
                AdminNote = x.AdminNote,
                CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(x.CreatedAt, false),
                HandledAtFa = x.HandledAt.HasValue ? PersianDateHelper.ToPersianDateTimeString(x.HandledAt.Value, false) : null
            })
            .ToListAsync();

        return Ok(list);
    }

    [Authorize(Policy = "perm:admin.monitoring.devices.manage")]
    [HttpGet("api/monitoring/renewal-requests")]
    public async Task<ActionResult<List<RenewalRequestDto>>> GetRenewalRequests([FromQuery] string? status = null, [FromQuery] int? workshopId = null)
    {
        var isSuperAdmin = User.IsInRole("super_admin");

        if (!isSuperAdmin)
        {
            var userWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (userWorkshopId <= 0) return Forbid();
            workshopId = userWorkshopId;
        }

        var q = _db.MonitoringRenewalRequests
            .AsNoTracking()
            .Include(x => x.Assignment).ThenInclude(a => a.MonitoringDevice)
            .Include(x => x.Workshop)
            .AsQueryable();

        if (workshopId.HasValue && workshopId.Value > 0)
            q = q.Where(x => x.WorkshopId == workshopId.Value);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<RenewalRequestStatus>(status, true, out var statusEnum))
            q = q.Where(x => x.Status == statusEnum);

        var list = await q
            .Include(x => x.MonitoringPlan)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new RenewalRequestDto
            {
                Id = x.Id,
                AssignmentId = x.AssignmentId,
                DeviceTitle = x.Assignment.MonitoringDevice.Title,
                DeviceNumber = x.Assignment.MonitoringDevice.DeviceNumber,
                WorkshopId = x.WorkshopId,
                WorkshopName = x.Workshop.WorkshopName,
                PlanMonths = x.PlanMonths,
                PlanTitle = x.MonitoringPlan != null ? x.MonitoringPlan.Title : null,
                PriceAtRequest = x.PriceAtRequest,
                PaymentReceiptUrl = x.PaymentReceiptUrl,
                Note = x.Note,
                Status = x.Status.ToString(),
                AdminNote = x.AdminNote,
                CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(x.CreatedAt, false),
                HandledAtFa = x.HandledAt.HasValue ? PersianDateHelper.ToPersianDateTimeString(x.HandledAt.Value, false) : null
            })
            .ToListAsync();

        return Ok(list);
    }

    [Authorize(Policy = "perm:admin.monitoring.devices.manage")]
    [HttpPut("api/monitoring/renewal-requests/{id:int}")]
    public async Task<ActionResult<RenewalRequestDto>> HandleRenewalRequest(int id, [FromBody] RenewalRequestActionDto action)
    {
        var entity = await _db.MonitoringRenewalRequests
            .Include(x => x.Assignment)
            .Include(x => x.MonitoringPlan)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null) return NotFound();

        if (!Enum.TryParse<RenewalRequestStatus>(action.Status, true, out var newStatus))
            return BadRequest("وضعیت معتبر نیست");

        if (newStatus == RenewalRequestStatus.Approved)
        {
            var now = DateTimeOffset.UtcNow;
            var currentEndAt = entity.Assignment.EndAt;

            if (currentEndAt.HasValue && currentEndAt.Value > now)
                entity.Assignment.EndAt = currentEndAt.Value.AddMonths(entity.PlanMonths);
            else
                entity.Assignment.EndAt = now.AddMonths(entity.PlanMonths);

            entity.HandledAt = DateTimeOffset.UtcNow;
        }
        else if (newStatus == RenewalRequestStatus.Pending)
        {
            // Undo EndAt extension if reverting from Approved
            if (entity.Status == RenewalRequestStatus.Approved && entity.Assignment.EndAt.HasValue)
            {
                if (entity.Assignment.EndAt.Value > DateTimeOffset.UtcNow)
                    entity.Assignment.EndAt = entity.Assignment.EndAt.Value.AddMonths(-entity.PlanMonths);
                else
                    entity.Assignment.EndAt = null;
            }

            entity.HandledAt = null;
        }
        else if (newStatus == RenewalRequestStatus.Rejected)
        {
            entity.HandledAt = DateTimeOffset.UtcNow;
        }

        entity.Status = newStatus;
        entity.AdminNote = action.AdminNote;

        await _db.SaveChangesAsync();

        return Ok(new RenewalRequestDto
        {
            Id = entity.Id,
            AssignmentId = entity.AssignmentId,
            PlanMonths = entity.PlanMonths,
            PlanTitle = entity.MonitoringPlan?.Title,
            PriceAtRequest = entity.PriceAtRequest,
            PaymentReceiptUrl = entity.PaymentReceiptUrl,
            Note = entity.Note,
            Status = entity.Status.ToString(),
            AdminNote = entity.AdminNote,
            CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(entity.CreatedAt, false),
            HandledAtFa = entity.HandledAt.HasValue ? PersianDateHelper.ToPersianDateTimeString(entity.HandledAt.Value, false) : null
        });
    }

    [Authorize(Policy = "perm:admin.monitoring.devices.manage")]
    [HttpDelete("api/monitoring/renewal-requests/{id:int}")]
    public async Task<IActionResult> DeleteRenewalRequest(int id)
    {
        var entity = await _db.MonitoringRenewalRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null) return NotFound();

        _db.MonitoringRenewalRequests.Remove(entity);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
