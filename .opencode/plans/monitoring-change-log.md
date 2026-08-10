# Plan: Monitoring Device Change Log

## Goal
ثبت خودکار تغییرات المان‌های اصلی (برق، موتور، المنت ۱، المنت ۲) در دیتابیس به همراه snapshot کامل از تمام مقادیر و اطلاعات رسید/کارگاه.

## فایل‌های درگیر

| # | فایل | عملیات |
|---|------|--------|
| 1 | `Server/Models/MonitoringEntities.cs` | + کلاس `MonitoringDeviceChangeLog` |
| 2 | `Server/Data/ErrorServiceDbContext.cs` | + `DbSet<MonitoringDeviceChangeLog>` + config |
| 3 | `Server/Controllers/MonitoringServerController.cs` | + وابستگی‌ها + منطق تغییر |
| 4 | `Server/Controllers/MonitoringChangeLogController.cs` | **جدید** — API لیست تغییرات |
| 5 | `Server/Data/Migrations/` | `dotnet ef migrations add AddMonitoringDeviceChangeLog` |

---

## Step 1 — Entity: `MonitoringDeviceChangeLog`

به انتهای `Server/Models/MonitoringEntities.cs` اضافه شود:

```csharp
public sealed class MonitoringDeviceChangeLog
{
    public int Id { get; set; }

    [Required][MaxLength(50)]
    public string DeviceCode { get; set; } = string.Empty;

    [Required][MaxLength(50)]
    public string ChangeType { get; set; } = string.Empty; // "Priz"/"MotorState"/"Element1"/"Element2"

    [MaxLength(50)]
    public string? OldValue { get; set; } // "on"/"off"

    [MaxLength(50)]
    public string? NewValue { get; set; }

    [Required][MaxLength(200)]
    public string ChangeDescription { get; set; } = string.Empty; // "روشن شدن برق اصلی"

    [Required]
    public string DataSnapshot { get; set; } = string.Empty; // JSON از ۲۲ پارامتر + timestamp

    public int? CustomerReceiptId { get; set; } // از MonitoringReceiptConnection
    public int? WorkshopId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(30)]
    public string CreatedAtFa { get; set; } = string.Empty; // شمسی
}
```

**نکته:** بدون FK (فقط `int?`) چون این جدول لاگ است و نباید به حذف رکوردهای اصلی وابسته باشد.

---

## Step 2 — DbContext: `ErrorServiceDbContext.cs`

### الف) اضافه کردن `DbSet` (بعد از line 73):

```csharp
public DbSet<MonitoringDeviceChangeLog> MonitoringDeviceChangeLogs { get; set; }
```

### ب) اضافه کردن config در `OnModelCreating` (بعد از بلاک MonitoringReceiptConnection):

```csharp
modelBuilder.Entity<MonitoringDeviceChangeLog>(entity =>
{
    entity.HasKey(x => x.Id);
    entity.Property(x => x.DeviceCode).IsRequired().HasMaxLength(50);
    entity.Property(x => x.ChangeType).IsRequired().HasMaxLength(50);
    entity.Property(x => x.OldValue).HasMaxLength(50);
    entity.Property(x => x.NewValue).HasMaxLength(50);
    entity.Property(x => x.ChangeDescription).IsRequired().HasMaxLength(200);
    entity.Property(x => x.DataSnapshot).IsRequired();
    entity.Property(x => x.CreatedAtFa).HasMaxLength(30);

    entity.HasIndex(x => x.DeviceCode);
    entity.HasIndex(x => x.ChangeType);
    entity.HasIndex(x => new { x.DeviceCode, x.CreatedAt });
});
```

### ج) اضافه کردن using:

```csharp
using ErrorService.Server.Models;
```
(اگر قبلاً وجود دارد، نیازی نیست.)

---

## Step 3 — منطق تغییر در `MonitoringServerController.cs`

### الف) تزریق وابستگی‌ها:

```csharp
private readonly MonitoringCacheService _cache;
private readonly ErrorServiceDbContext _db;
private readonly ILogger<MonitoringServerController> _logger;

public MonitoringServerController(MonitoringCacheService cache, ErrorServiceDbContext db, ILogger<MonitoringServerController> logger)
{
    _cache = cache;
    _db = db;
    _logger = logger;
}
```

### ب) قبل از `_cache.Set(deviceCode, data);`:

