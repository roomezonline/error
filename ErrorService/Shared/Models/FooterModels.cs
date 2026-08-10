using System.ComponentModel.DataAnnotations;

namespace ErrorService.Shared.Models
{
    public class FooterSettings
    {
        public int Id { get; set; }
        
        // Company Info
        public string CompanyName { get; set; } = "ارورسرویس";
        public string? CompanyDescription { get; set; } = "مرکز تخصصی تعمیرات و آموزش‌های الکترونیک";
        public string? LogoUrl { get; set; }
        
        // Contact Info
        public string? PhoneNumber1 { get; set; }
        public string? PhoneNumber2 { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        
        // Social Links
        public string? InstagramUrl { get; set; }
        public string? TelegramUrl { get; set; }
        public string? YouTubeUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        
        // Footer Links Sections
        public List<FooterLinkGroup> LinkGroups { get; set; } = new();
        
        // Trust Badges
        public string? EnamadCode { get; set; }
        public string? SamandehiCode { get; set; }
        public string? EtaCode { get; set; }
        
        // Copyright
        public string? CopyrightText { get; set; } = "© 2025 ارورسرویس - تمامی حقوق محفوظ است";
        
        public bool IsActive { get; set; } = true;
    }

    public class FooterLinkGroup
    {
        public int Id { get; set; }
        public int FooterSettingsId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public List<FooterLink> Links { get; set; } = new();
    }

    public class FooterLink
    {
        public int Id { get; set; }
        public int FooterLinkGroupId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsExternal { get; set; } = false;
    }

    // DTOs for API
    public class FooterSettingsDto
    {
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyDescription { get; set; }
        public string? LogoUrl { get; set; }
        public string? PhoneNumber1 { get; set; }
        public string? PhoneNumber2 { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? InstagramUrl { get; set; }
        public string? TelegramUrl { get; set; }
        public string? YouTubeUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public List<FooterLinkGroupDto> LinkGroups { get; set; } = new();
        public string? EnamadCode { get; set; }
        public string? SamandehiCode { get; set; }
        public string? EtaCode { get; set; }
        public string? CopyrightText { get; set; }
    }

    public class FooterLinkGroupDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public List<FooterLinkDto> Links { get; set; } = new();
    }

    public class FooterLinkDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsExternal { get; set; }
    }

    public class SaveFooterSettingsRequest
    {
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyDescription { get; set; }
        public string? LogoUrl { get; set; }
        public string? PhoneNumber1 { get; set; }
        public string? PhoneNumber2 { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? InstagramUrl { get; set; }
        public string? TelegramUrl { get; set; }
        public string? YouTubeUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public List<FooterLinkGroupDto> LinkGroups { get; set; } = new();
        public string? EnamadCode { get; set; }
        public string? SamandehiCode { get; set; }
        public string? EtaCode { get; set; }
        public string? CopyrightText { get; set; }
    }
}
