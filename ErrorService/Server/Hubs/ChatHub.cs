using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace ErrorService.Server.Hubs;

public sealed class ChatHub : Hub
{
    private readonly ErrorServiceDbContext _db;
    private readonly BaleBotService _baleBot;
    private readonly ILogger<ChatHub> _logger;

    private static readonly ConcurrentDictionary<string, int> OnlineAdmins = new();
    private static readonly ConcurrentDictionary<string, string> ConnectionVisitors = new();
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> TypingTimers = new();

    public ChatHub(ErrorServiceDbContext db, BaleBotService baleBot, ILogger<ChatHub> logger)
    {
        _db = db;
        _baleBot = baleBot;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var visitorId = http?.Request.Query["visitorId"].FirstOrDefault() ?? Context.ConnectionId;

        var isAuth = http?.User?.Identity?.IsAuthenticated == true;

        if (isAuth)
        {
            var hasPerm = http.User.HasClaim("perm", "admin.chat.manage");
            if (hasPerm)
            {
                var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId != null && int.TryParse(userId, out var uid))
                {
                    OnlineAdmins[Context.ConnectionId] = uid;
                    await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
                }
            }
        }

        ConnectionVisitors[Context.ConnectionId] = visitorId;
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        OnlineAdmins.TryRemove(Context.ConnectionId, out _);
        ConnectionVisitors.TryRemove(Context.ConnectionId, out _);
        foreach (var kv in TypingTimers)
        {
            if (kv.Key.StartsWith($"{Context.ConnectionId}:"))
            {
                kv.Value.Cancel();
                TypingTimers.TryRemove(kv.Key, out _);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<StartChatResult> StartChat(string userName, string? userEmail)
    {
        var visitorId = ConnectionVisitors.GetValueOrDefault(Context.ConnectionId) ?? Context.ConnectionId;

        // Check if visitor is banned
        var isBanned = await _db.BannedVisitors.AnyAsync(b => b.VisitorId == visitorId);
        if (isBanned)
        {
            _logger.LogWarning("StartChat rejected: visitor {VisitorId} is banned", visitorId);
            return new StartChatResult
            {
                Success = false,
                Message = "شما اجازه شروع مکالمه را ندارید."
            };
        }

        var session = new ChatSession
        {
            VisitorId = visitorId,
            UserName = userName,
            UserEmail = userEmail,
            Status = ChatSessionStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync();

        await Groups.AddToGroupAsync(Context.ConnectionId, $"session_{session.Id}");

        // Notify admins
        await Clients.Group("admins").SendAsync("NewSession", MapSessionDto(session));

        // Send system message to session
        var sysMsg = new ChatMessage
        {
            SessionId = session.Id,
            SenderType = ChatSenderType.System,
            SenderId = "system",
            Content = "مکالمه شروع شد. در انتظار پاسخ اپراتور...",
            MessageType = ChatMessageType.System,
            CreatedAt = DateTime.UtcNow
        };
        _db.ChatMessages.Add(sysMsg);
        await _db.SaveChangesAsync();

        await Clients.Caller.SendAsync("NewMessage", MapMessageDto(sysMsg));

        // Notify Bale group
        if (await _baleBot.IsConfiguredAsync())
        {
            await _baleBot.SendSystemMessageToGroup(session.Id,
                $"مکالمه جدید از {userName}\nبرای پاسخ به این پیام، روی همین پیام ریپلی کنید.");
        }

        return new StartChatResult
        {
            SessionId = session.Id,
            Success = true,
            Message = "مکالمه شروع شد."
        };
    }

    public async Task SendMessage(int sessionId, string? content, ChatMessageTypeDto messageType, string? mediaUrl)
    {
        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session == null || session.Status == ChatSessionStatus.Closed) return;

        var isAdmin = OnlineAdmins.ContainsKey(Context.ConnectionId);

        ChatMessage msg;
        if (isAdmin)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var adminUser = userId != null && int.TryParse(userId, out var uid)
                ? await _db.Users.FindAsync(uid)
                : null;
            msg = new ChatMessage
            {
                SessionId = sessionId,
                SenderType = ChatSenderType.Operator,
                SenderId = adminUser?.FullName ?? "اپراتور",
                Content = content,
                MessageType = (ChatMessageType)(int)messageType,
                MediaUrl = mediaUrl,
                CreatedAt = DateTime.UtcNow
            };
        }
        else
        {
            var visitorId = ConnectionVisitors.GetValueOrDefault(Context.ConnectionId);
            msg = new ChatMessage
            {
                SessionId = sessionId,
                SenderType = ChatSenderType.User,
                SenderId = visitorId ?? "unknown",
                Content = content,
                MessageType = (ChatMessageType)(int)messageType,
                MediaUrl = mediaUrl,
                CreatedAt = DateTime.UtcNow
            };
        }

        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync();

        var dto = MapMessageDto(msg);
        await Clients.Group($"session_{sessionId}").SendAsync("NewMessage", dto);

        if (!isAdmin)
        {
            try
            {
                if (await _baleBot.IsConfiguredAsync())
                {
                    var userName = session.UserName ?? "کاربر";
                    switch (messageType)
                    {
                        case ChatMessageTypeDto.Text:
                            await _baleBot.SendTextToGroup(sessionId, content ?? "", userName);
                            break;
                        case ChatMessageTypeDto.Image:
                            var baseUrl = $"{Context.GetHttpContext()?.Request.Scheme}://{Context.GetHttpContext()?.Request.Host}";
                            await _baleBot.SendPhotoToGroup(sessionId, $"{baseUrl}{mediaUrl}", content, userName);
                            break;
                        case ChatMessageTypeDto.Voice:
                            var voiceBaseUrl = $"{Context.GetHttpContext()?.Request.Scheme}://{Context.GetHttpContext()?.Request.Host}";
                            await _baleBot.SendVoiceToGroup(sessionId, $"{voiceBaseUrl}{mediaUrl}", userName);
                            break;
                    }
                    _logger.LogInformation("BaleBot: message forwarded for session {SessionId}", sessionId);
                }
                else
                {
                    var sysMsg = new ChatMessage
                    {
                        SessionId = sessionId,
                        SenderType = ChatSenderType.System,
                        SenderId = "system",
                        Content = "پیام ذخیره شد اما سرویس بله پیکربندی نشده است.",
                        MessageType = ChatMessageType.System,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.ChatMessages.Add(sysMsg);
                    await _db.SaveChangesAsync();
                    await Clients.Group($"session_{sessionId}").SendAsync("NewMessage", MapMessageDto(sysMsg));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BaleBot: failed to forward message for session {SessionId}", sessionId);
            }
        }
    }

    public async Task JoinSession(int sessionId)
    {
        var isAdmin = OnlineAdmins.ContainsKey(Context.ConnectionId);
        if (!isAdmin && !ConnectionVisitors.ContainsKey(Context.ConnectionId)) return;

        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session == null) return;

        // Check ownership for non-admins
        if (!isAdmin)
        {
            var visitorId = ConnectionVisitors.GetValueOrDefault(Context.ConnectionId);
            if (session.VisitorId != visitorId) return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"session_{sessionId}");
    }

    public async Task MarkAsRead(int sessionId, List<int> messageIds)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.SessionId == sessionId && messageIds.Contains(m.Id))
            .ToListAsync();
        foreach (var m in messages)
        {
            m.IsRead = true;
            m.Status = ChatMessageStatus.Read;
        }
        await _db.SaveChangesAsync();

        await Clients.Group($"session_{sessionId}").SendAsync("MessagesRead", sessionId, messageIds);
    }

