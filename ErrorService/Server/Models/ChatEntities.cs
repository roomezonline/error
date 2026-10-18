using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErrorService.Server.Models;

public enum ChatSessionStatus
{
    Waiting = 0,
    Active = 1,
    Closed = 2
}

public enum ChatMessageType
{
    Text = 0,
    Image = 1,
    Voice = 2,
    File = 3,
    System = 4
}

public enum ChatSenderType
{
    User = 0,
    Operator = 1,
    System = 2
}

public enum ChatMessageStatus
{
    Sending = 0,
    Sent = 1,
    Read = 2
}

public sealed class ChatSession
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    public string VisitorId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? UserName { get; set; }

    [MaxLength(200)]
    public string? UserEmail { get; set; }

    [MaxLength(30)]
    public string? UserPhone { get; set; }

    public int? UserId { get; set; }

    public int? OperatorId { get; set; }

    public ChatSessionStatus Status { get; set; } = ChatSessionStatus.Waiting;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ClosedAt { get; set; }

    public long? BaleChatId { get; set; }

    public int? LastBaleMessageId { get; set; }

    public int? Rating { get; set; }

    public string? RatingComment { get; set; }

    public AppUser? Operator { get; set; }

    public List<ChatMessage> Messages { get; set; } = new();
}

public sealed class ChatMessage
{
    [Key]
    public int Id { get; set; }

    public int SessionId { get; set; }

    public ChatSenderType SenderType { get; set; }

    [MaxLength(100)]
    public string? SenderId { get; set; }

    [MaxLength(4000)]
    public string? Content { get; set; }

    public ChatMessageType MessageType { get; set; } = ChatMessageType.Text;

    [MaxLength(1000)]
    public string? MediaUrl { get; set; }

    [MaxLength(500)]
    public string? FileName { get; set; }

    public long? FileSize { get; set; }

    [MaxLength(200)]
    public string? ContentType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; }

    public ChatMessageStatus Status { get; set; } = ChatMessageStatus.Sent;

    public bool IsDeleted { get; set; }

    public DateTime? EditedAt { get; set; }

    public int? ReplyToId { get; set; }

    public ChatSession Session { get; set; } = null!;
}

public sealed class CannedResponse
{
    [Key]
    public int Id { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Category { get; set; }

    public bool IsShared { get; set; } = true;

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ChatFaqEntry
{
    [Key]
    public int Id { get; set; }

    [MaxLength(300)]
    public string Question { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Answer { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Keywords { get; set; }

    public bool IsEnabled { get; set; } = true;
}

public sealed class BannedVisitor
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    public string VisitorId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Reason { get; set; }

    public DateTime BannedAt { get; set; } = DateTime.UtcNow;

    public int? BannedByUserId { get; set; }
}

// ===== زنجیره هوش مصنوعی چندسرویس‌دهنده‌ای =====

public sealed class ChatAiProvider
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>openrouter | openai | custom</summary>
    [MaxLength(30)]
    public string Kind { get; set; } = "custom";

    public bool Enabled { get; set; } = true;

    public int Priority { get; set; }

    public List<ChatAiApiKey> ApiKeys { get; set; } = new();

    public List<ChatAiModel> Models { get; set; } = new();
}

public sealed class ChatAiApiKey
{
    [Key]
    public int Id { get; set; }

    public int ProviderId { get; set; }

    [MaxLength(500)]
    public string KeyValue { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Label { get; set; }

    public bool Enabled { get; set; } = true;

    public int Priority { get; set; }

    // وضعیت زنده (برای سویچ خودکار و نمایش در پنل ادمین)
    public int ConsecutiveFailures { get; set; }

    public DateTime? CooldownUntil { get; set; }

    [MaxLength(500)]
    public string? LastError { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public long SuccessCount { get; set; }

    public long FailureCount { get; set; }

    public ChatAiProvider Provider { get; set; } = null!;
}

public sealed class ChatAiModel
{
    [Key]
    public int Id { get; set; }

    public int ProviderId { get; set; }

    [MaxLength(300)]
    public string ModelId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Label { get; set; }

    public bool Enabled { get; set; } = true;

    public int Priority { get; set; }

    // کول‌داون موقت مدل (مثلاً 404 یا سقف روزانه مدل رایگان)
    public int ConsecutiveFailures { get; set; }

    public DateTime? CooldownUntil { get; set; }

    [MaxLength(500)]
    public string? LastError { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public long SuccessCount { get; set; }

    public long FailureCount { get; set; }

    public ChatAiProvider Provider { get; set; } = null!;
}
