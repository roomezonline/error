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

    public ChatSession Session { get; set; } = null!;
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
