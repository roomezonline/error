using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TechnicalController : ControllerBase
{
    private const string ErrorCodeCacheTag = "error-codes";
    private const string ErrorCodesPerm = "perm:admin.technical.errorcodes.manage";

    private readonly ErrorServiceDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IOutputCacheStore _cacheStore;
    private readonly ILogger<TechnicalController> _logger;
    private readonly NotificationEventService _notificationEvents;

    public TechnicalController(ErrorServiceDbContext context, IWebHostEnvironment env, IOutputCacheStore cacheStore, ILogger<TechnicalController> logger, NotificationEventService notificationEvents)
    {
        _context = context;
        _env = env;
        _cacheStore = cacheStore;
        _logger = logger;
        _notificationEvents = notificationEvents;
    }

    private async Task EvictErrorCodesCacheAsync()
    {
        try { await _cacheStore.EvictByTagAsync(ErrorCodeCacheTag, CancellationToken.None); }
        catch (Exception ex) { _logger.LogWarning(ex, "Error evicting error-codes cache"); }
        ErrorService.Server.Infrastructure.SeoFallbackMiddleware.InvalidateHtmlCache();
    }

    private static string Norm(string? value) => (value ?? string.Empty).Trim();

    private static ErrorCode NormalizeErrorCode(ErrorCode errorCode)
    {
        errorCode.Brand = Norm(errorCode.Brand);
        errorCode.DeviceType = Norm(errorCode.DeviceType);
        errorCode.Code = Norm(errorCode.Code);
        errorCode.Category = string.IsNullOrWhiteSpace(errorCode.Category) ? null : Norm(errorCode.Category);
        return errorCode;
    }

    private async Task<bool> IsDuplicateErrorCodeAsync(string brand, string deviceType, string code, int? excludeId = null)
    {
        var b = Norm(brand).ToLowerInvariant();
        var d = Norm(deviceType).ToLowerInvariant();
        var c = Norm(code).ToLowerInvariant();
        return await _context.ErrorCodes.AnyAsync(e =>
            e.Brand.Trim().ToLower() == b &&
            e.DeviceType.Trim().ToLower() == d &&
            e.Code.Trim().ToLower() == c &&
            (excludeId == null || e.Id != excludeId));
    }

    private async Task EnsureCatalogItemAsync(ErrorCodeCatalogKind kind, string? name)
    {
        var n = Norm(name);
        if (n.Length == 0) return;

        var key = n.ToLowerInvariant();
        var exists = await _context.ErrorCodeCatalogItems.AnyAsync(x =>
            x.Kind == kind && x.Name.ToLower() == key);
        if (!exists)
        {
            _context.ErrorCodeCatalogItems.Add(new ErrorCodeCatalogItem { Kind = kind, Name = n });
        }
    }

    private async Task SyncCatalogFromErrorCodeAsync(ErrorCode errorCode)
    {
        await EnsureCatalogItemAsync(ErrorCodeCatalogKind.Brand, errorCode.Brand);
        await EnsureCatalogItemAsync(ErrorCodeCatalogKind.DeviceType, errorCode.DeviceType);
        await EnsureCatalogItemAsync(ErrorCodeCatalogKind.Category, errorCode.Category);
    }

    private async Task<int> CountBrandUsageAsync(string name, int? excludeCatalogId = null)
    {
        var key = Norm(name).ToLowerInvariant();
        if (key.Length == 0) return 0;
        var fromCodes = await _context.ErrorCodes.CountAsync(e => e.Brand.Trim().ToLower() == key);
        var fromCatalog = await _context.ErrorCodeCatalogItems.CountAsync(x =>
            x.Kind == ErrorCodeCatalogKind.Brand &&
            x.Name.ToLower() == key &&
            (excludeCatalogId == null || x.Id != excludeCatalogId));
        return fromCodes + fromCatalog;
    }

    private async Task<int> CountDeviceUsageAsync(string name, int? excludeCatalogId = null)
    {
        var key = Norm(name).ToLowerInvariant();
        if (key.Length == 0) return 0;
        var fromCodes = await _context.ErrorCodes.CountAsync(e => e.DeviceType.Trim().ToLower() == key);
        var fromCatalog = await _context.ErrorCodeCatalogItems.CountAsync(x =>
            x.Kind == ErrorCodeCatalogKind.DeviceType &&
            x.Name.ToLower() == key &&
            (excludeCatalogId == null || x.Id != excludeCatalogId));
        return fromCodes + fromCatalog;
    }

    private async Task<int> CountCategoryUsageAsync(string name, int? excludeCatalogId = null)
    {
        var key = Norm(name).ToLowerInvariant();
        if (key.Length == 0) return 0;
        var fromCodes = await _context.ErrorCodes.CountAsync(e =>
            e.Category != null && e.Category.Trim().ToLower() == key);
        var fromCatalog = await _context.ErrorCodeCatalogItems.CountAsync(x =>
            x.Kind == ErrorCodeCatalogKind.Category &&
            x.Name.ToLower() == key &&
            (excludeCatalogId == null || x.Id != excludeCatalogId));
        return fromCodes + fromCatalog;
    }

    private async Task<int> GetCatalogUsageAsync(ErrorCodeCatalogKind kind, string name, int? excludeCatalogId = null) =>
        kind switch
        {
            ErrorCodeCatalogKind.Brand => await CountBrandUsageAsync(name, excludeCatalogId),
            ErrorCodeCatalogKind.DeviceType => await CountDeviceUsageAsync(name, excludeCatalogId),
            ErrorCodeCatalogKind.Category => await CountCategoryUsageAsync(name, excludeCatalogId),
            _ => 0
        };

    private async Task<bool> CatalogNameExistsAsync(ErrorCodeCatalogKind kind, string name, int? excludeId = null)
    {
        var key = Norm(name).ToLowerInvariant();
        if (key.Length == 0) return false;
        return await _context.ErrorCodeCatalogItems.AnyAsync(x =>
            x.Kind == kind &&
            x.Name.ToLower() == key &&
            (excludeId == null || x.Id != excludeId));
    }

    private async Task RenameErrorCodesCascadeAsync(ErrorCodeCatalogKind kind, string oldName, string newName)
    {
        var oldKey = Norm(oldName).ToLowerInvariant();
        var newKey = Norm(newName).ToLowerInvariant();
        if (oldKey.Length == 0 || newKey.Length == 0 || oldKey == newKey) return;

        if (kind == ErrorCodeCatalogKind.Brand)
        {
            var items = await _context.ErrorCodes
                .Where(e => e.Brand.Trim().ToLower() == oldKey)
                .ToListAsync();
            foreach (var item in items)
                item.Brand = newName;
        }
        else if (kind == ErrorCodeCatalogKind.DeviceType)
        {
            var items = await _context.ErrorCodes
                .Where(e => e.DeviceType.Trim().ToLower() == oldKey)
                .ToListAsync();
            foreach (var item in items)
                item.DeviceType = newName;
        }
        else if (kind == ErrorCodeCatalogKind.Category)
        {
            var items = await _context.ErrorCodes
                .Where(e => e.Category != null && e.Category.Trim().ToLower() == oldKey)
                .ToListAsync();
            foreach (var item in items)
                item.Category = newName;
        }
    }

    // --- Error Code Catalog ---

    [HttpGet("error-codes/catalog")]
    [OutputCache(Duration = 300, VaryByQueryKeys = new[] { "kind" }, Tags = new[] { ErrorCodeCacheTag })]
    public async Task<ActionResult<IEnumerable<ErrorCodeCatalogItem>>> GetCatalog(ErrorCodeCatalogKind kind)
    {
        var items = await _context.ErrorCodeCatalogItems
            .AsNoTracking()
            .Where(x => x.Kind == kind)
            .OrderBy(x => x.Name)
            .ToListAsync();

        var result = new List<ErrorCodeCatalogItem>();
        foreach (var item in items)
        {
            item.UsageCount = await GetCatalogUsageAsync(kind, item.Name, item.Id);
            result.Add(item);
        }
        return Ok(result);
    }

    [Authorize(Policy = ErrorCodesPerm)]
    [HttpPost("error-codes/catalog")]
    public async Task<ActionResult<ErrorCodeCatalogItem>> CreateCatalogItem([FromBody] ErrorCodeCatalogItem request)
    {
        var name = Norm(request.Name);
        if (name.Length == 0)
            return BadRequest(new { message = "نام الزامی است." });

        if (await CatalogNameExistsAsync(request.Kind, name))
            return Conflict(new { message = $"«{name}» قبلاً در این لیست ثبت شده است." });

        var item = new ErrorCodeCatalogItem { Kind = request.Kind, Name = name };
        _context.ErrorCodeCatalogItems.Add(item);
        await _context.SaveChangesAsync();
        await EvictErrorCodesCacheAsync();

        item.UsageCount = 0;
        return Ok(item);
    }

    [Authorize(Policy = ErrorCodesPerm)]
    [HttpPut("error-codes/catalog/{id}")]
    public async Task<IActionResult> RenameCatalogItem(int id, [FromBody] ErrorCodeCatalogUpsertRequest request)
    {
        var item = await _context.ErrorCodeCatalogItems.FindAsync(id);
        if (item == null) return NotFound();

        var newName = Norm(request.Name);
        if (newName.Length == 0)
            return BadRequest(new { message = "نام الزامی است." });

        var oldName = item.Name;
        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            return NoContent();

        if (await CatalogNameExistsAsync(item.Kind, newName, id))
            return Conflict(new { message = $"«{newName}» قبلاً در این لیست ثبت شده است." });

        await RenameErrorCodesCascadeAsync(item.Kind, oldName, newName);
        item.Name = newName;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "تغییر نام باعث ایجاد تکرار در کدهای خطا می‌شود." });
        }

        await EvictErrorCodesCacheAsync();
        return NoContent();
    }

    [Authorize(Policy = ErrorCodesPerm)]
    [HttpDelete("error-codes/catalog/{id}")]
    public async Task<IActionResult> DeleteCatalogItem(int id)
    {
        var item = await _context.ErrorCodeCatalogItems.FindAsync(id);
        if (item == null) return NotFound();

        // Usage of this exact name excluding this catalog row itself
        var usage = await GetCatalogUsageAsync(item.Kind, item.Name, item.Id);
        if (usage > 0)
            return Conflict(new { message = $"«{item.Name}» در {usage} مورد استفاده شده و قابل حذف نیست." });

        _context.ErrorCodeCatalogItems.Remove(item);
        await _context.SaveChangesAsync();
        await EvictErrorCodesCacheAsync();
        return NoContent();
    }

    private async Task<int?> ResolveSiteUserIdAsync()
    {
        var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(idStr) && int.TryParse(idStr, out var id))
            return id;

        var phone = User.FindFirst(System.Security.Claims.ClaimTypes.MobilePhone)?.Value;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
            if (user != null) return user.Id;
        }
        return null;
    }

    // --- Error Codes ---

    [HttpGet("error-codes")]
    [OutputCache(Duration = 300, VaryByQueryKeys = new[] { "*" }, Tags = new[] { ErrorCodeCacheTag })]
    public async Task<ActionResult<IEnumerable<ErrorCode>>> GetErrorCodes(string? brand = null, string? deviceType = null, string? search = null, string? category = null)
    {
        var query = _context.ErrorCodes
            .Include(e => e.Documents)
            .AsQueryable();

        if (!string.IsNullOrEmpty(brand))
        {
            var brandKey = brand.Trim().ToLowerInvariant();
            query = query.Where(e => e.Brand.Trim().ToLower() == brandKey);
        }

        if (!string.IsNullOrEmpty(deviceType))
        {
            var deviceKey = deviceType.Trim().ToLowerInvariant();
            query = query.Where(e => e.DeviceType.Trim().ToLower() == deviceKey);
        }

        if (!string.IsNullOrEmpty(category))
        {
            var categoryKey = category.Trim().ToLowerInvariant();
            query = query.Where(e => e.Category != null && e.Category.Trim().ToLower() == categoryKey);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var searchKey = search.Trim().ToLowerInvariant();
            query = query.Where(e => e.Code.ToLower().Contains(searchKey) ||
                                     e.Description.ToLower().Contains(searchKey) ||
                                     e.Solution.ToLower().Contains(searchKey));
        }

        return await query.ToListAsync();
    }

    [HttpGet("error-codes/recent")]
    [OutputCache(Duration = 600, Tags = new[] { ErrorCodeCacheTag })]
    public async Task<ActionResult<IEnumerable<object>>> GetRecentErrorCodes([FromQuery] int take = 3)
    {
        return await _context.ErrorCodes
            .OrderByDescending(e => e.Id)
            .Take(take)
            .Select(e => new { e.Brand, e.DeviceType, e.Code, e.Description, e.Solution })
            .ToListAsync();
    }

    [HttpGet("error-codes/count")]
    [OutputCache(Duration = 600, Tags = new[] { ErrorCodeCacheTag })]
    public async Task<ActionResult<int>> GetErrorCodeCount()
    {
        return await _context.ErrorCodes.CountAsync();
    }

    [HttpGet("error-codes/detail")]
    [OutputCache(Duration = 600, VaryByQueryKeys = new[] { "brand", "deviceType", "code" }, Tags = new[] { ErrorCodeCacheTag })]
    public async Task<ActionResult<ErrorCode>> GetErrorCodeDetail(
        [FromQuery] string brand,
        [FromQuery] string deviceType,
        [FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(deviceType) || string.IsNullOrWhiteSpace(code))
            return BadRequest("برند، نوع دستگاه و کد خطا الزامی هستند.");

        var brandKey = brand.Trim().ToLowerInvariant();
        var deviceKey = deviceType.Trim().ToLowerInvariant();
        var codeKey = code.Trim().ToLowerInvariant();

        var errorCode = await _context.ErrorCodes
            .Include(e => e.Documents)
            .FirstOrDefaultAsync(e => e.Brand.Trim().ToLower() == brandKey &&
                                      e.DeviceType.Trim().ToLower() == deviceKey &&
                                      e.Code.Trim().ToLower() == codeKey);

        return errorCode == null ? NotFound() : Ok(errorCode);
    }

    [HttpGet("error-codes/brands")]
    [OutputCache(Duration = 1800, Tags = new[] { ErrorCodeCacheTag })]
    public async Task<ActionResult<IEnumerable<string>>> GetBrands()
    {
        var catalog = await _context.ErrorCodeCatalogItems
            .AsNoTracking()
            .Where(x => x.Kind == ErrorCodeCatalogKind.Brand)
            .Select(x => x.Name)
            .ToListAsync();

        var used = await _context.ErrorCodes
            .AsNoTracking()
            .Select(e => e.Brand)
            .ToListAsync();

        return Ok(catalog
            .Concat(used)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    [HttpGet("error-codes/devices")]
    [OutputCache(Duration = 1800, Tags = new[] { ErrorCodeCacheTag })]
    public async Task<ActionResult<IEnumerable<string>>> GetDeviceTypes()
    {
        var catalog = await _context.ErrorCodeCatalogItems
            .AsNoTracking()
            .Where(x => x.Kind == ErrorCodeCatalogKind.DeviceType)
            .Select(x => x.Name)
            .ToListAsync();

        var used = await _context.ErrorCodes
            .AsNoTracking()
            .Select(e => e.DeviceType)
            .ToListAsync();

        return Ok(catalog
            .Concat(used)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    [HttpGet("error-codes/categories")]
    [OutputCache(Duration = 3600, Tags = new[] { ErrorCodeCacheTag })]
    public async Task<ActionResult<IEnumerable<string>>> GetCategories()
    {
        var catalog = await _context.ErrorCodeCatalogItems
            .AsNoTracking()
            .Where(x => x.Kind == ErrorCodeCatalogKind.Category)
            .Select(x => x.Name)
            .ToListAsync();

        if (catalog.Count == 0)
            return Ok(ErrorCodeCategories.All);

        return Ok(catalog
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    [HttpGet("error-codes/exists")]
    public async Task<ActionResult<bool>> CheckErrorCodeExists(
        [FromQuery] string brand,
        [FromQuery] string deviceType,
        [FromQuery] string code,
        [FromQuery] int? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(deviceType) || string.IsNullOrWhiteSpace(code))
            return BadRequest("برند، نوع دستگاه و کد خطا الزامی هستند.");
        return Ok(await IsDuplicateErrorCodeAsync(brand, deviceType, code, excludeId));
    }

    [HttpGet("error-codes/{id}")]
    [OutputCache(Duration = 600, Tags = new[] { ErrorCodeCacheTag })]
    public async Task<ActionResult<ErrorCode>> GetErrorCodeById(int id)
    {
        var ec = await _context.ErrorCodes
            .Include(e => e.Documents)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (ec == null) return NotFound();
        return Ok(ec);
    }

    [HttpGet("error-codes/related/{id}")]
    [OutputCache(Duration = 600, Tags = new[] { ErrorCodeCacheTag })]
    public async Task<ActionResult<IEnumerable<ErrorCode>>> GetRelatedErrorCodes(int id)
    {
        var current = await _context.ErrorCodes.FindAsync(id);
        if (current == null) return NotFound();

        var related = await _context.ErrorCodes
            .Include(e => e.Documents)
            .Where(e => e.Id != id && e.Brand == current.Brand && e.DeviceType == current.DeviceType)
            .OrderBy(e => e.Code)
            .Take(6)
            .ToListAsync();

        return Ok(related);
    }

    [HttpGet("error-codes/by-brand/{brand}")]
    [OutputCache(Duration = 600, Tags = new[] { ErrorCodeCacheTag })]
    public async Task<ActionResult<IEnumerable<ErrorCode>>> GetErrorCodesByBrand(string brand)
    {
        return await _context.ErrorCodes
            .Include(e => e.Documents)
            .Where(e => e.Brand == brand)
            .OrderBy(e => e.DeviceType).ThenBy(e => e.Code)
            .ToListAsync();
    }

    [HttpGet("error-codes/by-device/{brand}/{deviceType}")]
    [OutputCache(Duration = 600, Tags = new[] { ErrorCodeCacheTag })]
    public async Task<ActionResult<IEnumerable<ErrorCode>>> GetErrorCodesByDevice(string brand, string deviceType)
    {
        return await _context.ErrorCodes
            .Include(e => e.Documents)
            .Where(e => e.Brand == brand && e.DeviceType == deviceType)
            .OrderBy(e => e.Code)
            .ToListAsync();
    }

    [Authorize(Policy = ErrorCodesPerm)]
    [HttpPost("error-codes")]
    public async Task<ActionResult<ErrorCode>> PostErrorCode(ErrorCode errorCode)
    {
        NormalizeErrorCode(errorCode);

        if (string.IsNullOrWhiteSpace(errorCode.Brand) ||
            string.IsNullOrWhiteSpace(errorCode.DeviceType) ||
            string.IsNullOrWhiteSpace(errorCode.Code))
            return BadRequest("برند، نوع دستگاه و کد خطا الزامی هستند.");

        if (await IsDuplicateErrorCodeAsync(errorCode.Brand, errorCode.DeviceType, errorCode.Code))
            return Conflict(new { message = $"کد {errorCode.Code} برای «{errorCode.Brand} - {errorCode.DeviceType}» قبلاً ثبت شده است." });

        await SyncCatalogFromErrorCodeAsync(errorCode);
        _context.ErrorCodes.Add(errorCode);
        await _context.SaveChangesAsync();
        await EvictErrorCodesCacheAsync();
        return Ok(errorCode);
    }

    [Authorize(Policy = ErrorCodesPerm)]
    [HttpPost("error-codes/upload")]
    public async Task<ActionResult<string>> UploadErrorCodeImage([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("فایل ارسال نشده است");

        const long maxBytes = 5 * 1024 * 1024;
        if (file.Length > maxBytes)
            return BadRequest("حجم فایل زیاد است (حداکثر 5MB)");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var relativeFolder = "uploads/error-codes";
        var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var uploadsRoot = Path.Combine(wwwroot, relativeFolder);
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using (var outStream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(outStream);
        }

        return Ok($"/{relativeFolder}/{fileName}");
    }

    [Authorize(Policy = ErrorCodesPerm)]
    [HttpPut("error-codes/{id}")]
    public async Task<IActionResult> PutErrorCode(int id, ErrorCode errorCode)
    {
        if (id != errorCode.Id) return BadRequest();

        NormalizeErrorCode(errorCode);

        if (string.IsNullOrWhiteSpace(errorCode.Brand) ||
            string.IsNullOrWhiteSpace(errorCode.DeviceType) ||
            string.IsNullOrWhiteSpace(errorCode.Code))
            return BadRequest("برند، نوع دستگاه و کد خطا الزامی هستند.");

        if (await IsDuplicateErrorCodeAsync(errorCode.Brand, errorCode.DeviceType, errorCode.Code, id))
            return Conflict(new { message = $"کد {errorCode.Code} برای «{errorCode.Brand} - {errorCode.DeviceType}» قبلاً ثبت شده است." });

        var existing = await _context.ErrorCodes
            .Include(e => e.Documents)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (existing == null) return NotFound();

        existing.Brand = errorCode.Brand;
        existing.DeviceType = errorCode.DeviceType;
        existing.Code = errorCode.Code;
        existing.Description = errorCode.Description;
        existing.Solution = errorCode.Solution;
        existing.TechnicalNotes = errorCode.TechnicalNotes;
        existing.ModelNames = errorCode.ModelNames;
        existing.RelatedProductIds = errorCode.RelatedProductIds;
        existing.ImageUrl = errorCode.ImageUrl;
        existing.Category = errorCode.Category;

        await SyncCatalogFromErrorCodeAsync(errorCode);

        if (errorCode.Documents != null)
        {
            _context.ErrorCodeDocuments.RemoveRange(existing.Documents);
            existing.Documents = errorCode.Documents
                .OrderBy(d => d.SortOrder)
                .Select((d, i) => new ErrorCodeDocument
                {
                    Title = d.Title,
                    Url = d.Url,
                    DocType = d.DocType,
                    SortOrder = i
                })
                .ToList();
        }

        await _context.SaveChangesAsync();
        await EvictErrorCodesCacheAsync();
        return NoContent();
    }

    [Authorize(Policy = ErrorCodesPerm)]
    [HttpDelete("error-codes/{id}")]
    public async Task<IActionResult> DeleteErrorCode(int id)
    {
        var errorCode = await _context.ErrorCodes.FindAsync(id);
        if (errorCode == null) return NotFound();
        _context.ErrorCodes.Remove(errorCode);
        await _context.SaveChangesAsync();
        await EvictErrorCodesCacheAsync();
        return NoContent();
    }

    // --- Consultation Tickets ---

    [HttpGet("tickets")]
    public async Task<ActionResult<IEnumerable<ConsultationTicket>>> GetTickets(string? userEmail = null)
    {
        // NOTE: This endpoint previously returned all tickets; now it is restricted.
        var query = _context.ConsultationTickets.Include(t => t.Replies).AsQueryable();

        if (!string.IsNullOrEmpty(userEmail))
        {
            query = query.Where(t => t.UserEmail == userEmail);
        }
        else
        {
            // If no userEmail is supplied, only allow authenticated users to see their own tickets.
            var userId = await ResolveSiteUserIdAsync();
            if (userId == null)
                return Unauthorized();

            query = query.Where(t => t.UserId == userId.Value);
        }
        
        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    [Authorize]
    [HttpGet("tickets/my")]
    public async Task<ActionResult<IEnumerable<ConsultationTicket>>> GetMyTickets()
    {
        var userId = await ResolveSiteUserIdAsync();
        if (userId == null)
            return Unauthorized();

        return await _context.ConsultationTickets
            .Include(t => t.Replies)
            .Where(t => t.UserId == userId.Value)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    [Authorize(Policy = "perm:admin.consultation.manage")]
    [HttpGet("tickets/admin")]
    public async Task<ActionResult<IEnumerable<ConsultationTicket>>> GetAdminTickets()
    {
        var tickets = await _context.ConsultationTickets
            .Include(t => t.Replies)
            .OrderByDescending(t => t.IsAdminRead == false)
            .ThenByDescending(t => t.LastMessageAt)
            .ToListAsync();

        var userIds = tickets
            .Where(t => t.UserId.HasValue)
            .Select(t => t.UserId!.Value)
            .Distinct()
            .ToList();

        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.PhoneNumber, u.Email })
            .ToDictionaryAsync(x => x.Id);

        foreach (var t in tickets)
        {
            if (t.UserId.HasValue && users.TryGetValue(t.UserId.Value, out var u))
            {
                t.UserFullName = u.FullName;
                t.UserPhoneNumber = u.PhoneNumber;
                t.UserEmail = u.Email;
            }
            else
            {
                t.UserFullName = null;
                t.UserPhoneNumber = null;
                t.UserEmail = null;
            }
        }

        return tickets;
    }

    [HttpPost("tickets/upload-attachment")]
    public async Task<ActionResult<dynamic>> UploadTicketAttachment([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("فایل ارسال نشده است");

        const long maxBytes = 10 * 1024 * 1024; // 10MB
        if (file.Length > maxBytes)
            return BadRequest("حجم فایل زیاد است (حداکثر 10MB)");

        var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "tickets");
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        var ext = Path.GetExtension(file.FileName);
        var originalName = file.FileName;
        var contentType = file.ContentType;

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new { 
            Url = $"/uploads/tickets/{fileName}", 
            Name = originalName, 
            ContentType = contentType 
        });
    }

    [HttpPost("tickets")]
    public async Task<ActionResult<ConsultationTicket>> PostTicket(ConsultationTicket ticket)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userIdStr) && int.TryParse(userIdStr, out var userId))
            ticket.UserId = userId;

        ticket.CreatedAt = DateTime.Now;
        ticket.LastMessageAt = DateTime.Now;
        ticket.IsAdminRead = false;
        ticket.IsUserRead = true;
        ticket.Status = TicketStatus.Pending;
        _context.ConsultationTickets.Add(ticket);
        await _context.SaveChangesAsync();
        return Ok(ticket);
    }

    [HttpPost("tickets/{id}/reply")]
    public async Task<ActionResult<TicketReply>> PostReply(int id, TicketReply reply)
    {
        var ticket = await _context.ConsultationTickets.FindAsync(id);
        if (ticket == null) return NotFound();

        // If user is replying (not admin), ensure they own the ticket.
        if (!reply.IsAdmin)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return Unauthorized();

            if (ticket.UserId.HasValue && ticket.UserId.Value != userId)
                return Forbid();
            
            ticket.IsAdminRead = false;
            ticket.IsUserRead = true;
        }
        else 
        {
            ticket.IsAdminRead = true;
            ticket.IsUserRead = false;
        }

        reply.RepliedAt = DateTime.Now;
        reply.ConsultationTicketId = ticket.Id;
        ticket.Replies.Add(reply);
        ticket.LastMessageAt = DateTime.Now;
        
        if (reply.IsAdmin)
            ticket.Status = TicketStatus.InProgress;

        if (reply.IsAdmin && ticket.UserId.HasValue)
        {
            await _notificationEvents.NotifyAsync(
                eventType: "ticket.replied",
                values: new Dictionary<string, string?>
                {
                    ["TicketTitle"] = ticket.Title,
                    ["TicketId"] = ticket.Id.ToString()
                },
                idempotencyKey: $"ticket.replied:{ticket.Id}:{reply.RepliedAt:yyyyMMddHHmmssfff}",
                fallbackSeverity: NotificationSeverity.Info,
                fallbackTitle: "پاسخ جدید دریافت شد",
                fallbackBody: $"به تیکت «{ticket.Title}» پاسخ جدید ثبت شد.",
                fallbackUrl: "/technical/consultation",
                appUserId: ticket.UserId);
        }

        await _context.SaveChangesAsync();
        return Ok(reply);
    }

    [HttpPost("tickets/{id}/read-admin")]
    public async Task<IActionResult> MarkAsReadAdmin(int id)
    {
        var ticket = await _context.ConsultationTickets.FindAsync(id);
        if (ticket == null) return NotFound();

        ticket.IsAdminRead = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("tickets/{id}/read-user")]
    public async Task<IActionResult> MarkAsReadUser(int id)
    {
        var ticket = await _context.ConsultationTickets.FindAsync(id);
        if (ticket == null) return NotFound();

        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdStr) || !int.TryParse(userIdStr, out var userId))
            return Unauthorized();

        if (ticket.UserId != userId) return Forbid();

        ticket.IsUserRead = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("tickets/{id}/status")]
    public async Task<IActionResult> UpdateTicketStatus(int id, [FromBody] TicketStatus status)
    {
        var ticket = await _context.ConsultationTickets.FindAsync(id);
        if (ticket == null) return NotFound();

        var previousStatus = ticket.Status;
        ticket.Status = status;

        if (previousStatus != status && ticket.UserId.HasValue)
        {
            var statusLabel = status switch
            {
                TicketStatus.Pending => "در انتظار",
                TicketStatus.InProgress => "در حال بررسی",
                TicketStatus.Resolved => "حل شده",
                TicketStatus.Closed => "بسته شده",
                _ => status.ToString()
            };
            await _notificationEvents.NotifyAsync(
                eventType: "ticket.status_changed",
                values: new Dictionary<string, string?>
                {
                    ["TicketTitle"] = ticket.Title,
                    ["Status"] = statusLabel,
                    ["TicketId"] = ticket.Id.ToString()
                },
                idempotencyKey: $"ticket.status:{ticket.Id}:{status}:{DateTimeOffset.UtcNow:yyyyMMddHH}",
                fallbackSeverity: NotificationSeverity.Info,
                fallbackTitle: "وضعیت تیکت شما تغییر کرد",
                fallbackBody: $"وضعیت تیکت «{ticket.Title}» به «{statusLabel}» تغییر کرد.",
                fallbackUrl: "/technical/consultation",
                appUserId: ticket.UserId);
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }
}
