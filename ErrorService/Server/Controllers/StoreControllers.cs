using ErrorService.Server.Data;
using ErrorService.Server.Infrastructure;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ErrorServiceDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public CategoriesController(ErrorServiceDbContext context, IWebHostEnvironment env, IConfiguration configuration)
    {
        _context = context;
        _env = env;
        _configuration = configuration;
    }

    [HttpGet]
    [OutputCache(Duration = 3600)]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories()
    {
        return await _context.Categories
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Icon = c.Icon,
                SortOrder = c.SortOrder
            }).ToListAsync();
    }

    [Authorize(Policy = "perm:admin.categories.manage")]
    [HttpPost]
    public async Task<ActionResult<CategoryDto>> PostCategory(CategoryDto categoryDto)
    {
        var category = new Category
        {
            Name = categoryDto.Name,
            Icon = categoryDto.Icon,
            SortOrder = categoryDto.SortOrder
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

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

        category.Name = categoryDto.Name;
        FileCleanupHelper.DeleteOldFileIfChanged(category.Icon, categoryDto.Icon, _env);
        category.Icon = categoryDto.Icon;
        category.SortOrder = categoryDto.SortOrder;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!CategoryExists(id)) return NotFound();
            else throw;
        }

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

        return NoContent();
    }

    private bool CategoryExists(int id) => _context.Categories.Any(e => e.Id == id);
}

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly ErrorServiceDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public ProductsController(ErrorServiceDbContext context, IWebHostEnvironment env, IConfiguration configuration)
    {
        _context = context;
        _env = env;
        _configuration = configuration;
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

    [HttpGet]
    [OutputCache(Duration = 120, VaryByQueryKeys = new[] { "*" })]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts(
        int? categoryId = null,
        bool onlyAvailable = false,
        bool onlyDiscounted = false,
        string? search = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? sort = null,
        int skip = 0,
        int take = 10)
    {
        var query = _context.Products.Include(p => p.Category).AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (onlyAvailable)
            query = query.Where(p => p.IsAvailable);

        if (onlyDiscounted)
        {
            var now = DateTimeOffset.Now;
            query = query.Where(p => p.DiscountPrice != null && p.DiscountExpiryDate != null && p.DiscountExpiryDate > now);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p => p.Name.Contains(s) || (p.Description != null && p.Description.Contains(s)));
        }

        if (minPrice.HasValue)
        {
            query = query.Where(p => (p.DiscountPrice ?? p.Price) >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(p => (p.DiscountPrice ?? p.Price) <= maxPrice.Value);
        }

        var totalCount = await query.CountAsync();
        Response.Headers["X-Total-Count"] = totalCount.ToString();

        query = sort?.ToLowerInvariant() switch
        {
            "price_asc" => query.OrderBy(p => p.DiscountPrice ?? p.Price).ThenByDescending(p => p.CreatedAt),
            "price_desc" => query.OrderByDescending(p => p.DiscountPrice ?? p.Price).ThenByDescending(p => p.CreatedAt),
            "discount_desc" => query
                .OrderByDescending(p => p.DiscountPrice != null ? (p.Price - p.DiscountPrice.Value) : 0)
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
                DatasheetUrl = p.DatasheetUrl,
                FailureSymptoms = p.FailureSymptoms,
                RelatedProductIds = p.RelatedProductIds
            }).ToListAsync();

        return Ok(result);
    }

    [HttpGet("slug/{slug}")]
    [OutputCache(Duration = 300)]
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
                DatasheetUrl = p.DatasheetUrl,
                FailureSymptoms = p.FailureSymptoms,
                RelatedProductIds = p.RelatedProductIds
            })
            .FirstOrDefaultAsync(p => p.Slug == slug);

        if (product == null)
        {
            return NotFound("محصول یافت نشد");
        }

        return Ok(product);
    }

    [HttpGet("{id:int}")]
    [OutputCache(Duration = 300)]
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
                DatasheetUrl = p.DatasheetUrl,
                FailureSymptoms = p.FailureSymptoms,
                RelatedProductIds = p.RelatedProductIds
            })
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound("محصول یافت نشد");
        }

        return Ok(product);
    }

    [Authorize(Policy = "perm:admin.products.manage")]
    [HttpPost]
    public async Task<ActionResult<ProductDto>> PostProduct(ProductUpsertRequest request)
    {
        var product = new Product
        {
            Name = request.Name,
            Slug = await SlugService.ResolveUniqueAsync(
                _context.Products.Where(p => p.Slug != null).Select(p => p.Slug!),
                request.Name),
            Sku = request.Sku,
            Description = request.Description,
            Price = request.Price,
            DiscountPrice = request.DiscountPrice,
            DiscountExpiryDate = request.DiscountExpiryDate,
            MainImageUrl = request.MainImageUrl,
            ImageUrl2 = request.ImageUrl2,
            ImageUrl3 = request.ImageUrl3,
            ImageUrl4 = request.ImageUrl4,
            CategoryId = request.CategoryId,
            IsAvailable = request.IsAvailable,
            StockQuantity = request.StockQuantity,
            CompatibilityInfo = request.CompatibilityInfo,
            DatasheetUrl = request.DatasheetUrl,
            FailureSymptoms = request.FailureSymptoms,
            RelatedProductIds = request.RelatedProductIds,
            CreatedAt = request.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (product.StockQuantity <= 0) product.IsAvailable = false;

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return Ok(new ProductDto { Id = product.Id, Name = product.Name, Slug = product.Slug });
    }

    [Authorize(Policy = "perm:admin.products.manage")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> PutProduct(int id, ProductUpsertRequest request)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound("محصول یافت نشد");
        }

        var oldName = product.Name;
        product.Name = request.Name;
        product.Sku = request.Sku;
        product.Description = request.Description;
        product.Price = request.Price;
        product.DiscountPrice = request.DiscountPrice;
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
        product.StockQuantity = request.StockQuantity;
        if (product.StockQuantity <= 0) product.IsAvailable = false;
        product.CompatibilityInfo = request.CompatibilityInfo;
        product.DatasheetUrl = request.DatasheetUrl;
        product.FailureSymptoms = request.FailureSymptoms;
        product.RelatedProductIds = request.RelatedProductIds;
        if (request.CreatedAt.HasValue)
            product.CreatedAt = request.CreatedAt.Value;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync();
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
        return NoContent();
    }
}
