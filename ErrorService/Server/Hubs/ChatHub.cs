using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Server.Services.ChatAi;
using ErrorService.Server.Services.Messenger;
using ErrorService.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace ErrorService.Server.Hubs;

public sealed class ChatHub : Hub
{
    private readonly ErrorServiceDbContext _db;
    private readonly MessengerRouter _router;
    private readonly ChatAiCoordinator _ai;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChatHub> _logger;

    private static readonly ConcurrentDictionary<string, int> OnlineAdmins = new();
    private static readonly ConcurrentDictionary<string, string> ConnectionVisitors = new();
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> TypingTimers = new();
    private static readonly ConcurrentDictionary<int, byte> AiBlockedSessions = new();
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> AiLocks = new();

    public ChatHub(ErrorServiceDbContext db, MessengerRouter router, ChatAiCoordinator ai,
        IServiceScopeFactory scopeFactory, ILogger<ChatHub> logger)
    {
        _db = db;
        _router = router;
        _ai = ai;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var visitorId = http?.Request.Query["visitorId"].FirstOrDefault() ?? Context.ConnectionId;

        var isAuth = http?.User?.Identity?.IsAuthenticated == true;

        if (isAuth)
        {
            var hasPerm = http.User.HasClaim("perm", "admin.chat.manage")
                          || http.User.IsInRole("super_admin");
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
        await Groups.AddToGroupAsync(Context.ConnectionId, "presence");
        await SendPresenceAsync();
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
        await SendPresenceAsync();
        await base.OnDisconnectedAsync(exception);
    }

    public int GetPresence() => OnlineAdmins.Count;

    public async Task RequestOperator(int sessionId)
    {
        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session == null) return;

        var visitorId = ConnectionVisitors.GetValueOrDefault(Context.ConnectionId) ?? Context.ConnectionId;
        if (session.VisitorId != visitorId && !OnlineAdmins.ContainsKey(Context.ConnectionId)) return;

        AiBlockedSessions[sessionId] = 1;

        var msg = new ChatMessage
        {
            SessionId = sessionId,
            SenderType = ChatSenderType.System,
            SenderId = "system",
            Content = "کاربر درخواست گفتگو با اپراتور انسانی داد.",
            MessageType = ChatMessageType.System,
            CreatedAt = DateTime.UtcNow
        };
        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync();

        await Clients.Group($"session_{sessionId}").SendAsync("NewMessage", MapMessageDto(msg));
        await Clients.Group("admins").SendAsync("OperatorRequested", sessionId);
    }

    private async Task TryRunAiAsync(int sessionId, string userMessage, string userName)
    {
        try
        {
            if (AiBlockedSessions.ContainsKey(sessionId)) return;
            if (OnlineAdmins.Count > 0) return;

            var settings = await _db.SiteSettings.FirstOrDefaultAsync();
            if (settings?.ChatEnableAiAssistant != true) return;

            if (userMessage.Length > 300) return;

            var sem = AiLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
            if (!await sem.WaitAsync(0)) return;
            try
            {
                var reply = await _ai.GetReplyAsync(userMessage);
                if (string.IsNullOrWhiteSpace(reply)) return;

                AiBlockedSessions[sessionId] = 1;

                var aiMsg = new ChatMessage
                {
                    SessionId = sessionId,
                    SenderType = ChatSenderType.Operator,
                    SenderId = "پاسخ خودکار",
                    Content = reply,
                    MessageType = ChatMessageType.Text,
                    CreatedAt = DateTime.UtcNow
                };
                _db.ChatMessages.Add(aiMsg);
                await _db.SaveChangesAsync();

                await Clients.Group($"session_{sessionId}").SendAsync("NewMessage", MapMessageDto(aiMsg));
            }
            finally
            {
                sem.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChatHub: AI reply failed for session {SessionId}", sessionId);
        }
    }

    private async Task SendPresenceAsync()
    {
        await Clients.Group("presence").SendAsync("PresenceChanged", OnlineAdmins.Count);
    }

    public async Task<StartChatResult> StartChat(string userName, string? userEmail, string? userPhone = null)
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

        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings?.ChatPhoneRequired == true && string.IsNullOrWhiteSpace(userPhone))
        {
            return new StartChatResult
            {
                Success = false,
                Message = "وارد کردن شماره تماس الزامی است."
            };
        }

