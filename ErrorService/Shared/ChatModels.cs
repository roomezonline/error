using System.Text.Json.Serialization;

namespace ErrorService.Shared;

public enum ChatSessionStatusDto
{
    Waiting = 0,
    Active = 1,
    Closed = 2
}

public enum ChatMessageTypeDto
{
    Text = 0,
    Image = 1,
    Voice = 2,
    File = 3,
    System = 4
}

public enum ChatSenderTypeDto
{
    User = 0,
    Operator = 1,
    System = 2
}

public enum ChatMessageStatusDto
{
    Sending = 0,
    Sent = 1,
    Read = 2
}

public sealed class ChatSessionDto
{
    public int Id { get; set; }
    public string VisitorId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserPhone { get; set; }
    public int? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public ChatSessionStatusDto Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int MessageCount { get; set; }
    public bool HasUnread { get; set; }
    public int? Rating { get; set; }
    public string? RatingComment { get; set; }
}

public sealed class ChatMessageDto
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public ChatSenderTypeDto SenderType { get; set; }
    public string? SenderId { get; set; }
    public string? Content { get; set; }
    public ChatMessageTypeDto MessageType { get; set; }
    public string? MediaUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? ContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public ChatMessageStatusDto Status { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? EditedAt { get; set; }
    public int? ReplyToId { get; set; }
    public string? ReplyToText { get; set; }
}

public sealed class StartChatRequest
{
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
}

public sealed class SendMessageRequest
{
    public int SessionId { get; set; }
    public string? Content { get; set; }
    public ChatMessageTypeDto MessageType { get; set; } = ChatMessageTypeDto.Text;
    public string? MediaUrl { get; set; }
}

public sealed class StartChatResult
{
    public int SessionId { get; set; }
    public string? Message { get; set; }
    public bool Success { get; set; }
}

public sealed class BaleWebhookPayload
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; set; }
    [JsonPropertyName("message")]
    public BaleMessage? Message { get; set; }
}

public sealed class BaleMessage
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; set; }
    [JsonPropertyName("from")]
    public BaleUser? From { get; set; }
    [JsonPropertyName("chat")]
    public BaleChat? Chat { get; set; }
    [JsonPropertyName("text")]
    public string? Text { get; set; }
    [JsonPropertyName("caption")]
    public string? Caption { get; set; }
    [JsonPropertyName("photo")]
    public List<BalePhotoSize>? Photo { get; set; }
    [JsonPropertyName("voice")]
    public BaleVoice? Voice { get; set; }
    [JsonPropertyName("reply_to_message")]
    public BaleMessage? ReplyToMessage { get; set; }
}

public sealed class BaleUser
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }
    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }
    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public sealed class BaleChat
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public sealed class BalePhotoSize
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
    [JsonPropertyName("width")]
    public int Width { get; set; }
    [JsonPropertyName("height")]
    public int Height { get; set; }
    [JsonPropertyName("file_size")]
    public int FileSize { get; set; }
}

public sealed class BaleVoice
{
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
    [JsonPropertyName("file_size")]
    public int FileSize { get; set; }
}

public sealed class OperatorStatusDto
{
    public bool IsAvailable { get; set; }
    public int ActiveSessions { get; set; }
}

public sealed class ChatPresenceDto
{
    public int OnlineAdmins { get; set; }
    public List<string> ActiveChannels { get; set; } = new();
}

public sealed class CannedResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsShared { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ChatFaqEntryDto
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string? Keywords { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public sealed class ChatOperatorDto
{
    public int Id { get; set; }
    public string? FullName { get; set; }
    public bool IsOnline { get; set; }
}

public sealed class ChatSettingsDto
{
    public bool EnableBaleChat { get; set; } = true;
    public string? BaleToken { get; set; }
    public string? BaleGroupId { get; set; }
    public bool EnableTelegramChat { get; set; }
    public string? TelegramToken { get; set; }
    public string? TelegramGroupId { get; set; }
    public bool EnableEitaaChat { get; set; }
    public string? EitaaToken { get; set; }
    public string? EitaaGroupId { get; set; }
    public string? WebhookSecret { get; set; }
    public string? WebhookUrl { get; set; }
    public string? WelcomeMessage { get; set; }
    public bool EnableAutoMessage { get; set; }
    public int AutoMessageSeconds { get; set; } = 60;
    public bool PhoneRequired { get; set; }
    public bool EnableAiAssistant { get; set; }
    public string? AiProvider { get; set; }
    public string? AiApiUrl { get; set; }
    public string? AiModel { get; set; }
    public string? AiSystemPrompt { get; set; }
}