    public async Task Typing(int sessionId, bool isTyping)
    {
        var isAdmin = OnlineAdmins.ContainsKey(Context.ConnectionId);

        if (isTyping)
        {
            var key = $"{Context.ConnectionId}:{sessionId}";
            if (TypingTimers.TryGetValue(key, out var cts))
                cts.Cancel();
            var newCts = new CancellationTokenSource();
            TypingTimers[key] = newCts;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(5000, newCts.Token);
                    if (isAdmin)
                        await Clients.Group($"session_{sessionId}").SendAsync("OperatorTyping", false);
                    else
                        await Clients.Group("admins").SendAsync("UserTyping", sessionId, false);
                    TypingTimers.TryRemove(key, out _);
                }
                catch (TaskCanceledException) { }
            });
        }

        if (isAdmin)
        {
            await Clients.Group($"session_{sessionId}").SendAsync("OperatorTyping", isTyping);
        }
        else
        {
            await Clients.Group("admins").SendAsync("UserTyping", sessionId, isTyping);
        }
    }

    public async Task CloseSession(int sessionId)
    {
        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session == null) return;

        var isAdmin = OnlineAdmins.ContainsKey(Context.ConnectionId);
        if (!isAdmin)
        {
            var visitorId = ConnectionVisitors.GetValueOrDefault(Context.ConnectionId);
            if (session.VisitorId != visitorId) return;
        }

        if (session.Status == ChatSessionStatus.Closed) return;

        session.Status = ChatSessionStatus.Closed;
        session.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Notify Bale
        if (await _baleBot.IsConfiguredAsync())
        {
            await _baleBot.SendSystemMessageToGroup(sessionId, "مکالمه توسط اپراتور بسته شد.");
        }

        await Clients.Group($"session_{sessionId}").SendAsync("SessionClosed", sessionId);
    }

    private static ChatSessionDto MapSessionDto(ChatSession s)
    {
        return new ChatSessionDto
        {
            Id = s.Id,
            VisitorId = s.VisitorId,
            UserName = s.UserName,
            UserEmail = s.UserEmail,
            OperatorId = s.OperatorId,
            OperatorName = s.Operator?.FullName,
            Status = (ChatSessionStatusDto)(int)s.Status,
            CreatedAt = s.CreatedAt,
            ClosedAt = s.ClosedAt,
            Rating = s.Rating,
            RatingComment = s.RatingComment
        };
    }

    private static ChatMessageDto MapMessageDto(ChatMessage m)
    {
        return new ChatMessageDto
        {
            Id = m.Id,
            SessionId = m.SessionId,
            SenderType = (ChatSenderTypeDto)(int)m.SenderType,
            SenderId = m.SenderId,
            Content = m.Content,
            MessageType = (ChatMessageTypeDto)(int)m.MessageType,
            MediaUrl = m.MediaUrl,
            FileName = m.FileName,
            FileSize = m.FileSize,
            ContentType = m.ContentType,
            CreatedAt = m.CreatedAt,
            IsRead = m.IsRead,
            Status = (ChatMessageStatusDto)(int)m.Status
        };
    }
}