        var session = new ChatSession
        {
            VisitorId = visitorId,
            UserName = userName,
            UserEmail = userEmail,
            UserPhone = string.IsNullOrWhiteSpace(userPhone) ? null : userPhone.Trim(),
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

        // Welcome message (operator-styled)
        if (!string.IsNullOrWhiteSpace(settings?.ChatWelcomeMessage))
        {
            var wMsg = new ChatMessage
            {
                SessionId = session.Id,
                SenderType = ChatSenderType.Operator,
                SenderId = "اپراتور",
                Content = settings.ChatWelcomeMessage,
                MessageType = ChatMessageType.Text,
                CreatedAt = DateTime.UtcNow
            };
            _db.ChatMessages.Add(wMsg);
            await _db.SaveChangesAsync();
            await Clients.Caller.SendAsync("NewMessage", MapMessageDto(wMsg));
        }

        // Auto (trigger) message if operator doesn't reply in time
        if (settings?.ChatEnableAutoMessage == true && settings.ChatAutoMessageSeconds > 0 && !string.IsNullOrWhiteSpace(settings.ChatWelcomeMessage))
        {
            var sessionId = session.Id;
            var delaySeconds = settings.ChatAutoMessageSeconds;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
                    var hub = scope.ServiceProvider.GetRequiredService<IHubContext<ChatHub>>();
                    var replied = await db.ChatMessages.AnyAsync(m =>
                        m.SessionId == sessionId &&
                        m.CreatedAt > DateTime.UtcNow.AddSeconds(-delaySeconds) &&
                        m.SenderType == ChatSenderType.Operator);
                    if (replied) return;
                    var aMsg = new ChatMessage
                    {
                        SessionId = sessionId,
                        SenderType = ChatSenderType.Operator,
                        SenderId = "اپراتور",
                        Content = "یک لحظه، اپراتور به‌زودی پاسخ شما را می‌دهد 🙏",
                        MessageType = ChatMessageType.Text,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.ChatMessages.Add(aMsg);
                    await db.SaveChangesAsync();
                    await hub.Clients.Group($"session_{sessionId}").SendAsync("NewMessage", MapMessageDto(aMsg));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ChatHub: auto message failed for session {SessionId}", sessionId);
                }
            });
        }

        // Notify messenger group(s)
        try
        {
            await _router.SendSystemToAllAsync(session.Id,
                $"مکالمه جدید از {userName}\nبرای پاسخ به این پیام، روی همین پیام ریپلی کنید.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChatHub: messenger notify failed for session {SessionId}", session.Id);
        }

        return new StartChatResult
        {
            SessionId = session.Id,
            Success = true,
            Message = "مکالمه شروع شد."
        };
    }

    public async Task<bool> SendMessage(int sessionId, string? content, ChatMessageTypeDto messageType, string? mediaUrl, int? replyToId = null)
    {
        var isAdmin = OnlineAdmins.ContainsKey(Context.ConnectionId);
        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session == null || session.Status == ChatSessionStatus.Closed) return false;

        if (!isAdmin)
        {
            var sessionVisitorId = ConnectionVisitors.GetValueOrDefault(Context.ConnectionId) ?? Context.ConnectionId;
            if (session.VisitorId != sessionVisitorId) return false;

            if (string.IsNullOrWhiteSpace(content) &&
                (string.IsNullOrWhiteSpace(mediaUrl) || !mediaUrl.StartsWith("/uploads/chat/", StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        if (content?.Length > 4000) return false;

        ChatMessage msg;
        string? operatorName = null;
        if (isAdmin)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var adminUser = userId != null && int.TryParse(userId, out var uid)
                ? await _db.Users.FindAsync(uid)
                : null;
            operatorName = adminUser?.FullName ?? "اپراتور";
            msg = new ChatMessage
            {
                SessionId = sessionId,
                SenderType = ChatSenderType.Operator,
                SenderId = operatorName,
                Content = content,
                MessageType = (ChatMessageType)(int)messageType,
                MediaUrl = mediaUrl,
                ReplyToId = replyToId,
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
                ReplyToId = replyToId,
                CreatedAt = DateTime.UtcNow
            };
        }

        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync();

        var dto = MapMessageDto(msg);
        await Clients.Group($"session_{sessionId}").SendAsync("NewMessage", dto);

        if (!isAdmin)
        {
            var userName = session.UserName ?? "کاربر";
            try
            {
                switch (messageType)
                {
                    case ChatMessageTypeDto.Text:
                        await _router.ForwardUserTextAsync(sessionId, content ?? "", userName);
                        break;
                    case ChatMessageTypeDto.Image:
                        var baseUrl = $"{Context.GetHttpContext()?.Request.Scheme}://{Context.GetHttpContext()?.Request.Host}";
                        await _router.ForwardUserPhotoAsync(sessionId, $"{baseUrl}{mediaUrl}", content, userName);
                        break;
                    case ChatMessageTypeDto.Voice:
                        var voiceBaseUrl = $"{Context.GetHttpContext()?.Request.Scheme}://{Context.GetHttpContext()?.Request.Host}";
                        await _router.ForwardUserVoiceAsync(sessionId, $"{voiceBaseUrl}{mediaUrl}", userName);
                        break;
                }
                _logger.LogInformation("MessengerRouter: user message forwarded for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MessengerRouter: failed to forward message for session {SessionId}", sessionId);
            }

            // Keep admin panel in sync in real-time even when the session is not open
            await Clients.Group("admins").SendAsync("NewMessage", dto);

            await TryRunAiAsync(sessionId, content ?? "", userName);
        }
        else
        {
            try
            {
                switch (messageType)
                {
                    case ChatMessageTypeDto.Text:
                        await _router.ForwardOperatorTextAsync(sessionId, content ?? "", operatorName);
                        break;
                    case ChatMessageTypeDto.Image:
                        var baseUrl = $"{Context.GetHttpContext()?.Request.Scheme}://{Context.GetHttpContext()?.Request.Host}";
                        await _router.ForwardOperatorPhotoAsync(sessionId, $"{baseUrl}{mediaUrl}", content, operatorName);
                        break;
                    case ChatMessageTypeDto.Voice:
                        var voiceBaseUrl = $"{Context.GetHttpContext()?.Request.Scheme}://{Context.GetHttpContext()?.Request.Host}";
                        await _router.ForwardOperatorVoiceAsync(sessionId, $"{voiceBaseUrl}{mediaUrl}", operatorName);
                        break;
                }
                _logger.LogInformation("MessengerRouter: operator reply forwarded for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MessengerRouter: failed to forward operator reply for session {SessionId}", sessionId);
            }
        }

        return true;
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
        var isAdmin = OnlineAdmins.ContainsKey(Context.ConnectionId);
        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session == null) return;

        if (!isAdmin)
        {
            var visitorId = ConnectionVisitors.GetValueOrDefault(Context.ConnectionId) ?? Context.ConnectionId;
            if (session.VisitorId != visitorId) return;
        }

        var isUser = !isAdmin;
        var messages = await _db.ChatMessages
            .Where(m => m.SessionId == sessionId && messageIds.Contains(m.Id))
            .Where(m => isUser
                ? m.SenderType == ChatSenderType.Operator
                : m.SenderType == ChatSenderType.User)
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

        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session == null) return;

        if (!isAdmin)
        {
            var visitorId = ConnectionVisitors.GetValueOrDefault(Context.ConnectionId) ?? Context.ConnectionId;
            if (session.VisitorId != visitorId) return;
        }

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

    public async Task EditMessage(int messageId, string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        var msg = await _db.ChatMessages.FindAsync(messageId);
        if (msg == null || msg.IsDeleted || msg.MessageType != ChatMessageType.Text) return;

        var isAdmin = OnlineAdmins.ContainsKey(Context.ConnectionId);
        if (isAdmin)
        {
            if (msg.SenderType != ChatSenderType.Operator) return;
        }
        else
        {
            var visitorId = ConnectionVisitors.GetValueOrDefault(Context.ConnectionId) ?? Context.ConnectionId;
            if (msg.SenderType != ChatSenderType.User) return;
            var session = await _db.ChatSessions.FindAsync(msg.SessionId);
            if (session == null || session.VisitorId != visitorId) return;
            if (msg.CreatedAt < DateTime.UtcNow.AddMinutes(-10)) return;
        }
        if (content.Length > 4000) return;

        msg.Content = content;
        msg.EditedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await Clients.Group($"session_{msg.SessionId}").SendAsync("MessageEdited", MapMessageDto(msg));
    }

    public async Task DeleteMessage(int messageId)
    {
        var msg = await _db.ChatMessages.FindAsync(messageId);
        if (msg == null || msg.IsDeleted) return;

        var isAdmin = OnlineAdmins.ContainsKey(Context.ConnectionId);
        if (isAdmin)
        {
            if (msg.SenderType != ChatSenderType.Operator) return;
        }
        else
        {
            var visitorId = ConnectionVisitors.GetValueOrDefault(Context.ConnectionId) ?? Context.ConnectionId;
            if (msg.SenderType != ChatSenderType.User) return;
            var session = await _db.ChatSessions.FindAsync(msg.SessionId);
            if (session == null || session.VisitorId != visitorId) return;
            if (msg.CreatedAt < DateTime.UtcNow.AddMinutes(-30)) return;
        }

        msg.IsDeleted = true;
        await _db.SaveChangesAsync();
        await Clients.Group($"session_{msg.SessionId}").SendAsync("MessageDeleted", msg.SessionId, messageId);
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

        // Notify messenger group(s)
        try
        {
            await _router.SendSystemToAllAsync(sessionId, "مکالمه توسط اپراتور بسته شد.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChatHub: messenger close notify failed for session {SessionId}", sessionId);
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
            UserPhone = s.UserPhone,
            OperatorId = s.OperatorId,
            OperatorName = s.Operator?.FullName,
            Status = (ChatSessionStatusDto)(int)s.Status,
            CreatedAt = s.CreatedAt,
            ClosedAt = s.ClosedAt,
            Rating = s.Rating,
            RatingComment = s.RatingComment
        };
    }

    public static ChatMessageDto MapMessageDto(ChatMessage m)
    {
        var senderType = (ChatSenderTypeDto)(int)m.SenderType;
        var senderId = m.SenderId;
        if (m.SenderType == ChatSenderType.Operator && m.SenderId != null && m.SenderId.StartsWith("اپراتور ("))
        {
            senderType = ChatSenderTypeDto.User;
            senderId = "کاربر (" + m.SenderId.Substring("اپراتور (".Length);
        }
        return new ChatMessageDto
        {
            Id = m.Id,
            SessionId = m.SessionId,
            SenderType = senderType,
            SenderId = senderId,
            Content = m.Content,
            MessageType = (ChatMessageTypeDto)(int)m.MessageType,
            MediaUrl = m.MediaUrl,
            FileName = m.FileName,
            FileSize = m.FileSize,
            ContentType = m.ContentType,
            CreatedAt = m.CreatedAt,
            IsRead = m.IsRead,
            Status = (ChatMessageStatusDto)(int)m.Status,
            IsDeleted = m.IsDeleted,
            EditedAt = m.EditedAt,
            ReplyToId = m.ReplyToId
        };
    }
}
