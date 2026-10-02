using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErrorService.Shared.Models
{
    public class ErrorCode
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "نام برند الزامی است")]
        public string Brand { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "نوع دستگاه الزامی است")]
        public string DeviceType { get; set; } = string.Empty; 
        
        [Required(ErrorMessage = "کد خطا الزامی است")]
        public string Code { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "شرح خطا الزامی است")]
        public string Description { get; set; } = string.Empty;
        
        public string Solution { get; set; } = string.Empty;
        
        public string? TechnicalNotes { get; set; }

        public string? ModelNames { get; set; } // Comma separated model names
        public string? RelatedProductIds { get; set; } // Comma separated product IDs

        public string? ImageUrl { get; set; }

        [MaxLength(100)]
        public string? Category { get; set; }

        public List<ErrorCodeDocument> Documents { get; set; } = new();
    }

    public static class ErrorCodeCategories
    {
        public static readonly string[] All =
        {
            "سنسورها",
            "موتور و پمپ",
            "برد و الکترونیک",
            "آب و نشتی",
            "دما و گرمایش",
            "صفحه نمایش و پنل",
            "منبع تغذیه",
            "اتصالات و سیم‌کشی",
            "نرم‌افزار و فریمور",
            "سایر"
        };
    }

    public enum ErrorCodeCatalogKind
    {
        Brand = 0,
        DeviceType = 1,
        Category = 2
    }

    public class ErrorCodeCatalogItem
    {
        public int Id { get; set; }

        public ErrorCodeCatalogKind Kind { get; set; }

        [Required(ErrorMessage = "نام الزامی است")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [NotMapped]
        public int UsageCount { get; set; }
    }

    public class ErrorCodeCatalogUpsertRequest
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "نام الزامی است")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    public class ErrorCodeDocument
    {
        public int Id { get; set; }

        public int ErrorCodeId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Url { get; set; } = string.Empty;

        public ErrorCodeDocumentType DocType { get; set; } = ErrorCodeDocumentType.Other;

        public int SortOrder { get; set; } = 0;
    }

    public enum ErrorCodeDocumentType
    {
        Other = 0,
        UserManual = 1,
        ServiceManual = 2,
        Datasheet = 3,
        Source = 4
    }

    public class ConsultationTicket
    {
        public int Id { get; set; }

        public int? UserId { get; set; }
        
        [Required(ErrorMessage = "عنوان موضوع الزامی است")]
        public string Title { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "توضیحات مشکل الزامی است")]
        public string Description { get; set; } = string.Empty;
        
        public string? ImageUrl { get; set; }

        [MaxLength(500)]
        public string? FileUrl { get; set; }

        [MaxLength(255)]
        public string? FileName { get; set; }

        [MaxLength(120)]
        public string? FileContentType { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime LastMessageAt { get; set; } = DateTime.Now;

        public bool IsAdminRead { get; set; } = false;

        public bool IsUserRead { get; set; } = true;

        public TicketStatus Status { get; set; } = TicketStatus.Pending;

        [NotMapped]
        public string? UserEmail { get; set; }

        [NotMapped]
        public string? UserFullName { get; set; }

        [NotMapped]
        public string? UserPhoneNumber { get; set; }
        
        public List<TicketReply> Replies { get; set; } = new();
    }

    public enum TicketStatus
    {
        Pending,
        InProgress,
        Resolved,
        Closed
    }

    public class TicketReply
    {
        public int Id { get; set; }
        public int ConsultationTicketId { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime RepliedAt { get; set; } = DateTime.Now;
        public bool IsAdmin { get; set; }

        [MaxLength(500)]
        public string? AttachmentUrl { get; set; }

        [MaxLength(255)]
        public string? AttachmentName { get; set; }

        [MaxLength(120)]
        public string? AttachmentContentType { get; set; }
    }
}
