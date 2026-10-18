using System.Collections.Concurrent;
using ErrorService.Server.Data;
using ErrorService.Server.Hubs;
using ErrorService.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services.ChatAi;

public sealed class ChatAiResponder
{
    private static readonly ConcurrentDictionary<int, byte> BlockedSessions = new();
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();

    // جلوگیری از اسپم پیام‌های اطلاع‌رسانی: هر نوع اطلاع حداکثر هر ۱۰ دقیقه یک‌بار در هر مکالمه.
    private static readonly ConcurrentDictionary<(int SessionId, string Kind), DateTime> NoticeSentAt = new();
    private static readonly TimeSpan NoticeThrottle = TimeSpan.FromMinutes(10);

    private readonly ErrorServiceDbContext _db;
    private readonly ChatAiCoordinator _ai;
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<ChatAiResponder> _logger;

    public ChatAiResponder(ErrorServiceDbContext db, ChatAiCoordinator ai, IHubContext<ChatHub> hub, ILogger<ChatAiResponder> logger)
    {
        _db = db;
        _ai = ai;
        _hub = hub;
        _logger = logger;
    }

    public void BlockSession(int sessionId) => BlockedSessions[sessionId] = 1;

    public bool IsBlocked(int sessionId) => BlockedSessions.ContainsKey(sessionId);

    private async Task SetAiTypingAsync(int sessionId, bool isTyping)
    {
        try
        {
            await _hub.Clients.Group($"session_{sessionId}").SendAsync("AiTyping", isTyping);
            await _hub.Clients.Group("admins").SendAsync("AiTyping", sessionId, isTyping);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChatAiResponder: failed to send AiTyping for session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// یک پیام سیستمی دوستانه برای کاربر می‌فرستد (با throttle تا اسپم نشود).
    /// </summary>
    private async Task TrySendNoticeAsync(int sessionId, string kind, string message)
    {
        try
        {
            var key = (sessionId, kind);
            var now = DateTime.UtcNow;
            if (NoticeSentAt.TryGetValue(key, out var last) && now - last < NoticeThrottle) return;
            NoticeSentAt[key] = now;

            var msg = new ChatMessage
            {
                SessionId = sessionId,
                SenderType = ChatSenderType.System,
                SenderId = "system",
                Content = message,
                MessageType = ChatMessageType.System,
                CreatedAt = DateTime.UtcNow
            };
            _db.ChatMessages.Add(msg);
            await _db.SaveChangesAsync();

            var dto = ChatHub.MapMessageDto(msg);
            await _hub.Clients.Group($"session_{sessionId}").SendAsync("NewMessage", dto);
            await _hub.Clients.Group("admins").SendAsync("NewMessage", dto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChatAiResponder: failed to send notice ({Kind}) for session {SessionId}", kind, sessionId);
        }
    }

    public async Task TryReplyAsync(int sessionId, string userMessage)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userMessage)) return;
            if (BlockedSessions.ContainsKey(sessionId)) return;

            var settings = await _db.SiteSettings.FirstOrDefaultAsync();
            if (settings?.ChatEnableAiAssistant != true) return;

            var maxChars = settings.ChatAiMaxQuestionChars > 0 ? settings.ChatAiMaxQuestionChars : 300;
            if (userMessage.Length > maxChars)
            {
                await TrySendNoticeAsync(sessionId, "too_long",
                    $"⚠️ سوال شما خیلی طولانی است (حداکثر {maxChars} کاراکتر). لطفاً آن را کوتاه‌تر کنید یا با اپراتور انسانی گفتگو نمایید.");
                return;
            }

            // کلمات ممنوعه: اگر سوال شامل یکی از آن‌ها باشد، دستیار جواب نمی‌دهد و اپراتور وارد می‌شود.
            if (!string.IsNullOrWhiteSpace(settings.ChatAiBlockKeywords))
            {
                var blocked = settings.ChatAiBlockKeywords
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var kw in blocked)
                {
                    if (kw.Length > 0 && userMessage.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    {
                        await TrySendNoticeAsync(sessionId, "blocked_topic",
                            "این موضوع نیاز به بررسی کارشناسی دارد؛ اپراتور انسانی به‌زودی پاسخ می‌دهد. 🙏");
                        return;
                    }
                }
            }

            var session = await _db.ChatSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null) return;

