using System.Globalization;
using System.Text.Json;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/about")]
public sealed class AboutController : ControllerBase
{
    private const string CacheTag = "about-page";

    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private static readonly JsonSerializerOptions FeatureJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly ErrorServiceDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IOutputCacheStore _outputCache;

    public AboutController(ErrorServiceDbContext db, IWebHostEnvironment env, IOutputCacheStore outputCache)
    {
        _db = db;
        _env = env;
        _outputCache = outputCache;
    }

    [HttpGet]
    [AllowAnonymous]
    [OutputCache(Duration = 600, Tags = new[] { CacheTag, "error-codes", "academy" })]
    public async Task<ActionResult<AboutPageDto>> Get(CancellationToken ct)
    {
        var content = await LoadContentAsync(ct);
        var dto = MapToDto(content);
        dto.Stats = await BuildStatsAsync(dto.FoundingYear, ct);
        return Ok(dto);
    }

    [Authorize(Policy = "perm:admin.about.manage")]
    [HttpGet("admin")]
    public async Task<ActionResult<AboutPageDto>> GetAdmin(CancellationToken ct)
    {
        var content = await LoadContentAsync(ct);
        return Ok(MapToDto(content));
    }

    [Authorize(Policy = "perm:admin.about.manage")]
    [HttpPut]
    public async Task<IActionResult> Update(AboutPageDto dto, CancellationToken ct)
    {
        var content = await _db.AboutPageContents.FirstOrDefaultAsync(ct);
        if (content == null)
        {
            content = new AboutPageContent();
            _db.AboutPageContents.Add(content);
        }

        Apply(dto, content);
        content.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        try { await _outputCache.EvictByTagAsync(CacheTag, CancellationToken.None); } catch { }
        ErrorService.Server.Infrastructure.SeoFallbackMiddleware.InvalidateHtmlCache();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.about.manage")]
    [HttpPost("upload")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<string>> Upload([FromForm] IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length <= 0)
            return BadRequest("فایل خالی است یا دریافت نشد");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
            return BadRequest("فرمت تصویر مجاز نیست. فقط jpg, jpeg, png, webp");

        var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var uploadsRoot = Path.Combine(wwwroot, "uploads", "about");
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        await using (var outStream = System.IO.File.Create(Path.Combine(uploadsRoot, fileName)))
        {
            await file.CopyToAsync(outStream, ct);
        }

        return Ok($"/uploads/about/{fileName}");
    }

    private async Task<AboutPageContent?> LoadContentAsync(CancellationToken ct)
        => await _db.AboutPageContents.AsNoTracking().FirstOrDefaultAsync(ct);

    private async Task<AboutStatsDto> BuildStatsAsync(int foundingYear, CancellationToken ct)
    {
        // آمار «مانیتورینگ» مثل نوار صفحهٔ اصلی تجمعی محاسبه می‌شود تا با حذف
        // یا پایان یک رکورد کاهش پیدا نکند.
        var monitoringRows = await _db.MonitoringReceiptConnections.AsNoTracking().CountAsync(ct);
        var maxConnectionId = await _db.MonitoringReceiptConnections.AsNoTracking()
            .MaxAsync(c => (int?)c.Id, ct) ?? 0;
        var maxArchivedConnectionId = await _db.MonitoringDataArchives.AsNoTracking()
            .MaxAsync(a => (int?)a.MonitoringReceiptConnectionId, ct) ?? 0;

        var courses = await _db.TrainingCourses.AsNoTracking().CountAsync(c => c.IsPublished, ct);
        var articles = await _db.TrainingArticles.AsNoTracking().CountAsync(a => a.IsPublished, ct);

        return new AboutStatsDto
        {
            ErrorCodesCount = await _db.ErrorCodes.AsNoTracking().CountAsync(ct),
            ServicesCount = await _db.CustomerReceipts.AsNoTracking().CountAsync(ct),
            ProductsCount = await _db.Products.AsNoTracking().CountAsync(p => !p.IsDraft, ct),
            AcademyItemsCount = courses + articles,
            MonitoringCount = Math.Max(monitoringRows, Math.Max(maxConnectionId, maxArchivedConnectionId)),
            WorkshopsCount = await _db.Workshops.AsNoTracking().CountAsync(w => !w.IsDeleted && w.IsActive, ct),
            ExperienceYears = CalcExperienceYears(foundingYear)
        };
    }

    private static int CalcExperienceYears(int foundingYear)
    {
        if (foundingYear < 1300 || foundingYear > 1450) foundingYear = 1394;
        var current = new PersianCalendar().GetYear(DateTime.Now);
        return Math.Clamp(current - foundingYear, 0, 200);
    }

    private static AboutPageDto CreateDefaults() => new()
    {
        HeroBadge = "درباره ارورسرویس",
        HeroTitle = "مرجع تخصصی کد خطا، تعمیر و آموزش لوازم خانگی",
        HeroSubtitle = "از بانک کدهای خطا و فروش قطعات تا خدمات تعمیر و آموزش فنی؛ ارورسرویس با تکیه بر تجربهٔ کارگاهی و داده‌های واقعی در کنار شماست.",
        StoryTitle = "داستان ما",
        StoryText = "ارورسرویس با یک هدف ساده شروع شد: دسترسی سریع و دقیق به اطلاعات فنی لوازم خانگی. آنچه با گردآوری کدهای خطا آغاز شد، امروز به مجموعه‌ای از فروشگاه قطعات، خدمات تعمیر و آموزش تخصصی تبدیل شده است.\n\nهر روز کد خطای جدیدی به بانک اطلاعاتی اضافه می‌شود، قطعات بررسی و ثبت می‌شوند و تجربهٔ تعمیر کارگاه‌ها به محتوای آموزشی تبدیل می‌شود. آمار همین صفحه از دادهٔ زندهٔ دیتابیس خوانده می‌شود؛ نه عددی برای نمایش.",
        MissionTitle = "رسالت ما",
        MissionText = "دسترسی سریع، دقیق و رایگان به اطلاعات فنی؛ فروش قطعات اصل و ارائهٔ خدمات تعمیر با کیفیتی که بشود روی آن حساب کرد.",
        VisionTitle = "چشم‌انداز",
        VisionText = "تبدیل شدن به مرجع اول کدهای خطا و آموزش تعمیرات لوازم خانگی در ایران.",
        ValuesTitle = "ارزش‌ها",
        ValuesText = "دقت فنی، شفافیت در قیمت و صادقانه گفتن آنچه یک دستگاه واقعاً به آن نیاز دارد.",
        FeaturesTitle = "ارورسرویس چه می‌کند؟",
        FeaturesSubtitle = "چهار مسیر اصلی که در سایت دنبال می‌کنیم",
        Features = new List<AboutFeatureDto>
        {
            new() { Icon = "list", Title = "بانک کدهای خطا", Description = "جستجوی سریع کد خطا بر اساس برند و دستگاه، همراه با علت‌های محتمل و راه‌حل." },
            new() { Icon = "cart", Title = "فروشگاه قطعات", Description = "قطعات یدکی و مصرفی با مشخصات دقیق، موجودی واقعی و امکان ثبت سفارش آنلاین." },
            new() { Icon = "wrench", Title = "تعمیر تخصصی", Description = "ثبت درخواست تعمیر و پیگیری وضعیت دستگاه از ابتدا تا تحویل." },
            new() { Icon = "book", Title = "آموزش و آکادمی", Description = "مقالات و درس‌های کاربردی برای یادگیری عیب‌یابی و تعمیر لوازم خانگی." }
        },
        ShowStats = true,
        ShowStatErrorCodes = true,
        ShowStatServices = true,
        ShowStatProducts = true,
        ShowStatAcademy = true,
        ShowStatMonitoring = true,
        ShowStatWorkshops = true,
        ShowStatExperience = true,
        FoundingYear = 1394,
        ShowTeam = true,
        ShowTestimonials = true,
        CtaTitle = "سوال فنی دارید؟",
        CtaText = "تیم پشتیبانی ارورسرویس آمادهٔ راهنمایی شماست؛ برای مشاورهٔ خرید قطعه یا عیب‌یابی دستگاه با ما در تماس باشید.",
        CtaButtonText = "تماس با ما",
        CtaButtonUrl = "/contact",
        SeoTitle = "درباره ارورسرویس | فروشگاه قطعات، تعمیرات و آموزش لوازم خانگی",
        SeoDescription = "ارورسرویس؛ بانک کدهای خطا، فروشگاه قطعات یدکی و خدمات تعمیر تخصصی لوازم خانگی با تیمی از کارشناسان فنی.",
        SeoKeywords = "درباره ارورسرویس, تعمیر لوازم خانگی, کدهای خطا, فروشگاه قطعات الکترونیک, آموزش تعمیرات"
    };

    private static AboutPageDto MapToDto(AboutPageContent? content)
    {
        var defaults = CreateDefaults();
        if (content == null) return defaults;

        var dto = new AboutPageDto
        {
            HeroBadge = OrDefault(content.HeroBadge, defaults.HeroBadge),
            HeroTitle = OrDefault(content.HeroTitle, defaults.HeroTitle),
            HeroSubtitle = OrDefault(content.HeroSubtitle, defaults.HeroSubtitle),
            HeroImageUrl = string.IsNullOrWhiteSpace(content.HeroImageUrl) ? null : content.HeroImageUrl,
            StoryTitle = OrDefault(content.StoryTitle, defaults.StoryTitle),
            StoryText = OrDefault(content.StoryText, defaults.StoryText),
            MissionTitle = OrDefault(content.MissionTitle, defaults.MissionTitle),
            MissionText = OrDefault(content.MissionText, defaults.MissionText),
            VisionTitle = OrDefault(content.VisionTitle, defaults.VisionTitle),
            VisionText = OrDefault(content.VisionText, defaults.VisionText),
            ValuesTitle = OrDefault(content.ValuesTitle, defaults.ValuesTitle),
            ValuesText = OrDefault(content.ValuesText, defaults.ValuesText),
            FeaturesTitle = OrDefault(content.FeaturesTitle, defaults.FeaturesTitle),
            FeaturesSubtitle = content.FeaturesSubtitle ?? defaults.FeaturesSubtitle,
            Features = ParseFeatures(content.FeaturesJson) ?? defaults.Features,
            ShowStats = content.ShowStats,
            ShowStatErrorCodes = content.ShowStatErrorCodes,
            ShowStatServices = content.ShowStatServices,
            ShowStatProducts = content.ShowStatProducts,
            ShowStatAcademy = content.ShowStatAcademy,
            ShowStatMonitoring = content.ShowStatMonitoring,
            ShowStatWorkshops = content.ShowStatWorkshops,
            ShowStatExperience = content.ShowStatExperience,
            FoundingYear = content.FoundingYear is >= 1300 and <= 1450 ? content.FoundingYear : defaults.FoundingYear,
            ShowTeam = content.ShowTeam,
            ShowTestimonials = content.ShowTestimonials,
            CtaTitle = OrDefault(content.CtaTitle, defaults.CtaTitle),
            CtaText = OrDefault(content.CtaText, defaults.CtaText),
            CtaButtonText = OrDefault(content.CtaButtonText, defaults.CtaButtonText),
            CtaButtonUrl = NormalizeUrl(content.CtaButtonUrl, defaults.CtaButtonUrl) ?? defaults.CtaButtonUrl,
            SeoTitle = OrDefault(content.SeoTitle, defaults.SeoTitle),
            SeoDescription = OrDefault(content.SeoDescription, defaults.SeoDescription),
            SeoKeywords = OrDefault(content.SeoKeywords, defaults.SeoKeywords)
        };
        return dto;
    }

    private static void Apply(AboutPageDto dto, AboutPageContent content)
    {
        content.HeroBadge = TrimTo(dto.HeroBadge, 120);
        content.HeroTitle = TrimTo(dto.HeroTitle, 200);
        content.HeroSubtitle = TrimTo(dto.HeroSubtitle, 600);
        content.HeroImageUrl = NormalizeUrl(dto.HeroImageUrl, null);
        content.StoryTitle = TrimTo(dto.StoryTitle, 120);
        content.StoryText = TrimTo(dto.StoryText, 6000);
        content.MissionTitle = TrimTo(dto.MissionTitle, 120);
        content.MissionText = TrimTo(dto.MissionText, 600);
        content.VisionTitle = TrimTo(dto.VisionTitle, 120);
        content.VisionText = TrimTo(dto.VisionText, 600);
        content.ValuesTitle = TrimTo(dto.ValuesTitle, 120);
        content.ValuesText = TrimTo(dto.ValuesText, 600);
        content.FeaturesTitle = TrimTo(dto.FeaturesTitle, 160);
        content.FeaturesSubtitle = TrimTo(dto.FeaturesSubtitle, 400);
        content.FeaturesJson = JsonSerializer.Serialize(SanitizeFeatures(dto.Features), FeatureJsonOptions);
        content.ShowStats = dto.ShowStats;
        content.ShowStatErrorCodes = dto.ShowStatErrorCodes;
        content.ShowStatServices = dto.ShowStatServices;
        content.ShowStatProducts = dto.ShowStatProducts;
        content.ShowStatAcademy = dto.ShowStatAcademy;
        content.ShowStatMonitoring = dto.ShowStatMonitoring;
        content.ShowStatWorkshops = dto.ShowStatWorkshops;
        content.ShowStatExperience = dto.ShowStatExperience;
        content.FoundingYear = dto.FoundingYear is >= 1300 and <= 1450 ? dto.FoundingYear : 1394;
        content.ShowTeam = dto.ShowTeam;
        content.ShowTestimonials = dto.ShowTestimonials;
        content.CtaTitle = TrimTo(dto.CtaTitle, 200);
        content.CtaText = TrimTo(dto.CtaText, 600);
        content.CtaButtonText = TrimTo(dto.CtaButtonText, 80);
        content.CtaButtonUrl = NormalizeUrl(dto.CtaButtonUrl, "/contact") ?? "/contact";
        content.SeoTitle = TrimTo(dto.SeoTitle, 200);
        content.SeoDescription = TrimTo(dto.SeoDescription, 500);
        content.SeoKeywords = TrimTo(dto.SeoKeywords, 500);
    }

    private static List<AboutFeatureDto> SanitizeFeatures(List<AboutFeatureDto>? items)
    {
        if (items == null) return new List<AboutFeatureDto>();

        return items
            .Where(x => !string.IsNullOrWhiteSpace(x.Title))
            .Take(8)
            .Select(x => new AboutFeatureDto
            {
                Icon = NormalizeIcon(x.Icon),
                Title = TrimTo(x.Title, 120),
                Description = TrimTo(x.Description, 400)
            })
            .ToList();
    }

    private static List<AboutFeatureDto>? ParseFeatures(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var items = JsonSerializer.Deserialize<List<AboutFeatureDto>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (items == null) return new List<AboutFeatureDto>();
            return items
                .Where(x => !string.IsNullOrWhiteSpace(x.Title))
                .Take(8)
                .Select(x => new AboutFeatureDto
                {
                    Icon = NormalizeIcon(x.Icon),
                    Title = x.Title,
                    Description = x.Description ?? string.Empty
                })
                .ToList();
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeIcon(string? icon)
    {
        var value = (icon ?? string.Empty).Trim().ToLowerInvariant();
        if (value.StartsWith("oi-", StringComparison.Ordinal)) value = value[3..];
        if (value.Length is 0 or > 40) return "check";
        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '-') return "check";
        }
        return value;
    }

    private static string? NormalizeUrl(string? url, string? fallback)
    {
        var value = (url ?? string.Empty).Trim();
        if (value.Length == 0) return fallback;
        if (value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal)) return value;
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return value;
        // مسیر نسبی بدون اسلش (مثلاً contact)؛ پروتکل‌های ناشناخته مثل javascript: رد می‌شوند.
        if (!value.Contains(':') && !value.Contains(".."))
            return "/" + value.TrimStart('/');
        return fallback;
    }

    private static string OrDefault(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string TrimTo(string? value, int max)
    {
        var trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
