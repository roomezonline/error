using ErrorService.Server.Data;
using ErrorService.Server.Infrastructure;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    internal const string CacheTag = "categories";

    private readonly ErrorServiceDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly IOutputCacheStore _outputCache;
    private readonly IAuthorizationService _authorizationService;

    public CategoriesController(ErrorServiceDbContext context, IWebHostEnvironment env, IConfiguration configuration, IOutputCacheStore outputCache, IAuthorizationService authorizationService)
    {
        _context = context;
        _env = env;
        _configuration = configuration;
        _outputCache = outputCache;
        _authorizationService = authorizationService;
    }

    private async Task InvalidateCategoryCacheAsync()
    {
        try { await _outputCache.EvictByTagAsync(CacheTag, CancellationToken.None); }
        catch { /* eviction must never break the request */ }
        // نام دسته در خروجی محصولات هم می‌آید، پس کش محصولات هم باید پاک شود.
        try { await _outputCache.EvictByTagAsync(ProductsController.CacheTag, CancellationToken.None); }
        catch { }
        SeoFallbackMiddleware.InvalidateHtmlCache();
    }

    [HttpGet]
    [OutputCache(Duration = 3600, Tags = new[] { CacheTag }, VaryByQueryKeys = new[] { "includeInactive" })]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories(bool includeInactive = false)
    {
        if (includeInactive)
        {
            var auth = await _authorizationService.AuthorizeAsync(User, "perm:admin.categories.manage");
            if (!auth.Succeeded)
            {
                includeInactive = false;
            }
            else
            {
                var cacheFeature = HttpContext.Features.Get<Microsoft.AspNetCore.OutputCaching.IOutputCacheFeature>();
                if (cacheFeature != null)
                    cacheFeature.Context.EnableOutputCaching = false;
            }
        }

        var query = _context.Categories.AsQueryable();
        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Icon = c.Icon,
                SortOrder = c.SortOrder,
                ProductCount = _context.Products.Count(p => p.CategoryId == c.Id),
                Slug = c.Slug,
                ParentId = c.ParentId,
                SeoTitle = c.SeoTitle,
                SeoDescription = c.SeoDescription,
                IsActive = c.IsActive
            }).ToListAsync();
    }

    private async Task<string?> ValidateParentAsync(int? parentId, int? selfId)
    {
        if (!parentId.HasValue)
            return null;
        if (parentId == selfId)
            return "دسته والد نمی‌تواند خود همین دسته باشد.";
        if (!await _context.Categories.AnyAsync(c => c.Id == parentId.Value))
            return "دسته والد انتخاب‌شده معتبر نیست.";

        var current = parentId;
        var depth = 0;
        while (current.HasValue && depth++ < 50)
        {
            if (current == selfId)
                return "انتخاب این دسته به‌عنوان والد، حلقه ایجاد می‌کند.";
            var next = current.Value;
            current = await _context.Categories
                .Where(c => c.Id == next)
                .Select(c => c.ParentId)
                .FirstOrDefaultAsync();
            if (current == parentId)
                return "انتخاب این دسته به‌عنوان والد، حلقه ایجاد می‌کند.";
        }
        return null;
    }

    [Authorize(Policy = "perm:admin.categories.manage")]
    [HttpPost]
    public async Task<ActionResult<CategoryDto>> PostCategory(CategoryDto categoryDto)
    {
        var parentError = await ValidateParentAsync(categoryDto.ParentId, null);
        if (parentError != null)
            return BadRequest(parentError);

        var category = new Category
        {
            Name = categoryDto.Name,
            Icon = categoryDto.Icon,
            SortOrder = categoryDto.SortOrder,
            Slug = await SlugService.ResolveUniqueAsync(
                _context.Categories.Where(c => c.Slug != null).Select(c => c.Slug!),
                string.IsNullOrWhiteSpace(categoryDto.Slug) ? categoryDto.Name : categoryDto.Slug),
            ParentId = categoryDto.ParentId,
            SeoTitle = categoryDto.SeoTitle,
            SeoDescription = categoryDto.SeoDescription,
            IsActive = categoryDto.IsActive
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        await InvalidateCategoryCacheAsync();

        categoryDto.Id = category.Id;
        return CreatedAtAction(nameof(GetCategories), new { id = category.Id }, categoryDto);
    }

    [Authorize(Policy = "perm:admin.categories.manage")]
    [HttpPost("upload")]
    [RequestSizeLimit(3_000_000)]
    public async Task<ActionResult<string>> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length <= 0)
            {
                return BadRequest("فایل خالی است یا دریافت نشد");
            }

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".bin";
            }

            var safeExt = ext.ToLowerInvariant();
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(safeExt))
            {
                return BadRequest("فرمت فایل مجاز نیست. فقط jpg, jpeg, png, webp");
            }

            var relativeFolder = _configuration["Uploads:CategoriesRelativePath"];
            if (string.IsNullOrWhiteSpace(relativeFolder))
            {
                relativeFolder = "uploads/categories";
            }

            relativeFolder = relativeFolder.Trim().TrimStart('~').TrimStart('/').TrimEnd('/');

            var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            if (!Directory.Exists(wwwroot))
            {
                Directory.CreateDirectory(wwwroot);
            }

            var uploadsRoot = Path.Combine(wwwroot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(uploadsRoot))
            {
                Directory.CreateDirectory(uploadsRoot);
            }

            var fileName = $"{Guid.NewGuid():N}{safeExt}";
            var fullPath = Path.Combine(uploadsRoot, fileName);

            await using (var outStream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(outStream, cancellationToken);
            }

            var publicUrl = $"/{relativeFolder}/{fileName}";
            return Ok(publicUrl);
        }
        catch (Exception ex)
        {
            return BadRequest($"آپلود تصویر ناموفق بود: {ex.Message}");
        }
    }

    [Authorize(Policy = "perm:admin.categories.manage")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutCategory(int id, CategoryDto categoryDto)
    {
        if (id != categoryDto.Id)
        {
            return BadRequest("شناسه دسته‌بندی مطابقت ندارد");
        }

        var category = await _context.Categories.FindAsync(id);
        if (category == null)
        {
            return NotFound("دسته‌بندی مورد نظر یافت نشد");
        }

        var oldName = category.Name;
        var parentError = await ValidateParentAsync(categoryDto.ParentId, id);
        if (parentError != null)
            return BadRequest(parentError);

        category.Name = categoryDto.Name;
        FileCleanupHelper.DeleteOldFileIfChanged(category.Icon, categoryDto.Icon, _env);
        category.Icon = categoryDto.Icon;
        category.SortOrder = categoryDto.SortOrder;
        category.ParentId = categoryDto.ParentId;
        category.SeoTitle = categoryDto.SeoTitle;
        category.SeoDescription = categoryDto.SeoDescription;
        category.IsActive = categoryDto.IsActive;
        var otherSlugs = _context.Categories.Where(c => c.Slug != null && c.Id != id).Select(c => c.Slug!);
        category.Slug = !string.IsNullOrWhiteSpace(categoryDto.Slug)
            ? await SlugService.ResolveUniqueAsync(otherSlugs, categoryDto.Slug)
            : await SlugService.ResolveForUpdateAsync(otherSlugs, oldName, category.Slug, categoryDto.Name);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!CategoryExists(id)) return NotFound();
            else throw;
        }

        await InvalidateCategoryCacheAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.categories.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);
        if (category == null)
        {
            return NotFound("دسته‌بندی یافت نشد");
        }

        if (category.Products.Any())
        {
            return BadRequest("این دسته‌بندی دارای محصول است و نمی‌توان آن را حذف کرد. ابتدا محصولات را جابجا یا حذف کنید.");
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        await InvalidateCategoryCacheAsync();

        return NoContent();
    }

    private bool CategoryExists(int id) => _context.Categories.Any(e => e.Id == id);
}

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    internal const string CacheTag = "products";

    /// <summary>آستانهٔ «موجودی کم» در پنل مدیریت محصولات.</summary>
    private const int LowStockThreshold = 5;

    private readonly ErrorServiceDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly StockAlertNotifier _stockAlerts;
    private readonly IOutputCacheStore _outputCache;

    public ProductsController(ErrorServiceDbContext context, IWebHostEnvironment env, IConfiguration configuration, StockAlertNotifier stockAlerts, IOutputCacheStore outputCache)
    {
        _context = context;
        _env = env;
        _configuration = configuration;
        _stockAlerts = stockAlerts;
        _outputCache = outputCache;
    }

    private async Task InvalidateProductCacheAsync()
    {
        try { await _outputCache.EvictByTagAsync(CacheTag, CancellationToken.None); }
        catch { /* eviction must never break the request */ }
        SeoFallbackMiddleware.InvalidateHtmlCache();
    }

    [HttpPost("check-availability")]
    [AllowAnonymous]
    public async Task<ActionResult<List<ProductAvailabilityDto>>> CheckAvailability([FromBody] List<ProductAvailabilityCheckItem>? items)
    {
        if (items == null || items.Count == 0)
            return Ok(new List<ProductAvailabilityDto>());

        var ids = items.Select(x => x.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var result = new List<ProductAvailabilityDto>();
        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var p))
            {
                result.Add(new ProductAvailabilityDto
                {
                    ProductId = item.ProductId,
                    IsAvailable = false,
                    StockQuantity = 0,
                    HasSufficientStock = false
                });
                continue;
            }

            var qty = Math.Max(1, item.Quantity);
            result.Add(new ProductAvailabilityDto
            {
                ProductId = p.Id,
                Name = p.Name,
                IsAvailable = p.IsAvailable,
                StockQuantity = p.StockQuantity,
                HasSufficientStock = p.IsAvailable && p.StockQuantity >= qty
            });
        }

        return Ok(result);
    }

    [Authorize(Policy = "perm:admin.products.manage")]
    [HttpPost("upload")]
    [RequestSizeLimit(15_000_000)]
    public async Task<ActionResult<string>> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length <= 0)
                return BadRequest("فایل خالی است یا دریافت نشد");

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".bin";

            var safeExt = ext.ToLowerInvariant();
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(safeExt))
                return BadRequest("فرمت فایل مجاز نیست. فقط jpg, jpeg, png, webp");

            var relativeFolder = _configuration["Uploads:ProductsRelativePath"];
            if (string.IsNullOrWhiteSpace(relativeFolder))
                relativeFolder = "uploads/products";

            relativeFolder = relativeFolder.Trim().TrimStart('~').TrimStart('/').TrimEnd('/');

            var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            if (!Directory.Exists(wwwroot)) Directory.CreateDirectory(wwwroot);

            var uploadsRoot = Path.Combine(wwwroot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(uploadsRoot)) Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{Guid.NewGuid():N}{safeExt}";
            var fullPath = Path.Combine(uploadsRoot, fileName);

            await using (var outStream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(outStream, cancellationToken);
            }

            return Ok($"/{relativeFolder}/{fileName}");
        }
        catch (Exception ex)
        {
            return BadRequest($"آپلود تصویر ناموفق بود: {ex.Message}");
        }
    }

    [HttpGet("facets")]
    [OutputCache(Duration = 120, VaryByQueryKeys = new[] { "*" }, Tags = new[] { ProductsController.CacheTag })]
    public async Task<ActionResult<ProductFacetsDto>> GetProductFacets(
        int? categoryId = null,
        string? categorySlug = null,
        bool onlyAvailable = false,
        bool onlyDiscounted = false,
        string? search = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        int? minStock = null,
        int? maxStock = null)
    {
        var now = DateTimeOffset.UtcNow;
        var baseQuery = _context.Products.AsQueryable();

        // محصولات پیش‌نویس در فیلترها/شمارش‌های فروشگاه شمرده نمی‌شوند
        baseQuery = baseQuery.Where(p => !p.IsDraft);

        if (!string.IsNullOrWhiteSpace(search))
            baseQuery = baseQuery.ApplyTextSearch(search);

        if (minPrice.HasValue || maxPrice.HasValue)
            baseQuery = baseQuery.Where(p => p.DiscountPrice != null
                && (!p.DiscountStartDate.HasValue || p.DiscountStartDate.Value <= now)
                && (!p.DiscountExpiryDate.HasValue || p.DiscountExpiryDate.Value > now)
                    ? (p.DiscountPrice.Value >= (minPrice ?? 0) && (maxPrice == null || p.DiscountPrice.Value <= maxPrice))
                    : (p.Price >= (minPrice ?? 0) && (maxPrice == null || p.Price <= maxPrice)));

        if (minStock.HasValue)
            baseQuery = baseQuery.Where(p => p.StockQuantity >= minStock.Value);

        if (maxStock.HasValue)
            baseQuery = baseQuery.Where(p => p.StockQuantity <= maxStock.Value);

        var categoryScoped = baseQuery;
        if (categoryId.HasValue)
            categoryScoped = categoryScoped.Where(p => p.CategoryId == categoryId.Value);
        if (!string.IsNullOrWhiteSpace(categorySlug))
            categoryScoped = categoryScoped.Where(p => p.Category.Slug == categorySlug);

        var toggled = categoryScoped;
        if (onlyAvailable)
            toggled = toggled.Where(p => p.IsAvailable);
        if (onlyDiscounted)
            toggled = toggled.Where(p => p.DiscountPrice != null
                && (!p.DiscountStartDate.HasValue || p.DiscountStartDate.Value <= now)
                && (!p.DiscountExpiryDate.HasValue || p.DiscountExpiryDate.Value > now));

        var categoryFacets = await categoryScoped
            .GroupBy(p => new { p.CategoryId, p.Category!.Name, p.Category!.Slug })
            .Select(g => new CategoryFacetDto
            {
                Id = g.Key.CategoryId,
                Name = g.Key.Name,
                Slug = g.Key.Slug,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var total = await toggled.CountAsync();
        var availableCount = await categoryScoped.CountAsync(p => p.IsAvailable);
        var discountedCount = await categoryScoped.CountAsync(p => p.DiscountPrice != null
            && (!p.DiscountStartDate.HasValue || p.DiscountStartDate.Value <= now)
            && (!p.DiscountExpiryDate.HasValue || p.DiscountExpiryDate.Value > now));

        return new ProductFacetsDto
        {
            Categories = categoryFacets,
            Total = total,
            AvailableCount = availableCount,
            DiscountedCount = discountedCount
        };
    }

    [HttpGet]
    [OutputCache(Duration = 120, VaryByQueryKeys = new[] { "*" }, Tags = new[] { ProductsController.CacheTag })]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts(
        int? categoryId = null,
        string? categorySlug = null,
        bool onlyAvailable = false,
        bool onlyDiscounted = false,
        bool timedOnly = false,
        bool featuredOnly = false,
        bool random = false,
        string? search = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? sort = null,
        int? minStock = null,
        int? maxStock = null,
        int skip = 0,
        int take = 10)
    {
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(0, skip);
        var query = _context.Products.Include(p => p.Category).AsQueryable();
        var now = DateTimeOffset.UtcNow;

        // محصولات پیش‌نویس در فروشگاه نمایش داده نمی‌شوند
        query = query.Where(p => !p.IsDraft);

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(categorySlug))
            query = query.Where(p => p.Category.Slug == categorySlug);

        if (onlyAvailable)
            query = query.Where(p => p.IsAvailable);

        if (onlyDiscounted)
        {
            query = query.Where(p => p.DiscountPrice != null
                && (!p.DiscountStartDate.HasValue || p.DiscountStartDate.Value <= now)
                && (!p.DiscountExpiryDate.HasValue || p.DiscountExpiryDate.Value > now));
        }

        if (timedOnly)
        {
            query = query.Where(p => p.DiscountPrice != null
                && p.DiscountExpiryDate.HasValue && p.DiscountExpiryDate.Value > now
                && (!p.DiscountStartDate.HasValue || p.DiscountStartDate.Value <= now));
        }

        if (featuredOnly)
        {
            query = query.Where(p => p.IsFeatured);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.ApplyTextSearch(search);
        }

        if (minPrice.HasValue || maxPrice.HasValue)
        {
            query = query.Where(p => p.DiscountPrice != null
                && (!p.DiscountStartDate.HasValue || p.DiscountStartDate.Value <= now)
                && (!p.DiscountExpiryDate.HasValue || p.DiscountExpiryDate.Value > now)
                    ? (p.DiscountPrice.Value >= (minPrice ?? 0) && (maxPrice == null || p.DiscountPrice.Value <= maxPrice))
                    : (p.Price >= (minPrice ?? 0) && (maxPrice == null || p.Price <= maxPrice)));
        }

        if (minStock.HasValue)
            query = query.Where(p => p.StockQuantity >= minStock.Value);

        if (maxStock.HasValue)
            query = query.Where(p => p.StockQuantity <= maxStock.Value);

        var totalCount = await query.CountAsync();
        Response.Headers["X-Total-Count"] = totalCount.ToString();

        if (random)
        {
            var cacheFeature = HttpContext.Features.Get<Microsoft.AspNetCore.OutputCaching.IOutputCacheFeature>();
            if (cacheFeature != null)
            {
                cacheFeature.Context.EnableOutputCaching = false;
            }
            var picked = (await query.Select(p => p.Id).ToListAsync())
                .OrderBy(_ => Guid.NewGuid())
                .Take(take)
                .ToList();
            var items = await query
                .Where(p => picked.Contains(p.Id))
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Slug = p.Slug,
                    Sku = p.Sku,
                    Description = p.Description,
                    Price = p.Price,
                    DiscountPrice = p.DiscountPrice,
                    DiscountStartDate = p.DiscountStartDate,
                    DiscountExpiryDate = p.DiscountExpiryDate,
                    MainImageUrl = p.MainImageUrl,
                    ImageUrl2 = p.ImageUrl2,
                    ImageUrl3 = p.ImageUrl3,
                    ImageUrl4 = p.ImageUrl4,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category.Name,
                    IsAvailable = p.IsAvailable,
                    StockQuantity = p.StockQuantity,
                    IsFeatured = p.IsFeatured,
                    SortOrder = p.SortOrder,
                    CreatedAt = p.CreatedAt,
                    CompatibilityInfo = p.CompatibilityInfo,
                    EstimatedDeliveryText = p.EstimatedDeliveryText,
                    DatasheetUrl = p.DatasheetUrl,
                    FailureSymptoms = p.FailureSymptoms,
                    RelatedProductIds = p.RelatedProductIds,
                    ViewCount = p.ViewCount
                }).ToListAsync();
            return Ok(items.OrderBy(p => picked.IndexOf(p.Id)));
        }

        query = sort?.ToLowerInvariant() switch
        {
            "price_asc" => query.OrderBy(p => p.DiscountPrice != null
                && (!p.DiscountStartDate.HasValue || p.DiscountStartDate.Value <= now)
                && (!p.DiscountExpiryDate.HasValue || p.DiscountExpiryDate.Value > now)
                    ? p.DiscountPrice.Value : p.Price).ThenByDescending(p => p.CreatedAt),
            "price_desc" => query.OrderByDescending(p => p.DiscountPrice != null
                && (!p.DiscountStartDate.HasValue || p.DiscountStartDate.Value <= now)
                && (!p.DiscountExpiryDate.HasValue || p.DiscountExpiryDate.Value > now)
                    ? p.DiscountPrice.Value : p.Price).ThenByDescending(p => p.CreatedAt),
            "discount_desc" => query
                .OrderByDescending(p => p.DiscountPrice != null
                    && (!p.DiscountStartDate.HasValue || p.DiscountStartDate.Value <= now)
                    && (!p.DiscountExpiryDate.HasValue || p.DiscountExpiryDate.Value > now)
                        ? (p.Price - p.DiscountPrice.Value) : 0)
                .ThenByDescending(p => p.CreatedAt),
            "featured" => query
                .OrderByDescending(p => p.IsFeatured)
                .ThenBy(p => p.SortOrder)
                .ThenByDescending(p => p.CreatedAt),
            "manual" => query
                .OrderBy(p => p.SortOrder)
                .ThenByDescending(p => p.CreatedAt),
            "bestselling" => query
                .OrderByDescending(p => _context.OrderItems
                    .Where(oi => oi.ProductId == p.Id)
                    .Sum(oi => (int?)oi.Quantity) ?? 0)
                .ThenByDescending(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        var result = await query
            .Skip(skip)
            .Take(take)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Sku = p.Sku,
                Description = p.Description,
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                DiscountStartDate = p.DiscountStartDate,
                DiscountExpiryDate = p.DiscountExpiryDate,
                MainImageUrl = p.MainImageUrl,
                ImageUrl2 = p.ImageUrl2,
                ImageUrl3 = p.ImageUrl3,
                ImageUrl4 = p.ImageUrl4,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                IsAvailable = p.IsAvailable,
                StockQuantity = p.StockQuantity,
                IsFeatured = p.IsFeatured,
                SortOrder = p.SortOrder,
                CreatedAt = p.CreatedAt,
                CompatibilityInfo = p.CompatibilityInfo,
                EstimatedDeliveryText = p.EstimatedDeliveryText,
                DatasheetUrl = p.DatasheetUrl,
                FailureSymptoms = p.FailureSymptoms,
                RelatedProductIds = p.RelatedProductIds,
                ViewCount = p.ViewCount
            }).ToListAsync();

        return Ok(result);
    }

    private static IQueryable<ProductDto> ProjectProductCards(IQueryable<Product> query) =>
        query.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Slug = p.Slug,
            Sku = p.Sku,
            Description = p.Description,
            Price = p.Price,
            DiscountPrice = p.DiscountPrice,
            DiscountStartDate = p.DiscountStartDate,
            DiscountExpiryDate = p.DiscountExpiryDate,
            MainImageUrl = p.MainImageUrl,
            ImageUrl2 = p.ImageUrl2,
            ImageUrl3 = p.ImageUrl3,
            ImageUrl4 = p.ImageUrl4,
            CategoryId = p.CategoryId,
            CategoryName = p.Category.Name,
            IsAvailable = p.IsAvailable,
            StockQuantity = p.StockQuantity,
            IsFeatured = p.IsFeatured,
            SortOrder = p.SortOrder,
            CreatedAt = p.CreatedAt,
            CompatibilityInfo = p.CompatibilityInfo,
            EstimatedDeliveryText = p.EstimatedDeliveryText,
            DatasheetUrl = p.DatasheetUrl,
            FailureSymptoms = p.FailureSymptoms,
            RelatedProductIds = p.RelatedProductIds,
            ViewCount = p.ViewCount
        });

    /// <summary>پرفروش‌ترین محصولات بر اساس سفارش‌های تایید/تکمیل شده.</summary>
    [HttpGet("bestsellers")]
    public async Task<ActionResult<List<ProductDto>>> GetBestSellers([FromQuery] int take = 8)
    {
        take = Math.Clamp(take, 1, 24);

        var soldIds = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order.Status == OrderStatus.Approved || oi.Order.Status == OrderStatus.Completed)
            .GroupBy(oi => oi.ProductId)
            .Select(g => new { ProductId = g.Key, Sold = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Sold)
            .Take(take)
            .Select(x => x.ProductId)
            .ToListAsync();

        var baseQuery = _context.Products.AsNoTracking().Where(p => !p.IsDraft);

        if (soldIds.Count > 0)
        {
            var ordered = await ProjectProductCards(baseQuery.Where(p => soldIds.Contains(p.Id))).ToListAsync();
            return Ok(ordered.OrderBy(p => soldIds.IndexOf(p.Id)).ToList());
        }

        var fallback = await ProjectProductCards(baseQuery.OrderByDescending(p => p.ViewCount))
            .Take(take)
            .ToListAsync();
        return Ok(fallback);
    }

    /// <summary>محصولات تازه/تجدیدشده که هم‌اکنون موجود هستند.</summary>
    [HttpGet("restocked")]
    public async Task<ActionResult<List<ProductDto>>> GetRestocked([FromQuery] int take = 8)
    {
        take = Math.Clamp(take, 1, 24);
        var result = await ProjectProductCards(
                _context.Products.AsNoTracking()
                    .Where(p => !p.IsDraft && p.IsAvailable && p.StockQuantity > 0)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(take))
            .ToListAsync();
        return Ok(result);
    }

    // ===================== مدیریت محصولات (فقط ادمین) =====================

    private static IQueryable<Product> ApplyAdminFilters(
        IQueryable<Product> query,
        string? search,
        int? categoryId,
        string? status,
        string? stock,
        int lowStockThreshold,
        bool? featured,
        bool onlyDiscounted,
        decimal? minPrice,
        decimal? maxPrice,
        out DateTimeOffset now)
    {
        now = DateTimeOffset.UtcNow;
        var now2 = now;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p =>
                p.Name.Contains(s)
                || (p.Sku != null && p.Sku.Contains(s))
                || p.Id.ToString().Contains(s)
                || (p.Description != null && p.Description.Contains(s)));
        }

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        switch (status?.ToLowerInvariant())
        {
            case "available":
                query = query.Where(p => p.IsAvailable && p.StockQuantity > 0);
                break;
            case "unavailable":
                query = query.Where(p => !p.IsAvailable || p.StockQuantity <= 0);
                break;
        }

        switch (stock?.ToLowerInvariant())
        {
            case "out":
                query = query.Where(p => p.StockQuantity <= 0);
                break;
            case "low":
                query = query.Where(p => p.StockQuantity > 0 && p.StockQuantity <= lowStockThreshold);
                break;
            case "in":
                query = query.Where(p => p.StockQuantity > lowStockThreshold);
                break;
        }

        if (featured.HasValue)
            query = query.Where(p => p.IsFeatured == featured.Value);

        if (onlyDiscounted)
        {
            query = query.Where(p => p.DiscountPrice != null
                && (!p.DiscountStartDate.HasValue || p.DiscountStartDate.Value <= now2)
                && (!p.DiscountExpiryDate.HasValue || p.DiscountExpiryDate.Value > now2));
        }

        if (minPrice.HasValue || maxPrice.HasValue)
        {
            var lo = minPrice ?? decimal.MinValue;
            var hi = maxPrice ?? decimal.MaxValue;
            query = query.Where(p => p.Price >= lo && p.Price <= hi);
        }

        return query;
    }

    private static IQueryable<Product> ApplyAdminSort(IQueryable<Product> query, string? sort, DateTimeOffset now)
    {
        return sort?.ToLowerInvariant() switch
        {
            "name_asc" => query.OrderBy(p => p.Name),
            "name_desc" => query.OrderByDescending(p => p.Name),
            "price_asc" => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            "stock_asc" => query.OrderBy(p => p.StockQuantity),
            "stock_desc" => query.OrderByDescending(p => p.StockQuantity),
            "views" => query.OrderByDescending(p => p.ViewCount),
            "oldest" => query.OrderBy(p => p.CreatedAt),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };
    }

    [Authorize(Policy = "perm:admin.products.view")]
    [HttpGet("admin/list")]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAdminProducts(
        string? search = null,
        int? categoryId = null,
        string? status = null,
        string? stock = null,
        bool? featured = null,
        bool onlyDiscounted = false,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? sort = null,
        int page = 1,
        int pageSize = 25)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(1, page);

        var query = _context.Products.AsNoTracking().AsQueryable();
        query = ApplyAdminFilters(query, search, categoryId, status, stock, LowStockThreshold, featured, onlyDiscounted, minPrice, maxPrice, out var now);

        var totalCount = await query.CountAsync();
        Response.Headers["X-Total-Count"] = totalCount.ToString();

        query = ApplyAdminSort(query, sort, now);

        var result = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Sku = p.Sku,
                Description = p.Description,
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                DiscountStartDate = p.DiscountStartDate,
                DiscountExpiryDate = p.DiscountExpiryDate,
                MainImageUrl = p.MainImageUrl,
                ImageUrl2 = p.ImageUrl2,
                ImageUrl3 = p.ImageUrl3,
                ImageUrl4 = p.ImageUrl4,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                IsAvailable = p.IsAvailable,
                IsDraft = p.IsDraft,
                StockQuantity = p.StockQuantity,
                IsFeatured = p.IsFeatured,
                SortOrder = p.SortOrder,
                CreatedAt = p.CreatedAt,
                CompatibilityInfo = p.CompatibilityInfo,
                EstimatedDeliveryText = p.EstimatedDeliveryText,
                DatasheetUrl = p.DatasheetUrl,
                FailureSymptoms = p.FailureSymptoms,
                RelatedProductIds = p.RelatedProductIds,
                ViewCount = p.ViewCount
            })
            .ToListAsync();

        return Ok(result);
    }

    /// <summary>دریافت یک محصول برای ویرایش در پنل مدیریت؛ شامل محصولات پیش‌نویس.</summary>
    [Authorize(Policy = "perm:admin.products.view")]
    [HttpGet("admin/{id:int}")]
    public async Task<ActionResult<ProductDto>> GetAdminProduct(int id)
    {
        var product = await _context.Products.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.Id == id)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Sku = p.Sku,
                Description = p.Description,
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                DiscountStartDate = p.DiscountStartDate,
                DiscountExpiryDate = p.DiscountExpiryDate,
                MainImageUrl = p.MainImageUrl,
                ImageUrl2 = p.ImageUrl2,
                ImageUrl3 = p.ImageUrl3,
                ImageUrl4 = p.ImageUrl4,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                IsAvailable = p.IsAvailable,
                IsDraft = p.IsDraft,
                StockQuantity = p.StockQuantity,
                IsFeatured = p.IsFeatured,
                SortOrder = p.SortOrder,
                CreatedAt = p.CreatedAt,
                CompatibilityInfo = p.CompatibilityInfo,
                EstimatedDeliveryText = p.EstimatedDeliveryText,
                DatasheetUrl = p.DatasheetUrl,
                FailureSymptoms = p.FailureSymptoms,
                RelatedProductIds = p.RelatedProductIds,
                ViewCount = p.ViewCount
            })
            .FirstOrDefaultAsync();

        if (product == null)
            return NotFound("محصول یافت نشد");

        return Ok(product);
    }

    [Authorize(Policy = "perm:admin.products.view")]
    [HttpGet("admin/summary")]
    public async Task<ActionResult<ProductAdminSummaryDto>> GetAdminSummary()
    {
        var threshold = LowStockThreshold;

        var nowSum = DateTimeOffset.UtcNow;
        var total = await _context.Products.CountAsync();
        var available = await _context.Products.CountAsync(p => p.IsAvailable && p.StockQuantity > 0);
        var outOfStock = await _context.Products.CountAsync(p => p.StockQuantity <= 0);
        var lowStock = await _context.Products.CountAsync(p => p.StockQuantity > 0 && p.StockQuantity <= threshold);
        var discounted = await _context.Products.CountAsync(p => p.DiscountPrice != null
            && (!p.DiscountStartDate.HasValue || p.DiscountStartDate.Value <= nowSum)
            && (!p.DiscountExpiryDate.HasValue || p.DiscountExpiryDate.Value > nowSum));
        var totalStock = await _context.Products.SumAsync(p => (int?)p.StockQuantity) ?? 0;
        var inventoryValue = await _context.Products.SumAsync(p =>
            (decimal?)(((p.DiscountPrice != null
                && (!p.DiscountStartDate.HasValue || p.DiscountStartDate.Value <= nowSum)
                && (!p.DiscountExpiryDate.HasValue || p.DiscountExpiryDate.Value > nowSum))
                ? p.DiscountPrice : p.Price) * p.StockQuantity)) ?? 0;
        var categories = await _context.Categories.CountAsync();
        var emptyCategories = await _context.Categories.CountAsync(c => !_context.Products.Any(p => p.CategoryId == c.Id));

        return new ProductAdminSummaryDto
        {
            Total = total,
            Available = available,
            Unavailable = total - available,
            OutOfStock = outOfStock,
            LowStock = lowStock,
            Discounted = discounted,
            TotalStock = totalStock,
            LowStockThreshold = threshold,
            InventoryValue = inventoryValue,
            CategoryCount = categories,
            EmptyCategoryCount = emptyCategories,
            QnaCount = await _context.ProductQuestions.CountAsync(),
            PendingQnaCount = await _context.ProductQuestions.CountAsync(q => !q.IsAdminRead || !q.IsApproved),
            ReviewCount = await _context.ProductReviews.CountAsync(r => r.ParentId == null),
            PendingReviewCount = await _context.ProductReviews.CountAsync(r => !r.IsApproved || !r.IsAdminRead)
        };
    }

    [Authorize(Policy = "perm:admin.products.view")]
    [HttpGet("admin/export")]
    public async Task<IActionResult> ExportCsv(
        string? search = null,
        int? categoryId = null,
        string? status = null,
        string? stock = null,
        bool? featured = null,
        bool onlyDiscounted = false,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? sort = null)
    {
        var query = _context.Products.AsNoTracking().AsQueryable();
        query = ApplyAdminFilters(query, search, categoryId, status, stock, LowStockThreshold, featured, onlyDiscounted, minPrice, maxPrice, out var now);
        query = ApplyAdminSort(query, sort, now);

        var rows = await query
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                Category = p.Category.Name,
                p.Price,
                p.DiscountPrice,
                p.StockQuantity,
                p.IsAvailable,
                p.IsFeatured,
                p.ViewCount,
                p.Slug,
                p.CreatedAt
            })
            .Take(5000)
            .ToListAsync();

        static string Csv(string? v)
        {
            v ??= string.Empty;
            if (v.Contains('"') || v.Contains(',') || v.Contains('\n') || v.Contains('\r'))
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ID,Name,SKU,Category,Price,DiscountPrice,Stock,Available,Featured,Views,Slug,CreatedAt");
        foreach (var r in rows)
        {
            sb.Append(r.Id).Append(',')
              .Append(Csv(r.Name)).Append(',')
              .Append(Csv(r.Sku)).Append(',')
              .Append(Csv(r.Category)).Append(',')
              .Append(r.Price).Append(',')
              .Append(r.DiscountPrice?.ToString() ?? "").Append(',')
              .Append(r.StockQuantity).Append(',')
              .Append(r.IsAvailable ? "1" : "0").Append(',')
              .Append(r.IsFeatured ? "1" : "0").Append(',')
              .Append(r.ViewCount).Append(',')
              .Append(Csv(r.Slug)).Append(',')
              .Append(r.CreatedAt.ToString("yyyy-MM-dd"))
              .AppendLine();
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"products-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    [HttpGet("{id:int}/price-history")]
    [OutputCache(Duration = 300, Tags = new[] { ProductsController.CacheTag })]
    public async Task<ActionResult<List<ProductPricePointDto>>> GetPriceHistory(int id, int days = 0)
    {
        var query = _context.ProductPriceHistories.AsNoTracking()
            .Where(x => x.ProductId == id);

        if (days > 0)
        {
            var since = DateTimeOffset.UtcNow.AddDays(-days);
            query = query.Where(x => x.RecordedAt >= since);
        }

        var points = await query
            .OrderBy(x => x.RecordedAt)
            .Select(x => new ProductPricePointDto
            {
                Id = x.Id,
                Price = x.Price,
                DiscountPrice = x.DiscountPrice,
                DateFa = "",
                RecordedAt = x.RecordedAt
            })
            .ToListAsync();

        foreach (var pt in points)
            pt.DateFa = PersianDateHelper.ToPersianDateTimeString(pt.RecordedAt, includeTime: false);

        if (points.Count == 0)
        {
            var p = await _context.Products.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.Price, x.DiscountPrice, x.CreatedAt })
                .FirstOrDefaultAsync();
            if (p == null) return NotFound("محصول یافت نشد");
            points.Add(new ProductPricePointDto
            {
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                DateFa = PersianDateHelper.ToPersianDateTimeString(p.CreatedAt, includeTime: false),
                RecordedAt = p.CreatedAt
            });
        }

        return points;
    }

    [HttpPost("{id:int}/price-history")]
    [Authorize(Policy = "perm:admin.products.manage")]
    public async Task<IActionResult> AddPriceHistoryPoint(int id, [FromBody] ProductPricePointRequest? request)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound("محصول یافت نشد");

        var price = request?.Price ?? product.Price;
        if (price <= 0) return BadRequest("قیمت رکورد باید بزرگتر از صفر باشد");

        _context.ProductPriceHistories.Add(new ProductPriceHistory
        {
            ProductId = id,
            Price = price,
            DiscountPrice = request?.DiscountPrice,
            RecordedAt = request?.RecordedAt ?? product.CreatedAt
        });
        await _context.SaveChangesAsync();
        try { await _outputCache.EvictByTagAsync(CacheTag, CancellationToken.None); } catch { }
        return Ok();
    }

    [HttpPut("{id:int}/price-history/{pointId:int}")]
    [Authorize(Policy = "perm:admin.products.manage")]
    public async Task<IActionResult> UpdatePriceHistoryPoint(int id, int pointId, [FromBody] ProductPricePointRequest request)
    {
        var point = await _context.ProductPriceHistories
            .FirstOrDefaultAsync(x => x.Id == pointId && x.ProductId == id);
        if (point == null) return NotFound("رکورد یافت نشد");

        if (request.Price.HasValue)
        {
            if (request.Price.Value <= 0) return BadRequest("قیمت رکورد باید بزرگتر از صفر باشد");
            point.Price = request.Price.Value;
        }
        if (request.DiscountPrice.HasValue)
            point.DiscountPrice = request.DiscountPrice.Value;
        if (request.RecordedAt.HasValue)
            point.RecordedAt = request.RecordedAt.Value;

        await _context.SaveChangesAsync();
        try { await _outputCache.EvictByTagAsync(CacheTag, CancellationToken.None); } catch { }
        return Ok();
    }

    [HttpDelete("{id:int}/price-history/{pointId:int}")]
    [Authorize(Policy = "perm:admin.products.manage")]
    public async Task<IActionResult> DeletePriceHistoryPoint(int id, int pointId)
    {
        var point = await _context.ProductPriceHistories
            .FirstOrDefaultAsync(x => x.Id == pointId && x.ProductId == id);
        if (point == null) return NotFound("رکورد یافت نشد");

        _context.ProductPriceHistories.Remove(point);
        await _context.SaveChangesAsync();
        try { await _outputCache.EvictByTagAsync(CacheTag, CancellationToken.None); } catch { }
        return Ok();
    }

    [HttpGet("slug/{slug}")]
    [OutputCache(Duration = 300, Tags = new[] { ProductsController.CacheTag })]
    public async Task<ActionResult<ProductDto>> GetProductBySlug(string slug)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Sku = p.Sku,
                Description = p.Description,
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                DiscountStartDate = p.DiscountStartDate,
                DiscountExpiryDate = p.DiscountExpiryDate,
                MainImageUrl = p.MainImageUrl,
                ImageUrl2 = p.ImageUrl2,
                ImageUrl3 = p.ImageUrl3,
                ImageUrl4 = p.ImageUrl4,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                IsAvailable = p.IsAvailable,
                StockQuantity = p.StockQuantity,
                IsFeatured = p.IsFeatured,
                SortOrder = p.SortOrder,
                CreatedAt = p.CreatedAt,
                CompatibilityInfo = p.CompatibilityInfo,
                EstimatedDeliveryText = p.EstimatedDeliveryText,
                DatasheetUrl = p.DatasheetUrl,
                FailureSymptoms = p.FailureSymptoms,
                RelatedProductIds = p.RelatedProductIds
            })
            .FirstOrDefaultAsync(p => p.Slug == slug && !p.IsDraft);

        if (product == null)
        {
            return NotFound("محصول یافت نشد");
        }

        return Ok(product);
    }

    [HttpGet("{id:int}")]
    [OutputCache(Duration = 300, Tags = new[] { ProductsController.CacheTag })]
    public async Task<ActionResult<ProductDto>> GetProduct(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Sku = p.Sku,
                Description = p.Description,
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                DiscountStartDate = p.DiscountStartDate,
                DiscountExpiryDate = p.DiscountExpiryDate,
                MainImageUrl = p.MainImageUrl,
                ImageUrl2 = p.ImageUrl2,
                ImageUrl3 = p.ImageUrl3,
                ImageUrl4 = p.ImageUrl4,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                IsAvailable = p.IsAvailable,
                StockQuantity = p.StockQuantity,
                CreatedAt = p.CreatedAt,
                CompatibilityInfo = p.CompatibilityInfo,
                EstimatedDeliveryText = p.EstimatedDeliveryText,
                DatasheetUrl = p.DatasheetUrl,
                FailureSymptoms = p.FailureSymptoms,
                RelatedProductIds = p.RelatedProductIds
            })
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDraft);

        if (product == null)
        {
            return NotFound("محصول یافت نشد");
        }

        return Ok(product);
    }

    private async Task<string?> ValidateSkuUniqueAsync(string? sku, int? excludeId)
    {
        var normalized = sku?.Trim();
        if (string.IsNullOrEmpty(normalized))
            return null;

        var duplicate = await _context.Products.AnyAsync(p =>
            p.Sku != null && p.Sku == normalized && (excludeId == null || p.Id != excludeId));
        return duplicate ? "کد محصول (SKU) تکراری است؛ کد دیگری وارد کنید." : null;
    }

    [Authorize(Policy = "perm:admin.products.manage")]
    [HttpPost]
    public async Task<ActionResult<ProductDto>> PostProduct(ProductUpsertRequest request)
    {
        if (request.Price <= 0) return BadRequest("قیمت محصول معتبر نیست");
        var skuError = await ValidateSkuUniqueAsync(request.Sku, null);
        if (skuError != null)
            return BadRequest(skuError);

        var product = new Product
        {
            Name = request.Name,
            Slug = await SlugService.ResolveUniqueAsync(
                _context.Products.Where(p => p.Slug != null).Select(p => p.Slug!),
                request.Name),
            Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim(),
            Description = request.Description,
            Price = request.Price,
            DiscountPrice = request.DiscountPrice,
            DiscountStartDate = request.DiscountStartDate,
            DiscountExpiryDate = request.DiscountExpiryDate,
            MainImageUrl = request.MainImageUrl,
            ImageUrl2 = request.ImageUrl2,
            ImageUrl3 = request.ImageUrl3,
            ImageUrl4 = request.ImageUrl4,
            CategoryId = request.CategoryId,
            IsAvailable = request.IsAvailable,
            IsDraft = request.IsDraft,
            StockQuantity = request.StockQuantity,
            IsFeatured = request.IsFeatured,
            SortOrder = request.SortOrder,
            CompatibilityInfo = request.CompatibilityInfo,
            EstimatedDeliveryText = request.EstimatedDeliveryText,
            DatasheetUrl = request.DatasheetUrl,
            FailureSymptoms = request.FailureSymptoms,
            RelatedProductIds = request.RelatedProductIds,
            CreatedAt = request.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (product.StockQuantity <= 0) product.IsAvailable = false;

        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        _context.ProductPriceHistories.Add(new ProductPriceHistory
        {
            ProductId = product.Id,
            Price = product.Price,
            DiscountPrice = product.DiscountPrice,
            RecordedAt = product.CreatedAt
        });
        await _context.SaveChangesAsync();
        await InvalidateProductCacheAsync();

        return Ok(new ProductDto { Id = product.Id, Name = product.Name, Slug = product.Slug });
    }

    [Authorize(Policy = "perm:admin.products.manage")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutProduct(int id, ProductUpsertRequest request)
    {
        if (request.Price <= 0) return BadRequest("قیمت محصول معتبر نیست");
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound("محصول یافت نشد");
        }

        var skuError = await ValidateSkuUniqueAsync(request.Sku, id);
        if (skuError != null)
            return BadRequest(skuError);

        var oldName = product.Name;
        var priceChanged = product.Price != request.Price || product.DiscountPrice != request.DiscountPrice;
        var wasOutOfStock = !product.IsAvailable || product.StockQuantity <= 0;
        product.Name = request.Name;
        product.Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim();
        product.Description = request.Description;
        product.Price = request.Price;
        product.DiscountPrice = request.DiscountPrice;
        product.DiscountStartDate = request.DiscountStartDate;
        product.DiscountExpiryDate = request.DiscountExpiryDate;
        product.Slug = await SlugService.ResolveForUpdateAsync(
            _context.Products.Where(p => p.Slug != null && p.Id != id).Select(p => p.Slug!),
            oldName, product.Slug, request.Name);
        FileCleanupHelper.DeleteOldFileIfChanged(product.MainImageUrl, request.MainImageUrl, _env);
        product.MainImageUrl = request.MainImageUrl;
        FileCleanupHelper.DeleteOldFileIfChanged(product.ImageUrl2, request.ImageUrl2, _env);
        product.ImageUrl2 = request.ImageUrl2;
        FileCleanupHelper.DeleteOldFileIfChanged(product.ImageUrl3, request.ImageUrl3, _env);
        product.ImageUrl3 = request.ImageUrl3;
        FileCleanupHelper.DeleteOldFileIfChanged(product.ImageUrl4, request.ImageUrl4, _env);
        product.ImageUrl4 = request.ImageUrl4;
        product.CategoryId = request.CategoryId;
        product.IsAvailable = request.IsAvailable;
        product.IsDraft = request.IsDraft;
        product.StockQuantity = request.StockQuantity;
        product.IsFeatured = request.IsFeatured;
        product.SortOrder = request.SortOrder;
        if (product.StockQuantity <= 0) product.IsAvailable = false;
        product.CompatibilityInfo = request.CompatibilityInfo;
        product.EstimatedDeliveryText = request.EstimatedDeliveryText;
        product.DatasheetUrl = request.DatasheetUrl;
        product.FailureSymptoms = request.FailureSymptoms;
        product.RelatedProductIds = request.RelatedProductIds;
        if (request.CreatedAt.HasValue)
            product.CreatedAt = request.CreatedAt.Value;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
        if (priceChanged)
        {
            _context.ProductPriceHistories.Add(new ProductPriceHistory
            {
                ProductId = product.Id,
                Price = product.Price,
                DiscountPrice = product.DiscountPrice,
                RecordedAt = product.CreatedAt
            });
            await _context.SaveChangesAsync();
        }
        await InvalidateProductCacheAsync();

        var isRestocked = wasOutOfStock && product.IsAvailable && product.StockQuantity > 0;
        if (isRestocked)
            await _stockAlerts.NotifyRestockedAsync(new[] { product.Id });

        return NoContent();
    }

    [Authorize(Policy = "perm:admin.products.manage")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound("محصول یافت نشد");
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        await InvalidateProductCacheAsync();
        return NoContent();
    }
}
