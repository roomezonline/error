using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using ErrorService.Server.Data;
using ErrorService.Shared.Models;

namespace ErrorService.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FooterController : ControllerBase
    {
        private readonly ErrorServiceDbContext _context;
        private readonly ILogger<FooterController> _logger;
        private readonly IOutputCacheStore _cacheStore;

        public FooterController(ErrorServiceDbContext context, ILogger<FooterController> logger,
            IOutputCacheStore cacheStore)
        {
            _context = context;
            _logger = logger;
            _cacheStore = cacheStore;
        }

        [HttpGet]
        [AllowAnonymous]
        [OutputCache(Duration = 3600, Tags = new[] { "footer" })]
        public async Task<ActionResult<FooterSettingsDto>> GetFooter()
        {
            try
            {
                var footer = await _context.FooterSettings
                    .Include(f => f.LinkGroups)
                    .ThenInclude(g => g.Links)
                    .FirstOrDefaultAsync(f => f.IsActive);

                if (footer == null)
                {
                    // Return default footer
                    return Ok(GetDefaultFooter());
                }

                return Ok(MapToDto(footer));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting footer settings");
                return Ok(GetDefaultFooter());
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveFooter(SaveFooterSettingsRequest request)
        {
            try
            {
                var existing = await _context.FooterSettings
                    .Include(f => f.LinkGroups)
                    .ThenInclude(g => g.Links)
                    .FirstOrDefaultAsync();

                if (existing == null)
                {
                    // Create new
                    var newFooter = new FooterSettings
                    {
                        CompanyName = request.CompanyName,
                        CompanyDescription = request.CompanyDescription,
                        LogoUrl = request.LogoUrl,
                        PhoneNumber1 = request.PhoneNumber1,
                        PhoneNumber2 = request.PhoneNumber2,
                        WhatsAppNumber = request.WhatsAppNumber,
                        Email = request.Email,
                        Address = request.Address,
                        InstagramUrl = request.InstagramUrl,
                        TelegramUrl = request.TelegramUrl,
                        YouTubeUrl = request.YouTubeUrl,
                        LinkedInUrl = request.LinkedInUrl,
                        EnamadCode = request.EnamadCode,
                        SamandehiCode = request.SamandehiCode,
                        EtaCode = request.EtaCode,
                        CopyrightText = request.CopyrightText,
                        IsActive = true,
                        LinkGroups = request.LinkGroups.Select((g, i) => new FooterLinkGroup
                        {
                            Title = g.Title,
                            SortOrder = i,
                            Links = g.Links.Select((l, j) => new FooterLink
                            {
                                Title = l.Title,
                                Url = l.Url,
                                Icon = l.Icon,
                                SortOrder = j,
                                IsExternal = l.IsExternal
                            }).ToList()
                        }).ToList()
                    };
                    _context.FooterSettings.Add(newFooter);
                }
                else
                {
                    // Update existing
                    existing.CompanyName = request.CompanyName;
                    existing.CompanyDescription = request.CompanyDescription;
                    existing.LogoUrl = request.LogoUrl;
                    existing.PhoneNumber1 = request.PhoneNumber1;
                    existing.PhoneNumber2 = request.PhoneNumber2;
                    existing.WhatsAppNumber = request.WhatsAppNumber;
                    existing.Email = request.Email;
                    existing.Address = request.Address;
                    existing.InstagramUrl = request.InstagramUrl;
                    existing.TelegramUrl = request.TelegramUrl;
                    existing.YouTubeUrl = request.YouTubeUrl;
                    existing.LinkedInUrl = request.LinkedInUrl;
                    existing.EnamadCode = request.EnamadCode;
                    existing.SamandehiCode = request.SamandehiCode;
                    existing.EtaCode = request.EtaCode;
                    existing.CopyrightText = request.CopyrightText;

                    // Remove existing link groups and recreate
                    _context.FooterLinkGroups.RemoveRange(existing.LinkGroups);
                    existing.LinkGroups = request.LinkGroups.Select((g, i) => new FooterLinkGroup
                    {
                        Title = g.Title,
                        SortOrder = i,
                        Links = g.Links.Select((l, j) => new FooterLink
                        {
                            Title = l.Title,
                            Url = l.Url,
                            Icon = l.Icon,
                            SortOrder = j,
                            IsExternal = l.IsExternal
                        }).ToList()
                    }).ToList();
                }

                await _context.SaveChangesAsync();

                try { await _cacheStore.EvictByTagAsync("footer", CancellationToken.None); }
                catch (Exception ex) { _logger.LogWarning(ex, "Error evicting footer cache"); }

                return Ok(new { message = "Footer settings saved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving footer settings");
                var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Error saving footer settings: {message}");
            }
        }

        private static FooterSettingsDto MapToDto(FooterSettings footer)
        {
            return new FooterSettingsDto
            {
                CompanyName = footer.CompanyName,
                CompanyDescription = footer.CompanyDescription,
                LogoUrl = footer.LogoUrl,
                PhoneNumber1 = footer.PhoneNumber1,
                PhoneNumber2 = footer.PhoneNumber2,
                WhatsAppNumber = footer.WhatsAppNumber,
                Email = footer.Email,
                Address = footer.Address,
                InstagramUrl = footer.InstagramUrl,
                TelegramUrl = footer.TelegramUrl,
                YouTubeUrl = footer.YouTubeUrl,
                LinkedInUrl = footer.LinkedInUrl,
                EnamadCode = footer.EnamadCode,
                SamandehiCode = footer.SamandehiCode,
                EtaCode = footer.EtaCode,
                CopyrightText = footer.CopyrightText,
                LinkGroups = footer.LinkGroups.OrderBy(g => g.SortOrder).Select(g => new FooterLinkGroupDto
                {
                    Id = g.Id,
                    Title = g.Title,
                    SortOrder = g.SortOrder,
                    Links = g.Links.OrderBy(l => l.SortOrder).Select(l => new FooterLinkDto
                    {
                        Id = l.Id,
                        Title = l.Title,
                        Url = l.Url,
                        Icon = l.Icon,
                        SortOrder = l.SortOrder,
                        IsExternal = l.IsExternal
                    }).ToList()
                }).ToList()
            };
        }

        private static FooterSettingsDto GetDefaultFooter()
        {
            return new FooterSettingsDto
            {
                CompanyName = "ارورسرویس",
                CompanyDescription = "مرکز تخصصی تعمیرات و آموزش‌های الکترونیک با بیش از ۱۰ سال تجربه",
                PhoneNumber1 = "09166912537",
                Email = "info@errorservice.ir",
                CopyrightText = "© 2025 ارورسرویس - تمامی حقوق محفوظ است",
                LinkGroups = new List<FooterLinkGroupDto>
                {
                    new FooterLinkGroupDto
                    {
                        Title = "دسترسی سریع",
                        Links = new List<FooterLinkDto>
                        {
                            new FooterLinkDto { Title = "صفحه اصلی", Url = "/" },
                            new FooterLinkDto { Title = "محصولات", Url = "/products" },
                            new FooterLinkDto { Title = "آکادمی", Url = "/academy" },
                            new FooterLinkDto { Title = "تماس با ما", Url = "/contact" }
                        }
                    },
                    new FooterLinkGroupDto
                    {
                        Title = "خدمات",
                        Links = new List<FooterLinkDto>
                        {
                            new FooterLinkDto { Title = "تعمیرات", Url = "/services/repair" },
                            new FooterLinkDto { Title = "مشاوره فنی", Url = "/technical/consultation" },
                            new FooterLinkDto { Title = "کدهای خطا", Url = "/technical/error-codes" }
                        }
                    }
                }
            };
        }
    }
}