```csharp
// Detect changes for main elements
var oldData = _cache.Get(deviceCode);
if (oldData != null)
{
    var changes = new List<(string changeType, string? oldVal, string? newVal, string desc)>();

    // Power (Priz)
    if (oldData.Priz != data.Priz)
    {
        var oldP = string.IsNullOrEmpty(oldData.Priz) || oldData.Priz == "off" ? "خاموش" : "روشن";
        var newP = string.IsNullOrEmpty(data.Priz) || data.Priz == "off" ? "خاموش" : "روشن";
        changes.Add(("Priz", oldData.Priz, data.Priz, $"برق اصلی {oldP} → {newP}"));
    }

    // Motor
    if (oldData.MotorState != data.MotorState)
    {
        var oldM = IsOn(oldData.MotorState) ? "روشن" : "خاموش";
        var newM = IsOn(data.MotorState) ? "روشن" : "خاموش";
        changes.Add(("MotorState", oldData.MotorState, data.MotorState, $"موتور {oldM} → {newM}"));
    }

    // Element 1
    if (oldData.Element1 != data.Element1)
    {
        var oldE1 = IsOn(oldData.Element1) ? "روشن" : "خاموش";
        var newE1 = IsOn(data.Element1) ? "روشن" : "خاموش";
        changes.Add(("Element1", oldData.Element1, data.Element1, $"المنت ۱ {oldE1} → {newE1}"));
    }

    // Element 2
    if (oldData.Element2 != data.Element2)
    {
        var oldE2 = IsOn(oldData.Element2) ? "روشن" : "خاموش";
        var newE2 = IsOn(data.Element2) ? "روشن" : "خاموش";
        changes.Add(("Element2", oldData.Element2, data.Element2, $"المنت ۲ {oldE2} → {newE2}"));
    }

    if (changes.Count > 0)
    {
        try
        {
            // Build snapshot from current data
            var snapshotObj = new
            {
                data.Code, data.Priz, data.MotorState, data.Element1, data.Element2,
                data.Temp1, data.Temp2, data.Jaryan, data.CntM, data.CntE1, data.CntE2,
                data.Temp3, data.CurrentType, data.Inverter, data.Voltage, data.Tavan,
                data.Freq, data.Dc1, data.Dc2, data.Ac1, data.Ac2, data.DeviceType,
                Timestamp = DateTime.UtcNow,
                TimestampFa = PersianDateHelper.ToPersianDateTimeString(DateTime.Now, true)
            };
            var snapshotJson = System.Text.Json.JsonSerializer.Serialize(snapshotObj);

            // Look up receipt connection
            int? customerReceiptId = null;
            int? workshopId = null;
            var deviceEntity = await _db.MonitoringDevices
                .FirstOrDefaultAsync(d => d.DeviceNumber == deviceCode);
            if (deviceEntity != null)
            {
                var connection = await _db.MonitoringReceiptConnections
                    .Where(c => c.MonitoringDeviceId == deviceEntity.Id && c.EndedAt == null)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync();
                if (connection != null)
                {
                    customerReceiptId = connection.CustomerReceiptId;
                    workshopId = connection.WorkshopId;
                }
            }

            var nowFa = PersianDateHelper.ToPersianDateTimeString(DateTime.Now, true);

            foreach (var (changeType, oldVal, newVal, desc) in changes)
            {
                _db.MonitoringDeviceChangeLogs.Add(new MonitoringDeviceChangeLog
                {
                    DeviceCode = deviceCode,
                    ChangeType = changeType,
                    OldValue = oldVal,
                    NewValue = newVal,
                    ChangeDescription = desc,
                    DataSnapshot = snapshotJson,
                    CustomerReceiptId = customerReceiptId,
                    WorkshopId = workshopId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedAtFa = nowFa
                });
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save change log for device {Code}", deviceCode);
        }
    }
}
```

### ج) متد کمکی `IsOn` (در همان کلاس):

```csharp
private static bool IsOn(string? val) =>
    val != null && val.Trim().Equals("on", StringComparison.OrdinalIgnoreCase);
```

### د) اضافه کردن usings در بالای فایل:

```csharp
using ErrorService.Server.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
```

---

## Step 4 — API Controller جدید: `MonitoringChangeLogController.cs`

فایل جدید: `Server/Controllers/MonitoringChangeLogController.cs`

```csharp
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/monitoring/change-log")]
public class MonitoringChangeLogController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public MonitoringChangeLogController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? code,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.MonitoringDeviceChangeLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(code))
            query = query.Where(x => x.DeviceCode == code);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.DeviceCode,
                x.ChangeType,
                x.ChangeDescription,
                x.CustomerReceiptId,
                x.WorkshopId,
                x.CreatedAtFa
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var log = await _db.MonitoringDeviceChangeLogs
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.DeviceCode,
                x.ChangeType,
                x.OldValue,
                x.NewValue,
                x.ChangeDescription,
                x.DataSnapshot,
                x.CustomerReceiptId,
                x.WorkshopId,
                x.CreatedAtFa
            })
            .FirstOrDefaultAsync();

        if (log == null) return NotFound();
        return Ok(log);
    }
}
```

---

## Step 5 — Migration

```bash
cd D:\DotNet\Web\errorservice_new\ErrorService
dotnet ef migrations add AddMonitoringDeviceChangeLog --namespace ErrorService.Server.Data.Migrations --project Server --startup-project Server
```

سپس:

```bash
dotnet build
```

اگر build با ۰ خطا شد، کار تمام است. با اجرای بعدی برنامه، migration به صورت خودکار اعمال می‌شود (`db.Database.Migrate()` در `Program.cs`).

---

## جریان کامل اجرا

```
ESP32 → GET /api/monitoring/server?code=123&priz=on&motor_state=on&...
        │
        ├─ ۱. خواندن oldData از cache
        ├─ ۲. مقایسه Priz, MotorState, Element1, Element2
        │
        ├─ اگر تغییری detected:
        │   ├─ ساخت snapshot JSON از ۲۲ پارامتر
        │   ├─ کوئری DB: MonitoringDevice → MonitoringReceiptConnection فعال
        │   ├─ ساخت و SaveAsync رکورد MonitoringDeviceChangeLog
        │   └─ (خطا در DB لاگ → فقط warning، break نمی‌کند)
        │
        ├─ ۳. _cache.Set(deviceCode, data)  (طبق معمول)
        └─ ۴. return "OK"
```

## نکات امنیتی/عملکردی

- **DB query فقط در صورت تغییر واقعی** — نه در هر poll ۵ ثانیه‌ای
- **خطا در DB swallow می‌شود** — اگر دیتابیس مشکل داشته باشد، جریان اصلی ESP32 مختل نمی‌شود
- **Snapshot به JSON** — با اضافه شدن فیلد جدید به DTO، snapshot به‌طور خودکار کامل می‌ماند
- **تغییر توضیحات به فارسی** — کاربر به راحتی می‌فهمد چه اتفاقی افتاده
- **تغییر مسیر لاگ ناقص نمی‌شود** چون FK ندارد