            // سقف پاسخ‌های دستیار در هر مکالمه
            var sessionLimit = settings.ChatAiSessionLimit > 0 ? settings.ChatAiSessionLimit : 12;
            var aiInSession = await _db.ChatMessages.AsNoTracking()
                .CountAsync(m => m.SessionId == sessionId
                    && m.SenderType == ChatSenderType.Operator
                    && m.SenderId == "پاسخ خودکار");
            if (aiInSession >= sessionLimit)
            {
                await TrySendNoticeAsync(sessionId, "session_limit",
                    $"✅ سقف پاسخ‌های دستیار هوشمند در این گفتگو ({sessionLimit} پاسخ) تکمیل شد. برای ادامه، اپراتور انسانی پاسخگوی شماست.");
                return;
            }

            // Guest vs member gating + daily limit (Tehran day, UTC+3:30 = 210 minutes).
            var tehranTodayUtc = DateTime.UtcNow.AddMinutes(210).Date.AddMinutes(-210);

            if (session.UserId == null)
            {
                if (settings.ChatAiGuestEnabled != true)
                {
                    await TrySendNoticeAsync(sessionId, "guest_locked",
                        "🤖 دستیار هوشمند فعلاً فقط برای اعضای سایت فعال است. با عضویت رایگان، گفتگو با دستیار هوشمند برای شما هم باز می‌شود.\n👉 عضویت: /auth/register");
                    return;
                }
            }
            else
            {
                var member = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == session.UserId.Value);
                if (member == null) return;
                if (member.ChatAiDisabled)
                {
                    await TrySendNoticeAsync(sessionId, "member_disabled",
                        "⛔ استفاده شما از دستیار هوشمند موقتاً غیرفعال شده است. برای اطلاعات بیشتر با پشتیبانی تماس بگیرید.");
                    return;
                }

                var limit = member.ChatAiDailyLimitOverride ?? settings.ChatAiDailyLimit;
                if (limit <= 0)
                {
                    await TrySendNoticeAsync(sessionId, "member_no_quota",
                        "🤖 دستیار هوشمند در حال حاضر برای حساب شما فعال نیست. می‌توانید با اپراتور انسانی گفتگو کنید.");
                    return;
                }

                var used = await _db.ChatMessages.AsNoTracking()
                    .Where(m => m.MessageType == ChatMessageType.Text
                        && m.SenderType == ChatSenderType.Operator
                        && m.SenderId == "پاسخ خودکار"
                        && m.CreatedAt >= tehranTodayUtc
                        && m.Session.UserId == session.UserId.Value)
                    .CountAsync();
                if (used >= limit)
                {
                    await TrySendNoticeAsync(sessionId, "daily_limit",
                        $"⏳ سقف روزانه استفاده از دستیار هوشمند شما ({limit} پاسخ در روز) تکمیل شد. فردا دوباره در خدمت شما هستیم یا همین حالا با اپراتور انسانی گفتگو کنید.");
                    return;
                }
            }

            var sem = Locks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
            if (!await sem.WaitAsync(0)) return;
            try
            {
                var history = await _db.ChatMessages
                    .Where(m => m.SessionId == sessionId
                        && m.MessageType == ChatMessageType.Text
                        && (m.SenderType == ChatSenderType.User || m.SenderType == ChatSenderType.Operator))
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(12)
                    .Select(m => new { m.SenderType, m.Content })
                    .ToListAsync();

                var ordered = history
                    .AsEnumerable()
                    .Reverse()
                    .Select(m => new ChatAiTurn(
                        m.SenderType == ChatSenderType.User ? "user" : "assistant",
                        m.Content ?? ""))
                    .ToList();

                // Let the visitor (and admins) know the assistant is working on a reply.
                await SetAiTypingAsync(sessionId, true);
                string? reply;
                try
                {
                    reply = await _ai.GetReplyAsync(userMessage, ordered);
                }
                finally
                {
                    await SetAiTypingAsync(sessionId, false);
                }

                if (string.IsNullOrWhiteSpace(reply))
                {
                    // قبلاً بی‌صدا return می‌شد و کاربر با «چت مرده» مواجه می‌شد.
                    await TrySendNoticeAsync(sessionId, "no_reply",
                        "⚠️ دستیار هوشمند فعلاً پاسخی تولید نکرد. لطفاً چند لحظه بعد دوباره تلاش کنید یا از اپراتور انسانی کمک بگیرید.");
                    return;
                }

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

                var dto = ChatHub.MapMessageDto(aiMsg);
                await _hub.Clients.Group($"session_{sessionId}").SendAsync("NewMessage", dto);
                await _hub.Clients.Group("admins").SendAsync("NewMessage", dto);
            }
            finally
            {
                sem.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChatAiResponder: reply failed for session {SessionId}", sessionId);
            try
            {
                await TrySendNoticeAsync(sessionId, "error",
                    "⚠️ خطایی در دستیار هوشمند رخ داد. لطفاً دوباره تلاش کنید یا از اپراتور انسانی راهنمایی بگیرید.");
            }
            catch { }
        }
    }
}
