using System.Security.Claims;
using System.Text.Json;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/monitoring-receipts")]
[Authorize]
public sealed class MonitoringReceiptConnectionsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly MonitoringCacheService _monitoringCache;

    public MonitoringReceiptConnectionsController(ErrorServiceDbContext db, IMemoryCache cache, MonitoringCacheService monitoringCache)
    {
        _db = db;
        _cache = cache;
        _monitoringCache = monitoringCache;
    }

    private bool IsSuperAdmin() => User.IsInRole("super_admin");

    private int? GetWorkshopUserId()
    {
        var str = User.FindFirst("workshop_user_id")?.Value;
        if (int.TryParse(str, out var id)) return id;
        return null;
    }

    private string? GetUserDisplayName()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value;
    }

    [Authorize(Policy = "perm:admin.monitoring.view")]
    [HttpGet("active-devices")]
    public async Task<ActionResult<List<MonitoringDeviceDto>>> GetActiveDevicesForReceipt([FromQuery] int receiptId)
    {
        if (receiptId <= 0) return BadRequest("رسید معتبر نیست");

        var receipt = await _db.CustomerReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == receiptId);

        if (receipt == null) return NotFound();

        if (!IsSuperAdmin())
        {
            var targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (receipt.WorkshopId != targetWorkshopId)
                return Forbid();
        }

        var now = DateTimeOffset.UtcNow;

        var assignedDeviceIds = await _db.MonitoringDeviceAssignments
            .AsNoTracking()
            .Where(x => x.WorkshopId == receipt.WorkshopId && x.StartAt <= now && (x.EndAt == null || x.EndAt > now))
            .Select(x => x.MonitoringDeviceId)
            .ToListAsync();

        // Exclude devices already connected to any active receipt
        var busyDeviceIds = await _db.MonitoringReceiptConnections
            .AsNoTracking()
            .Where(x => x.EndedAt == null && assignedDeviceIds.Contains(x.MonitoringDeviceId))
            .Select(x => x.MonitoringDeviceId)
            .ToListAsync();

        var freeDeviceIds = assignedDeviceIds.Except(busyDeviceIds).ToList();

        var list = await _db.MonitoringDevices
            .AsNoTracking()
            .Where(d => freeDeviceIds.Contains(d.Id) && d.IsActive)
            .OrderBy(d => d.Title)
            .Select(d => new MonitoringDeviceDto
            {
                Id = d.Id,
                Title = d.Title,
                DeviceNumber = d.DeviceNumber,
                IsActive = d.IsActive,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    [Authorize(Policy = "perm:admin.monitoring.connections.manage")]
    [HttpPost("connect")]
    public async Task<ActionResult<MonitoringReceiptConnectionDto>> Connect([FromBody] MonitoringReceiptConnectRequest req)
    {
        if (req == null) return BadRequest("درخواست نامعتبر است");
        if (req.CustomerReceiptId <= 0) return BadRequest("رسید معتبر نیست");
        if (req.MonitoringDeviceId <= 0) return BadRequest("دستگاه مانیتورینگ معتبر نیست");

        var receipt = await _db.CustomerReceipts
            .Include(x => x.Customer)
            .Include(x => x.DeviceType)
            .Include(x => x.DeviceBrand)
            .Include(x => x.Workshop)
            .FirstOrDefaultAsync(x => x.Id == req.CustomerReceiptId);

        if (receipt == null) return NotFound("رسید یافت نشد");

        if (!IsSuperAdmin())
        {
            var targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (receipt.WorkshopId != targetWorkshopId)
                return Forbid();
        }

        var now = DateTimeOffset.UtcNow;

        var deviceOk = await _db.MonitoringDeviceAssignments
            .AsNoTracking()
            .AnyAsync(x => x.WorkshopId == receipt.WorkshopId
                           && x.MonitoringDeviceId == req.MonitoringDeviceId
                           && x.StartAt <= now
                           && (x.EndAt == null || x.EndAt > now));

        if (!deviceOk) return BadRequest("این دستگاه مانیتورینگ برای این کارگاه فعال نیست");

        var device = await _db.MonitoringDevices.FirstOrDefaultAsync(x => x.Id == req.MonitoringDeviceId);
        if (device == null) return BadRequest("دستگاه مانیتورینگ یافت نشد");
        if (!device.IsActive) return BadRequest("دستگاه مانیتورینگ غیرفعال است");

        // Check if device is already connected to another active receipt
        var isDeviceBusy = await _db.MonitoringReceiptConnections
            .AnyAsync(x => x.MonitoringDeviceId == req.MonitoringDeviceId
                        && x.CustomerReceiptId != req.CustomerReceiptId
                        && x.EndedAt == null);
        if (isDeviceBusy) return BadRequest("این دستگاه مانیتورینگ در حال حاضر به رسید دیگری متصل است");

        // Check if device is already connected to THIS receipt (avoid duplicate)
        var alreadyConnectedToThis = await _db.MonitoringReceiptConnections
            .AnyAsync(x => x.MonitoringDeviceId == req.MonitoringDeviceId
                        && x.CustomerReceiptId == req.CustomerReceiptId
                        && x.EndedAt == null);
        if (alreadyConnectedToThis) return BadRequest("این دستگاه قبلاً به این رسید متصل شده است");

        // Check workshop quota: active + archived connections
        var activeCount = await _db.MonitoringReceiptConnections
            .CountAsync(x => x.WorkshopId == receipt.WorkshopId && x.EndedAt == null);
        var archiveCount = await _db.MonitoringDataArchives
            .CountAsync(a => a.WorkshopId == receipt.WorkshopId);
        var totalWorkshopConnections = activeCount + archiveCount;

        var maxConnections = receipt.Workshop.MaxMonitoringConnections;
        if (totalWorkshopConnections >= maxConnections)
            return BadRequest(new { code = "WORKSHOP_QUOTA_EXCEEDED", message = $"ظرفیت مانیتورینگ این کارگاه تکمیل است. حداکثر {maxConnections} مانیتورینگ مجاز می‌باشد. برای شروع جدید، ابتدا دیتای یکی از مانیتورینگ‌های قدیمی را دانلود و سپس حذف کنید.", max = maxConnections, current = totalWorkshopConnections });

        // End previous active connections for this receipt (single active link per receipt)
        var prev = await _db.MonitoringReceiptConnections
            .Where(x => x.CustomerReceiptId == receipt.Id && x.EndedAt == null)
            .ToListAsync();

        foreach (var p in prev)
            p.EndedAt = now;

        var entity = new MonitoringReceiptConnection
        {
            CustomerReceiptId = receipt.Id,
            WorkshopId = receipt.WorkshopId,
            MonitoringDeviceId = device.Id,
            CreatedByUserId = GetWorkshopUserId(),
            CreatedByUserName = GetUserDisplayName(),
            CreatedAt = now,
            EndedAt = null
        };

        _db.MonitoringReceiptConnections.Add(entity);
        await _db.SaveChangesAsync();

        // Clear stale device data — keep only setting/ folder
        var deviceCode = device.DeviceNumber;
        _monitoringCache.Remove(deviceCode);
        var deviceDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitoring-data", deviceCode);
        if (Directory.Exists(deviceDir))
        {
            foreach (var sub in Directory.GetDirectories(deviceDir))
            {
                var dirName = Path.GetFileName(sub);
                if (dirName.Equals("setting", StringComparison.OrdinalIgnoreCase))
                    continue;
                Directory.Delete(sub, recursive: true);
            }
        }

        var customerDeviceTitle = receipt.DeviceType.Name + (receipt.DeviceBrand != null ? $" - {receipt.DeviceBrand.Name}" : "");

        return Ok(new MonitoringReceiptConnectionDto
        {
            Id = entity.Id,
            CustomerReceiptId = entity.CustomerReceiptId,
            WorkshopId = entity.WorkshopId,
            WorkshopName = receipt.Workshop.WorkshopName,
            MonitoringDeviceId = entity.MonitoringDeviceId,
            MonitoringDeviceTitle = device.Title,
            MonitoringDeviceNumber = device.DeviceNumber,
            CustomerFullName = $"{receipt.Customer.FirstName} {receipt.Customer.LastName}",
            CustomerMobile = receipt.Customer.Mobile,
            CustomerDeviceTitle = customerDeviceTitle,
            ProblemDescription = receipt.ProblemDescription,
            ReceiptImageUrl = receipt.ReceiptImageUrl,
            CreatedByUserId = entity.CreatedByUserId,
            CreatedByUserName = entity.CreatedByUserName,
            CreatedAt = entity.CreatedAt,
            CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(entity.CreatedAt, includeTime: true),
            EndedAt = entity.EndedAt,
            EndedAtFa = null,
            IsActive = true
        });
    }

    [Authorize(Policy = "perm:admin.monitoring.view")]
    [HttpGet("active")]
    public async Task<ActionResult<List<MonitoringReceiptConnectionDto>>> GetActive([FromQuery] int? workshopId = null)
    {
        var isSuperAdmin = IsSuperAdmin();
        int? targetWorkshopId;

        if (!isSuperAdmin)
        {
            targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
        }
        else
        {
            targetWorkshopId = workshopId;
        }

        var q = _db.MonitoringReceiptConnections
            .AsNoTracking()
            .Include(x => x.Workshop)
            .Include(x => x.MonitoringDevice)
            .Include(x => x.CustomerReceipt)
                .ThenInclude(x => x.Customer)
            .Include(x => x.CustomerReceipt)
                .ThenInclude(x => x.DeviceType)
            .Include(x => x.CustomerReceipt)
                .ThenInclude(x => x.DeviceBrand)
            .Where(x => x.EndedAt == null)
            .AsQueryable();

        if (targetWorkshopId.HasValue && targetWorkshopId.Value > 0)
            q = q.Where(x => x.WorkshopId == targetWorkshopId.Value);

        var raw = await q
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync();

        var deviceCodes = raw.Select(x => x.MonitoringDevice.DeviceNumber).Where(d => !string.IsNullOrEmpty(d)).ToList();
        var alertCounts = await _db.MonitoringAlerts
            .AsNoTracking()
            .Where(a => deviceCodes.Contains(a.DeviceCode) && a.State)
            .GroupBy(a => a.DeviceCode)
            .Select(g => new { DeviceCode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DeviceCode, x => x.Count);

        var result = raw.Select(x => new MonitoringReceiptConnectionDto
        {
            Id = x.Id,
            CustomerReceiptId = x.CustomerReceiptId,
            WorkshopId = x.WorkshopId,
            WorkshopName = x.Workshop.WorkshopName,
            MonitoringDeviceId = x.MonitoringDeviceId,
            MonitoringDeviceTitle = x.MonitoringDevice.Title,
            MonitoringDeviceNumber = x.MonitoringDevice.DeviceNumber,
            CustomerFullName = x.CustomerReceipt.Customer.FirstName + " " + x.CustomerReceipt.Customer.LastName,
            CustomerMobile = x.CustomerReceipt.Customer.Mobile,
            CustomerDeviceTitle = x.CustomerReceipt.DeviceType.Name + (x.CustomerReceipt.DeviceBrand != null ? " - " + x.CustomerReceipt.DeviceBrand.Name : ""),
            ProblemDescription = x.CustomerReceipt.ProblemDescription,
            ReceiptImageUrl = x.CustomerReceipt.ReceiptImageUrl,
            CreatedByUserId = x.CreatedByUserId,
            CreatedByUserName = x.CreatedByUserName,
            CreatedAt = x.CreatedAt,
            CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(x.CreatedAt, true),
            EndedAt = x.EndedAt,
            EndedAtFa = x.EndedAt.HasValue ? PersianDateHelper.ToPersianDateTimeString(x.EndedAt.Value, true) : null,
            IsActive = x.EndedAt == null,
            EndReason = x.EndReason,
            IsPoweredOn = _monitoringCache.Get(x.MonitoringDevice.DeviceNumber)?.Priz?.Trim().ToLower() == "on",
            AlertCount = alertCounts.GetValueOrDefault(x.MonitoringDevice.DeviceNumber, 0)
        }).ToList();

        return Ok(result);
    }

    [Authorize(Policy = "perm:admin.monitoring.connections.manage")]
    [HttpPost("{id:int}/end")]
    public async Task<IActionResult> EndConnection(int id, [FromBody] MonitoringReceiptEndRequest request)
    {
        var entity = await _db.MonitoringReceiptConnections
            .Include(e => e.MonitoringDevice)
            .FirstOrDefaultAsync(x => x.Id == id && x.EndedAt == null);

        if (entity == null) return NotFound();

        if (!IsSuperAdmin())
        {
            var targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (entity.WorkshopId != targetWorkshopId)
                return Forbid();
        }

        // Check device power status
        var deviceCode = entity.MonitoringDevice?.DeviceNumber;
        if (!string.IsNullOrEmpty(deviceCode))
        {
            var isPoweredOn = _monitoringCache.Get(deviceCode)?.Priz?.Trim().ToLower() == "on";
            if (isPoweredOn)
                return BadRequest("برق دستگاه وصل است. لطفاً ابتدا برق دستگاه را قطع کنید.");
        }

        // Check workshop connections quota: active + archived connections
        var activeCount = await _db.MonitoringReceiptConnections
            .CountAsync(x => x.WorkshopId == entity.WorkshopId && x.EndedAt == null);
        var archiveCount = await _db.MonitoringDataArchives
            .CountAsync(a => a.WorkshopId == entity.WorkshopId);
        var totalConnections = activeCount + archiveCount;

        var workshop = await _db.Workshops.FindAsync(entity.WorkshopId);
        var maxConnections = workshop?.MaxMonitoringConnections ?? 2;
        if (totalConnections > maxConnections)
            return BadRequest($"تعداد کل مانیتورینگ‌های این کارگاه ({totalConnections}) بیش از حد مجاز ({maxConnections}) است. لطفاً ابتدا یک مانیتورینگ را حذف کنید.");

        entity.EndedAt = DateTimeOffset.UtcNow;
        entity.EndReason = request.EndReason?.Trim();
        await _db.SaveChangesAsync();

        // Create archive
        var recordCount = await _db.MonitoringDataRecords
            .CountAsync(r => r.MonitoringId == entity.Id);

        var device = await _db.MonitoringDevices
            .FirstOrDefaultAsync(d => d.Id == entity.MonitoringDeviceId);

        var archive = new MonitoringDataArchive
        {
            WorkshopId = entity.WorkshopId,
            MonitoringReceiptConnectionId = entity.Id,
            CustomerReceiptId = entity.CustomerReceiptId,
            DeviceCode = device?.DeviceNumber ?? "",
            StartedAt = entity.CreatedAt.DateTime,
            EndedAt = DateTime.UtcNow,
            RecordCount = recordCount,
            EstimatedBytes = recordCount * 300L,
            Downloaded = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.MonitoringDataArchives.Add(archive);
        await _db.SaveChangesAsync();

        // Clear device data files and cache — device will get 404 on next send
        if (device != null)
        {
            _monitoringCache.Remove(device.DeviceNumber);
            var deviceDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitoring-data", device.DeviceNumber);
            if (Directory.Exists(deviceDir))
            {
                foreach (var sub in Directory.GetDirectories(deviceDir))
                {
                    var dirName = Path.GetFileName(sub);
                    if (dirName.Equals("setting", StringComparison.OrdinalIgnoreCase))
                        continue;
                    Directory.Delete(sub, recursive: true);
                }
            }
        }

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.monitoring.connections.manage")]
    [HttpDelete("{id:int}/hard")]
    public async Task<IActionResult> DeleteConnection(int id)
    {
        var entity = await _db.MonitoringReceiptConnections
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null) return NotFound();

        if (!IsSuperAdmin())
        {
            var targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (entity.WorkshopId != targetWorkshopId)
                return Forbid();
        }

        var device = await _db.MonitoringDevices
            .FirstOrDefaultAsync(d => d.Id == entity.MonitoringDeviceId);

        // Check device power status
        if (device != null)
        {
            var isPoweredOn = _monitoringCache.Get(device.DeviceNumber)?.Priz?.Trim().ToLower() == "on";
            if (isPoweredOn)
                return BadRequest("برق دستگاه وصل است. لطفاً ابتدا برق دستگاه را قطع کنید.");
        }

        var dataRecords = await _db.MonitoringDataRecords
            .Where(x => x.MonitoringId == id).ToListAsync();
        _db.MonitoringDataRecords.RemoveRange(dataRecords);

        var changeLogs = await _db.MonitoringDeviceChangeLogs
            .Where(x => x.MonitoringId == id).ToListAsync();
        _db.MonitoringDeviceChangeLogs.RemoveRange(changeLogs);

        var archive = await _db.MonitoringDataArchives
            .FirstOrDefaultAsync(a => a.MonitoringReceiptConnectionId == id);
        if (archive != null)
            _db.MonitoringDataArchives.Remove(archive);

        var alerts = await _db.MonitoringAlerts
            .Where(a => a.MonitoringId == id).ToListAsync();
        _db.MonitoringAlerts.RemoveRange(alerts);

        _db.MonitoringReceiptConnections.Remove(entity);
        await _db.SaveChangesAsync();

        // Clear device data files and cache
        if (device != null)
        {
            _monitoringCache.Remove(device.DeviceNumber);
            var deviceDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "monitoring-data", device.DeviceNumber);
            if (Directory.Exists(deviceDir))
            {
                foreach (var sub in Directory.GetDirectories(deviceDir))
                {
                    var dirName = Path.GetFileName(sub);
                    if (dirName.Equals("setting", StringComparison.OrdinalIgnoreCase))
                        continue;
                    Directory.Delete(sub, recursive: true);
                }
            }
        }

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.monitoring.devices.manage")]
    [HttpDelete("{id:int}/records")]
    public async Task<IActionResult> DeleteMonitoringRecords(int id)
    {
        var entity = await _db.MonitoringReceiptConnections
            .Include(e => e.MonitoringDevice)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null) return NotFound();

        if (!IsSuperAdmin())
        {
            var targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (entity.WorkshopId != targetWorkshopId)
                return Forbid();
        }

        var changeLogs = await _db.MonitoringDeviceChangeLogs
            .Where(x => x.MonitoringId == id).ToListAsync();
        _db.MonitoringDeviceChangeLogs.RemoveRange(changeLogs);

        var dataRecords = await _db.MonitoringDataRecords
            .Where(x => x.MonitoringId == id).ToListAsync();
        _db.MonitoringDataRecords.RemoveRange(dataRecords);

        await _db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.monitoring.view")]
    [HttpGet("workshop-capacity")]
    public async Task<ActionResult<object>> GetWorkshopCapacity([FromQuery] int workshopId)
    {
        var workshop = await _db.Workshops.FindAsync(workshopId);
        if (workshop == null) return NotFound("کارگاه یافت نشد");

        var activeCount = await _db.MonitoringReceiptConnections
            .CountAsync(x => x.WorkshopId == workshopId && x.EndedAt == null);
        var archiveCount = await _db.MonitoringDataArchives
            .CountAsync(a => a.WorkshopId == workshopId);
        var used = activeCount + archiveCount;

        var max = workshop.MaxMonitoringConnections;

        return Ok(new
        {
            workshopId,
            used,
            max,
            canConnect = used < max
        });
    }

    [Authorize(Policy = "perm:admin.monitoring.view")]
    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> ExportConnectionData(int id)
    {
        var entity = await _db.MonitoringReceiptConnections
            .AsNoTracking()
            .Include(x => x.MonitoringDevice)
            .Include(x => x.CustomerReceipt)
                .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entity == null) return NotFound();

        if (!IsSuperAdmin())
        {
            var targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (entity.WorkshopId != targetWorkshopId)
                return Forbid();
        }

        var records = await _db.MonitoringDataRecords
            .AsNoTracking()
            .Where(r => r.MonitoringId == id)
            .OrderBy(r => r.Timestamp)
            .ToListAsync();

        var deviceName = entity.MonitoringDevice?.Title ?? "مشخص نشده";
        var customerName = entity.CustomerReceipt?.Customer != null
            ? $"{entity.CustomerReceipt.Customer.FirstName} {entity.CustomerReceipt.Customer.LastName}".Trim()
            : "";
        var customerMobile = entity.CustomerReceipt?.Customer?.Mobile ?? "";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# ConnectionId: {id}");
        sb.AppendLine($"# CustomerReceiptId: {entity.CustomerReceiptId}");
        sb.AppendLine($"# CustomerName: {customerName}");
        sb.AppendLine($"# CustomerMobile: {customerMobile}");
        sb.AppendLine($"# MonitoringDeviceTitle: {deviceName}");
        sb.AppendLine($"# MonitoringDeviceNumber: {entity.MonitoringDevice?.DeviceNumber ?? ""}");
        sb.AppendLine("Timestamp,TimestampFa,TemperatureRef,TemperatureFreez,MotorState,Bargh,Element1,Element2,Fdc1,Fac1,Jaryan,Power,Kw,SumKw,State,Note");

        foreach (var r in records)
        {
            sb.AppendLine($"{r.Timestamp:O},{r.TimestampFa},{r.TemperatureRef},{r.TemperatureFreez},{r.MotorState},{r.Bargh},{r.Element1},{r.Element2},{r.Fdc1},{r.Fac1},{r.Jaryan},{r.Power},{r.Kw},{r.SumKw},{r.State},{r.Note}");
        }

        return File(
            System.Text.Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv",
            $"monitoring_{id}_{deviceName}.csv"
        );
    }

    [Authorize(Policy = "perm:admin.monitoring.connections.manage")]
    [HttpPost("upload-csv")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadCsv([FromQuery] int connectionId, IFormFile file)
    {
        if (connectionId <= 0) return BadRequest("شناسه مانیتورینگ معتبر نیست");
        if (file == null || file.Length == 0) return BadRequest("فایل CSV ارسال نشد");

        var entity = await _db.MonitoringReceiptConnections
            .AsNoTracking()
            .Include(x => x.MonitoringDevice)
            .Include(x => x.CustomerReceipt).ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == connectionId);

        if (entity == null) return NotFound("مانیتورینگ یافت نشد");

        if (!IsSuperAdmin())
        {
            var targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
            if (entity.WorkshopId != targetWorkshopId)
                return Forbid();
        }

        using var reader = new StreamReader(file.OpenReadStream());
        string? csvConnectionId = null;
        string? csvCustomerReceiptId = null;
        string? csvCustomerName = null;
        string? csvCustomerMobile = null;
        string? csvDeviceTitle = null;
        string? csvDeviceNumber = null;
        var dataLines = new List<string>();
        string? headerLine = null;

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (line.StartsWith('#'))
            {
                if (line.StartsWith("# ConnectionId: ")) csvConnectionId = line["# ConnectionId: ".Length..].Trim();
                else if (line.StartsWith("# CustomerReceiptId: ")) csvCustomerReceiptId = line["# CustomerReceiptId: ".Length..].Trim();
                else if (line.StartsWith("# CustomerName: ")) csvCustomerName = line["# CustomerName: ".Length..].Trim();
                else if (line.StartsWith("# CustomerMobile: ")) csvCustomerMobile = line["# CustomerMobile: ".Length..].Trim();
                else if (line.StartsWith("# MonitoringDeviceTitle: ")) csvDeviceTitle = line["# MonitoringDeviceTitle: ".Length..].Trim();
                else if (line.StartsWith("# MonitoringDeviceNumber: ")) csvDeviceNumber = line["# MonitoringDeviceNumber: ".Length..].Trim();
                continue;
            }

            if (headerLine == null)
            {
                headerLine = line;
                continue;
            }

            dataLines.Add(line);
        }

        // Prepare current connection details for diagnostics
        var actualCustomerName = entity.CustomerReceipt?.Customer != null
            ? $"{entity.CustomerReceipt.Customer.FirstName} {entity.CustomerReceipt.Customer.LastName}".Trim()
            : "";
        var currentDeviceName = entity.MonitoringDevice?.Title ?? "";
        var currentDeviceNumber = entity.MonitoringDevice?.DeviceNumber ?? "";

        // Validate connection ID matches (with detailed comparison)
        var expectedConnId = connectionId.ToString();
        if (csvConnectionId != expectedConnId)
        {
            return Conflict(new
            {
                error = "این فایل مربوط به مانیتورینگ دیگری است",
                csvDetails = new
                {
                    csvConnectionId = csvConnectionId ?? "پیدا نشد",
                    csvCustomerReceiptId = csvCustomerReceiptId ?? "پیدا نشد",
                    csvCustomerName = csvCustomerName ?? "پیدا نشد",
                    csvCustomerMobile = csvCustomerMobile ?? "پیدا نشد",
                    csvDeviceTitle = csvDeviceTitle ?? "پیدا نشد",
                    csvDeviceNumber = csvDeviceNumber ?? "پیدا نشد"
                },
                currentDetails = new
                {
                    connectionId = entity.Id,
                    customerReceiptId = entity.CustomerReceiptId,
                    customerName = actualCustomerName,
                    customerMobile = entity.CustomerReceipt?.Customer?.Mobile ?? "",
                    deviceTitle = currentDeviceName,
                    deviceNumber = currentDeviceNumber
                }
            });
        }

        // Validate customer name (with detailed comparison)
        if (!string.IsNullOrWhiteSpace(csvCustomerName) && !string.IsNullOrWhiteSpace(actualCustomerName)
            && !string.Equals(csvCustomerName, actualCustomerName, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new
            {
                error = "نام مشتری در فایل با مانیتورینگ جاری مطابقت ندارد",
                csvDetails = new
                {
                    csvConnectionId = csvConnectionId ?? "پیدا نشد",
                    csvCustomerReceiptId = csvCustomerReceiptId ?? "پیدا نشد",
                    csvCustomerName = csvCustomerName ?? "پیدا نشد",
                    csvCustomerMobile = csvCustomerMobile ?? "پیدا نشد",
                    csvDeviceTitle = csvDeviceTitle ?? "پیدا نشد",
                    csvDeviceNumber = csvDeviceNumber ?? "پیدا نشد"
                },
                currentDetails = new
                {
                    connectionId = entity.Id,
                    customerReceiptId = entity.CustomerReceiptId,
                    customerName = actualCustomerName,
                    customerMobile = entity.CustomerReceipt?.Customer?.Mobile ?? "",
                    deviceTitle = currentDeviceName,
                    deviceNumber = currentDeviceNumber
                }
            });
        }

        // Parse records
        var records = new List<MonitoringDataRecord>();
        foreach (var dataLine in dataLines)
        {
            if (string.IsNullOrWhiteSpace(dataLine)) continue;
            var parts = dataLine.Split(',');
            if (parts.Length < 3) continue;

            var record = new MonitoringDataRecord
            {
                MonitoringId = connectionId,
                Timestamp = DateTime.TryParse(parts[0], out var ts) ? ts : DateTime.UtcNow,
                TimestampFa = parts.Length > 1 ? parts[1] : "",
                TemperatureRef = float.TryParse(parts.Length > 2 ? parts[2] : "", out var tr) ? tr : null,
                TemperatureFreez = float.TryParse(parts.Length > 3 ? parts[3] : "", out var tf) ? tf : null,
                MotorState = parts.Length > 4 && (parts[4] == "True" || parts[4] == "true" || parts[4] == "1"),
                Bargh = parts.Length > 5 && (parts[5] == "True" || parts[5] == "true" || parts[5] == "1"),
                Element1 = parts.Length > 6 && (parts[6] == "True" || parts[6] == "true" || parts[6] == "1"),
                Element2 = parts.Length > 7 && (parts[7] == "True" || parts[7] == "true" || parts[7] == "1"),
                Fdc1 = parts.Length > 8 && (parts[8] == "True" || parts[8] == "true" || parts[8] == "1"),
                Fac1 = parts.Length > 9 && (parts[9] == "True" || parts[9] == "true" || parts[9] == "1"),
                Jaryan = float.TryParse(parts.Length > 10 ? parts[10] : "", out var jr) ? jr : null,
                Power = float.TryParse(parts.Length > 11 ? parts[11] : "", out var pw) ? pw : null,
                Kw = float.TryParse(parts.Length > 12 ? parts[12] : "", out var kw) ? kw : null,
                SumKw = float.TryParse(parts.Length > 13 ? parts[13] : "", out var sk) ? sk : null,
                State = parts.Length > 14 ? parts[14] : null,
                Note = parts.Length > 15 ? parts[15] : null
            };
            records.Add(record);
        }

        if (records.Count == 0)
            return BadRequest("فایل CSV معتبر نیست یا داده‌ای ندارد");

        // Store in memory cache with metadata
        var csvKey = Guid.NewGuid().ToString("N");
        var cacheEntry = new CsvCacheEntry
        {
            ConnectionId = connectionId,
            CustomerReceiptId = entity.CustomerReceiptId,
            CustomerName = actualCustomerName,
            CustomerMobile = entity.CustomerReceipt?.Customer?.Mobile ?? "",
            MonitoringDeviceTitle = entity.MonitoringDevice?.Title ?? "",
            MonitoringDeviceNumber = entity.MonitoringDevice?.DeviceNumber ?? "",
            Records = records
        };
        _cache.Set(csvKey, cacheEntry, TimeSpan.FromHours(2));

        // Also persist to DB so WebForms chart1.aspx can read it (different process)
        var jsonOptions = new JsonSerializerOptions
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var jsonData = JsonSerializer.Serialize(cacheEntry, jsonOptions);
        _db.CachedCsvData.Add(new CachedCsvData
        {
            Key = csvKey,
            Data = jsonData,
            CreatedAt = DateTime.UtcNow
        });

        // Cleanup entries older than 2 hours
        var cutoff = DateTime.UtcNow.AddHours(-2);
        var stale = await _db.CachedCsvData.Where(x => x.CreatedAt < cutoff).ToListAsync();
        if (stale.Count > 0)
        {
            _db.CachedCsvData.RemoveRange(stale);
        }

        await _db.SaveChangesAsync();

        return Ok(new { csvKey, recordCount = records.Count });
    }

    [Authorize(Policy = "perm:admin.monitoring.connections.manage")]
    [HttpGet("csv-cache/{csvKey}")]
    public async Task<IActionResult> GetCsvCache(string csvKey)
    {
        if (string.IsNullOrWhiteSpace(csvKey))
            return BadRequest("csvKey الزامی است");

        var entry = await _db.CachedCsvData
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == csvKey);

        if (entry == null)
            return NotFound("داده‌ای در حافظه موقت یافت نشد. لطفاً دوباره فایل CSV را بارگذاری کنید.");

        var jsonOptions = new JsonSerializerOptions
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var data = JsonSerializer.Deserialize<CsvCacheEntry>(entry.Data, jsonOptions);
        return Ok(data);
    }

    [Authorize(Policy = "perm:admin.monitoring.view")]
    [HttpGet("by-receipt/{receiptId:int}")]
    public async Task<ActionResult<List<MonitoringReceiptConnectionDto>>> GetByReceipt(int receiptId, [FromQuery] int? workshopId = null)
    {
        var isSuperAdmin = IsSuperAdmin();
        int? targetWorkshopId;

        if (!isSuperAdmin)
        {
            targetWorkshopId = ClaimsHelper.GetWorkshopId(User);
        }
        else
        {
            targetWorkshopId = workshopId;
        }

        var receipt = await _db.CustomerReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == receiptId);

        if (receipt == null) return NotFound();

        if (targetWorkshopId.HasValue && targetWorkshopId.Value > 0 && receipt.WorkshopId != targetWorkshopId.Value)
            return Forbid();

        var connections = await _db.MonitoringReceiptConnections
            .AsNoTracking()
            .Include(x => x.Workshop)
            .Include(x => x.MonitoringDevice)
            .Include(x => x.CustomerReceipt)
                .ThenInclude(x => x.Customer)
            .Include(x => x.CustomerReceipt)
                .ThenInclude(x => x.DeviceType)
            .Include(x => x.CustomerReceipt)
                .ThenInclude(x => x.DeviceBrand)
            .Where(x => x.CustomerReceiptId == receiptId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var result = connections.Select(x => new MonitoringReceiptConnectionDto
        {
            Id = x.Id,
            CustomerReceiptId = x.CustomerReceiptId,
            WorkshopId = x.WorkshopId,
            WorkshopName = x.Workshop.WorkshopName,
            MonitoringDeviceId = x.MonitoringDeviceId,
            MonitoringDeviceTitle = x.MonitoringDevice.Title,
            MonitoringDeviceNumber = x.MonitoringDevice.DeviceNumber,
            CustomerFullName = x.CustomerReceipt.Customer.FirstName + " " + x.CustomerReceipt.Customer.LastName,
            CustomerMobile = x.CustomerReceipt.Customer.Mobile,
            CustomerDeviceTitle = x.CustomerReceipt.DeviceType.Name + (x.CustomerReceipt.DeviceBrand != null ? " - " + x.CustomerReceipt.DeviceBrand.Name : ""),
            ProblemDescription = x.CustomerReceipt.ProblemDescription,
            ReceiptImageUrl = x.CustomerReceipt.ReceiptImageUrl,
            CreatedByUserId = x.CreatedByUserId,
            CreatedByUserName = x.CreatedByUserName,
            CreatedAt = x.CreatedAt,
            CreatedAtFa = PersianDateHelper.ToPersianDateTimeString(x.CreatedAt, true),
            EndedAt = x.EndedAt,
            EndedAtFa = x.EndedAt.HasValue ? PersianDateHelper.ToPersianDateTimeString(x.EndedAt.Value, true) : null,
            IsActive = x.EndedAt == null,
            EndReason = x.EndReason
        }).ToList();

        return Ok(result);
    }
}

public sealed class CsvCacheEntry
{
    public int ConnectionId { get; set; }
    public int CustomerReceiptId { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerMobile { get; set; } = "";
    public string MonitoringDeviceTitle { get; set; } = "";
    public string MonitoringDeviceNumber { get; set; } = "";
    public List<MonitoringDataRecord> Records { get; set; } = new();
}

