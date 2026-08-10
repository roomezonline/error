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

        public List<ErrorCodeDocument> Documents { get; set; } = new();
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

    public class ErrorCodeImportModel
    {
        public string Brand { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Solution { get; set; } = string.Empty;
        public string? TechnicalNotes { get; set; }
        public string? ModelNames { get; set; }
        public List<ErrorCodeDocumentImportModel> Documents { get; set; } = new();
    }

    public class ErrorCodeDocumentImportModel
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public ErrorCodeDocumentType DocType { get; set; } = ErrorCodeDocumentType.Other;
    }

    public class ErrorCodeImportResult
    {
        public int TotalProcessed { get; set; }
        public int InsertedCount { get; set; }
        public int UpdatedCount { get; set; }
        public List<string> Errors { get; set; } = new();
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
