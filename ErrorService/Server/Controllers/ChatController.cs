using ErrorService.Server.Data;
using ErrorService.Server.Hubs;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Server.Services.Messenger;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BaleBotService _baleBot;
    private readonly IConfiguration _config;
    private readonly IHubContext<ChatHub> _chatHub;
    private readonly MessengerRouter _router;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ErrorServiceDbContext db, IHttpClientFactory httpClientFactory, BaleBotService baleBot,
        IConfiguration config, IHubContext<ChatHub> chatHub, MessengerRouter router, ILogger<ChatController> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _baleBot = baleBot;
        _config = config;
        _chatHub = chatHub;
        _router = router;
        _logger = logger;
    }

    [HttpGet("settings")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult> GetSettings()
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        var token = settings?.BaleBotToken ?? "";
        var groupId = settings?.BaleBotGroupId ?? "";
        var webhookSecret = _config["BaleBot:WebhookSecret"] ?? "";
        var webhookUrl = $"{Request.Scheme}://{Request.Host}/api/chat/bale-webhook";
        if (!string.IsNullOrEmpty(webhookSecret))
            webhookUrl += $"?secret={webhookSecret}";
        return Ok(new
        {
            baleConfigured = !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(groupId),
            baleToken = token,
            baleGroupId = groupId,
            webhookSecret,
            webhookUrl
        });
    }

    [HttpPost("settings")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> SaveSettings([FromBody] BaleSettingsRequest req)
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SiteSettings();
            _db.SiteSettings.Add(settings);
        }

        if (!string.IsNullOrEmpty(req.BaleToken))
            settings.BaleBotToken = req.BaleToken.Trim();
        if (!string.IsNullOrEmpty(req.BaleGroupId))
            settings.BaleBotGroupId = req.BaleGroupId.Trim();
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "تنظیمات با موفقیت ذخیره شد و بلافاصله اعمال گردید." });
    }

    [HttpPost("test-bale")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> TestBale([FromBody] BaleSettingsRequest req)
    {
        if (string.IsNullOrEmpty(req.BaleToken) || string.IsNullOrEmpty(req.BaleGroupId))
            return Ok(new { success = false, message = "توکن و شناسه گروه را وارد کنید." });

        try
        {
            var http = _httpClientFactory.CreateClient();
            var baseUrl = $"https://tapi.bale.ai/bot{req.BaleToken.Trim()}";

            // Step 1: check token validity
            var getMe = await http.GetAsync($"{baseUrl}/getMe");
            if (!getMe.IsSuccessStatusCode)
                return Ok(new { success = false, message = "اتصال برقرار نشد. توکن را بررسی کنید." });

            // Step 2: actually test sending a message to the group
            var testPayload = System.Text.Json.JsonSerializer.Serialize(new
            {
                chat_id = req.BaleGroupId.Trim(),
                text = "🔧 تست اتصال از سایت — در صورت دریافت این پیام، تنظیمات بله صحیح است."
            });
            var content = new StringContent(testPayload, Encoding.UTF8, "application/json");
            var sendResp = await http.PostAsync($"{baseUrl}/sendMessage", content);
            var body = await sendResp.Content.ReadAsStringAsync();

            if (sendResp.IsSuccessStatusCode)
                return Ok(new { success = true, message = "✅ اتصال کامل است. پیام تست به گروه ارسال شد." });
            else
                return Ok(new { success = false, message = $"⚠️ توکن صحیح است اما گروه یافت نشد: {body}" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"خطا: {ex.Message}" });
        }
    }

    [HttpPost("set-webhook")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> SetWebhook()
    {
        var webhookUrl = $"{Request.Scheme}://{Request.Host}";
        var secret = _config["BaleBot:WebhookSecret"] ?? "";
        var results = new List<string>();
        var allOk = true;

        foreach (var ch in await _router.GetActiveChannelsAsync())
        {
            try
            {
                var (success, message) = await ch.SetWebhookAsync(webhookUrl, secret);
                results.Add(message);
                if (!success) allOk = false;
            }
            catch (Exception ex)
            {
                allOk = false;
                results.Add($"⚠️ خطا در ثبت وب‌هوک {ch.DisplayName}: {ex.Message}");
            }
        }

        // حتی اگر کانالی فعال نبود، بله را جداگانه بررسی کن (سازگاری با قبل)
        if (results.Count == 0)
        {
            var (success, message) = await _baleBot.SetWebhookAsync(webhookUrl, secret);
            allOk = success;
            results.Add(message);
        }

        return Ok(new { success = allOk, message = string.Join("\n", results) });
    }

    [HttpGet("webhook-status")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> GetWebhookStatus()
    {
        var (hasWebhook, registeredUrl) = await _baleBot.GetWebhookInfoAsync();
        return Ok(new { hasWebhook, registeredUrl });
    }

    [HttpGet("active")]
    public async Task<ActionResult<ChatSessionDto?>> GetActiveSession([FromQuery] string visitorId)
    {
        if (string.IsNullOrEmpty(visitorId)) return Ok(null);

        var session = await _db.ChatSessions
            .Include(s => s.Operator)
            .Where(s => s.VisitorId == visitorId && s.Status == ChatSessionStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (session == null) return Ok(null);

        return Ok(new ChatSessionDto
        {
            Id = session.Id,
            VisitorId = session.VisitorId,
            UserName = session.UserName,
            UserEmail = session.UserEmail,
            UserPhone = session.UserPhone,
            OperatorId = session.OperatorId,
            OperatorName = session.Operator?.FullName,
            Status = (ChatSessionStatusDto)(int)session.Status,
            CreatedAt = session.CreatedAt,
            ClosedAt = session.ClosedAt
        });
    }

    [HttpGet("sessions")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult<object>> GetSessions([FromQuery] string? status, [FromQuery] int skip = 0, [FromQuery] int take = 20)
    {
        var query = _db.ChatSessions.AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ChatSessionStatus>(status, true, out var statusEnum))
        {
            query = query.Where(s => s.Status == statusEnum);
        }

        var total = await query.CountAsync();

        var dtos = await query
            .OrderByDescending(s => s.Status == ChatSessionStatus.Active)
            .ThenByDescending(s => s.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(s => new ChatSessionDto
            {
                Id = s.Id,
                VisitorId = s.VisitorId,
                UserName = s.UserName,
                UserEmail = s.UserEmail,
                UserPhone = s.UserPhone,
                OperatorId = s.OperatorId,
                OperatorName = s.Operator != null ? s.Operator.FullName : null,
                Status = (ChatSessionStatusDto)(int)s.Status,
                CreatedAt = s.CreatedAt,
                ClosedAt = s.ClosedAt,
                MessageCount = s.Messages.Count,
                HasUnread = s.Messages.Any(m => !m.IsRead && m.SenderType == ChatSenderType.User),
                Rating = s.Rating,
                RatingComment = s.RatingComment
            })
            .ToListAsync();

        return Ok(new { items = dtos, total });
    }

    [HttpPost("send")]
    public async Task<ActionResult<ChatMessageDto>> SendMessage([FromBody] SendChatMessageRequest req)
    {
        var isAdmin = User.Identity?.IsAuthenticated == true &&
                      (User.HasClaim("perm", "admin.chat.manage") || User.IsInRole("super_admin"));

        var session = await _db.ChatSessions.FindAsync(req.SessionId);
        if (session == null || session.Status == ChatSessionStatus.Closed)
            return NotFound(new { message = "مکالمه یافت نشد یا بسته شده است." });

        if (!isAdmin)
        {
            if (string.IsNullOrWhiteSpace(req.VisitorId) || session.VisitorId != req.VisitorId)
                return Unauthorized(new { message = "دسترسی غیرمجاز است." });

            if (string.IsNullOrWhiteSpace(req.Content) &&
                (string.IsNullOrWhiteSpace(req.MediaUrl) || !req.MediaUrl.StartsWith("/uploads/chat/", StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new { message = "پیام خالی است." });
        }

        if (req.Content?.Length > 4000)
            return BadRequest(new { message = "پیام بیش از حد طولانی است." });

        ChatMessage msg;
        if (isAdmin)
        {
            var adminUser = User.FindFirst(ClaimTypes.NameIdentifier) != null && int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value, out var uid)
                ? await _db.Users.FindAsync(uid)
                : null;
            msg = new ChatMessage
            {
                SessionId = session.Id,
                SenderType = ChatSenderType.Operator,
                SenderId = adminUser?.FullName ?? "اپراتور",
                Content = req.Content,
                MessageType = (ChatMessageType)(int)req.MessageType,
                MediaUrl = req.MediaUrl,
                ReplyToId = req.ReplyToId,
                CreatedAt = DateTime.UtcNow
            };
        }
        else
        {
            msg = new ChatMessage
            {
                SessionId = session.Id,
                SenderType = ChatSenderType.User,
                SenderId = req.VisitorId,
                Content = req.Content,
                MessageType = (ChatMessageType)(int)req.MessageType,
                MediaUrl = req.MediaUrl,
                ReplyToId = req.ReplyToId,
                CreatedAt = DateTime.UtcNow
            };
        }

        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync();

        var dto = Hubs.ChatHub.MapMessageDto(msg);
        await _chatHub.Clients.Group($"session_{session.Id}").SendAsync("NewMessage", dto);
        await _chatHub.Clients.Group("admins").SendAsync("NewMessage", dto);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        if (!isAdmin)
        {
            var userName = session.UserName ?? "کاربر";
            try
            {
                switch (req.MessageType)
                {
                    case ChatMessageTypeDto.Text:
                        await _router.ForwardUserTextAsync(session.Id, req.Content ?? "", userName);
                        break;
                    case ChatMessageTypeDto.Image:
                        await _router.ForwardUserPhotoAsync(session.Id, $"{baseUrl}{req.MediaUrl}", req.Content, userName);
                        break;
                    case ChatMessageTypeDto.Voice:
                        await _router.ForwardUserVoiceAsync(session.Id, $"{baseUrl}{req.MediaUrl}", userName);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatController: messenger forward failed for session {SessionId}", session.Id);
            }
        }
        else
        {
            var operatorName = msg.SenderId ?? "اپراتور";
            try
            {
                switch (req.MessageType)
                {
                    case ChatMessageTypeDto.Text:
                        await _router.ForwardOperatorTextAsync(session.Id, req.Content ?? "", operatorName);
                        break;
                    case ChatMessageTypeDto.Image:
                        await _router.ForwardOperatorPhotoAsync(session.Id, $"{baseUrl}{req.MediaUrl}", req.Content, operatorName);
                        break;
                    case ChatMessageTypeDto.Voice:
                        await _router.ForwardOperatorVoiceAsync(session.Id, $"{baseUrl}{req.MediaUrl}", operatorName);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatController: operator forward failed for session {SessionId}", session.Id);
            }
        }

        return Ok(dto);
    }

    [HttpGet("sessions/{id}/messages")]
    public async Task<ActionResult<object>> GetMessages(int id, [FromQuery] string? visitorId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var session = await _db.ChatSessions.FindAsync(id);
        if (session == null) return NotFound();

        var isAdmin = User.Identity?.IsAuthenticated == true &&
                      User.HasClaim("perm", "admin.chat.manage");

        if (!isAdmin && (string.IsNullOrEmpty(visitorId) || session.VisitorId != visitorId))
            return Unauthorized();

        var query = _db.ChatMessages.Where(m => m.SessionId == id);
        var total = await query.CountAsync();

        var messages = await query
            .OrderBy(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        if (!isAdmin)
        {
            // Mark user's unread messages as read when user loads them
            var unread = messages.Where(m => m.SenderType == ChatSenderType.Operator && !m.IsRead);
            foreach (var m in unread) m.IsRead = true;
            if (unread.Any()) await _db.SaveChangesAsync();
        }

        var items = messages.Select(m =>
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
                Status = (ChatMessageStatusDto)(int)m.Status
            };
        }).ToList();

        return Ok(new { items, total });
    }

    [HttpPost("sessions/{id}/close")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult> CloseSession(int id)
    {
        var session = await _db.ChatSessions.FindAsync(id);
        if (session == null) return NotFound();

        session.Status = ChatSessionStatus.Closed;
        session.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("sessions/{id}")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult> DeleteSession(int id)
    {
        var session = await _db.ChatSessions.Include(s => s.Messages).FirstOrDefaultAsync(s => s.Id == id);
        if (session == null) return NotFound();

        _db.ChatMessages.RemoveRange(session.Messages);
        _db.ChatSessions.Remove(session);
        await _db.SaveChangesAsync();

        return Ok(new { message = "مکالمه و تمام پیام‌های آن حذف شد." });
    }

    [HttpPost("ban")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult> BanVisitor([FromBody] BanRequest req)
    {
        if (string.IsNullOrEmpty(req.VisitorId))
            return BadRequest();

        var existing = await _db.BannedVisitors.FirstOrDefaultAsync(b => b.VisitorId == req.VisitorId);
        if (existing != null)
            return Ok(new { message = "این کاربر قبلاً بن شده است." });

        _db.BannedVisitors.Add(new BannedVisitor
        {
            VisitorId = req.VisitorId,
            Reason = req.Reason,
            BannedByUserId = User.FindFirst(ClaimTypes.NameIdentifier) != null ? int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value) : null
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "کاربر بن شد." });
    }

    [HttpPost("unban")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult> UnbanVisitor([FromBody] BanRequest req)
    {
        if (string.IsNullOrEmpty(req.VisitorId))
            return BadRequest();

        var banned = await _db.BannedVisitors.FirstOrDefaultAsync(b => b.VisitorId == req.VisitorId);
        if (banned != null)
        {
            _db.BannedVisitors.Remove(banned);
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "بن کاربر لغو شد." });
    }

    [HttpGet("banned")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult<List<BannedVisitor>>> GetBannedVisitors()
    {
        return await _db.BannedVisitors.OrderByDescending(b => b.BannedAt).ToListAsync();
    }

    [HttpPost("check-ban")]
    public async Task<ActionResult> CheckBan([FromQuery] string visitorId)
    {
        if (string.IsNullOrEmpty(visitorId))
            return Ok(new { banned = false });

        var banned = await _db.BannedVisitors.AnyAsync(b => b.VisitorId == visitorId);
        return Ok(new { banned });
    }

    [HttpPost("sessions/{id}/transfer")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult> TransferSession(int id, [FromBody] TransferRequest req)
    {
        var session = await _db.ChatSessions.Include(s => s.Operator).FirstOrDefaultAsync(s => s.Id == id);
        if (session == null) return NotFound();

        session.OperatorId = req.OperatorId;
        await _db.SaveChangesAsync();

        var dto = new ChatSessionDto
        {
            Id = session.Id,
            VisitorId = session.VisitorId,
            UserName = session.UserName,
            UserEmail = session.UserEmail,
            UserPhone = session.UserPhone,
            OperatorId = session.OperatorId,
            OperatorName = session.Operator?.FullName,
            Status = (ChatSessionStatusDto)(int)session.Status,
            CreatedAt = session.CreatedAt,
            ClosedAt = session.ClosedAt,
            Rating = session.Rating,
            RatingComment = session.RatingComment
        };
        await _chatHub.Clients.Group("admins").SendAsync("SessionUpdated", dto);

        return Ok(new { message = "مکالمه منتقل شد." });
    }

    [HttpGet("operators")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult<List<ChatOperatorDto>>> GetOperators()
    {
        var operatorUserIds = await _db.RolePermissions
            .Where(rp => rp.Permission.Key == "admin.chat.manage")
            .Select(rp => rp.RoleId)
            .Distinct()
            .ToListAsync();

        var operators = await (from ur in _db.AppUserRoles
                               join u in _db.Users on ur.UserId equals u.Id
                               where u.IsActive && operatorUserIds.Contains(ur.RoleId)
                               select new ChatOperatorDto { Id = u.Id, FullName = u.FullName })
            .Distinct()
            .OrderBy(x => x.FullName)
            .ToListAsync();
        return operators;
    }

    [HttpGet("canned-responses")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult<List<CannedResponseDto>>> GetCannedResponses()
    {
        return await _db.CannedResponses
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CannedResponseDto
            {
                Id = c.Id,
                Title = c.Title,
                Content = c.Content,
                Category = c.Category,
                IsShared = c.IsShared,
                CreatedByUserId = c.CreatedByUserId,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
    }

    [HttpPost("canned-responses")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult<CannedResponseDto>> CreateCannedResponse([FromBody] CannedResponseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { message = "عنوان و متن پاسخ الزامی است." });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier) != null && int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value, out var uid) ? uid : 0;

        var item = new CannedResponse
        {
            Title = req.Title.Trim(),
            Content = req.Content.Trim(),
            Category = string.IsNullOrWhiteSpace(req.Category) ? null : req.Category.Trim(),
            IsShared = req.IsShared,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        _db.CannedResponses.Add(item);
        await _db.SaveChangesAsync();

        return Ok(new CannedResponseDto
        {
            Id = item.Id,
            Title = item.Title,
            Content = item.Content,
            Category = item.Category,
            IsShared = item.IsShared,
            CreatedByUserId = item.CreatedByUserId,
            CreatedAt = item.CreatedAt
        });
    }

    [HttpPut("canned-responses/{id}")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> UpdateCannedResponse(int id, [FromBody] CannedResponseRequest req)
    {
        var item = await _db.CannedResponses.FindAsync(id);
        if (item == null) return NotFound();

        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { message = "عنوان و متن پاسخ الزامی است." });

        item.Title = req.Title.Trim();
        item.Content = req.Content.Trim();
        item.Category = string.IsNullOrWhiteSpace(req.Category) ? null : req.Category.Trim();
        item.IsShared = req.IsShared;
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("canned-responses/{id}")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> DeleteCannedResponse(int id)
    {
        var item = await _db.CannedResponses.FindAsync(id);
        if (item == null) return NotFound();

        _db.CannedResponses.Remove(item);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpGet("faq")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult<List<ChatFaqEntryDto>>> GetFaqEntries()
    {
        return await _db.ChatFaqEntries
            .OrderBy(f => f.Id)
            .Select(f => new ChatFaqEntryDto
            {
                Id = f.Id,
                Question = f.Question,
                Answer = f.Answer,
                Keywords = f.Keywords,
                IsEnabled = f.IsEnabled
            })
            .ToListAsync();
    }

    [HttpPost("faq")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult<ChatFaqEntryDto>> CreateFaqEntry([FromBody] ChatFaqEntryDto req)
    {
        if (string.IsNullOrWhiteSpace(req.Question) || string.IsNullOrWhiteSpace(req.Answer))
            return BadRequest(new { message = "سوال و پاسخ الزامی است." });

        var item = new ChatFaqEntry
        {
            Question = req.Question.Trim(),
            Answer = req.Answer.Trim(),
            Keywords = string.IsNullOrWhiteSpace(req.Keywords) ? null : req.Keywords.Trim(),
            IsEnabled = req.IsEnabled
        };
        _db.ChatFaqEntries.Add(item);
        await _db.SaveChangesAsync();

        return Ok(new ChatFaqEntryDto
        {
            Id = item.Id,
            Question = item.Question,
            Answer = item.Answer,
            Keywords = item.Keywords,
            IsEnabled = item.IsEnabled
        });
    }

    [HttpPut("faq/{id}")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> UpdateFaqEntry(int id, [FromBody] ChatFaqEntryDto req)
    {
        var item = await _db.ChatFaqEntries.FindAsync(id);
        if (item == null) return NotFound();

        if (string.IsNullOrWhiteSpace(req.Question) || string.IsNullOrWhiteSpace(req.Answer))
            return BadRequest(new { message = "سوال و پاسخ الزامی است." });

        item.Question = req.Question.Trim();
        item.Answer = req.Answer.Trim();
        item.Keywords = string.IsNullOrWhiteSpace(req.Keywords) ? null : req.Keywords.Trim();
        item.IsEnabled = req.IsEnabled;
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("faq/{id}")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> DeleteFaqEntry(int id)
    {
        var item = await _db.ChatFaqEntries.FindAsync(id);
        if (item == null) return NotFound();

        _db.ChatFaqEntries.Remove(item);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpGet("site-settings")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<ActionResult<ChatSettingsPayload>> GetChatSiteSettings()
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings == null) return Ok(new ChatSettingsPayload());

        return Ok(new ChatSettingsPayload
        {
            EnableBaleChat = settings.EnableBaleChat,
            BaleBotToken = settings.BaleBotToken,
            BaleBotGroupId = settings.BaleBotGroupId,
            EnableTelegramChat = settings.EnableTelegramChat,
            TelegramBotToken = settings.TelegramBotToken,
            TelegramGroupId = settings.TelegramGroupId,
            EnableEitaaChat = settings.EnableEitaaChat,
            EitaaBotToken = settings.EitaaBotToken,
            EitaaGroupId = settings.EitaaGroupId,
            WelcomeMessage = settings.ChatWelcomeMessage,
            EnableAutoMessage = settings.ChatEnableAutoMessage,
            AutoMessageSeconds = settings.ChatAutoMessageSeconds,
            PhoneRequired = settings.ChatPhoneRequired,
            EnableAiAssistant = settings.ChatEnableAiAssistant,
            AiProvider = settings.ChatAiProvider,
            AiApiUrl = settings.ChatAiApiUrl,
            AiModel = settings.ChatAiModel,
            AiApiKey = settings.ChatAiApiKey,
            AiSystemPrompt = settings.ChatAiSystemPrompt
        });
    }

    [HttpPut("site-settings")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> SaveChatSiteSettings([FromBody] ChatSettingsPayload p)
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SiteSettings();
            _db.SiteSettings.Add(settings);
        }

        settings.EnableBaleChat = p.EnableBaleChat;
        settings.BaleBotToken = string.IsNullOrWhiteSpace(p.BaleBotToken) ? null : p.BaleBotToken.Trim();
        settings.BaleBotGroupId = string.IsNullOrWhiteSpace(p.BaleBotGroupId) ? null : p.BaleBotGroupId.Trim();
        settings.EnableTelegramChat = p.EnableTelegramChat;
        settings.TelegramBotToken = string.IsNullOrWhiteSpace(p.TelegramBotToken) ? null : p.TelegramBotToken.Trim();
        settings.TelegramGroupId = string.IsNullOrWhiteSpace(p.TelegramGroupId) ? null : p.TelegramGroupId.Trim();
        settings.EnableEitaaChat = p.EnableEitaaChat;
        settings.EitaaBotToken = string.IsNullOrWhiteSpace(p.EitaaBotToken) ? null : p.EitaaBotToken.Trim();
        settings.EitaaGroupId = string.IsNullOrWhiteSpace(p.EitaaGroupId) ? null : p.EitaaGroupId.Trim();
        settings.ChatWelcomeMessage = p.WelcomeMessage;
        settings.ChatEnableAutoMessage = p.EnableAutoMessage;
        settings.ChatAutoMessageSeconds = p.AutoMessageSeconds > 0 ? p.AutoMessageSeconds : 60;
        settings.ChatPhoneRequired = p.PhoneRequired;
        settings.ChatEnableAiAssistant = p.EnableAiAssistant;
        settings.ChatAiProvider = p.AiProvider;
        settings.ChatAiApiUrl = string.IsNullOrWhiteSpace(p.AiApiUrl) ? null : p.AiApiUrl.Trim();
        settings.ChatAiModel = p.AiModel;
        settings.ChatAiApiKey = string.IsNullOrWhiteSpace(p.AiApiKey) ? null : p.AiApiKey.Trim();
        settings.ChatAiSystemPrompt = p.AiSystemPrompt;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "تنظیمات چت ذخیره شد." });
    }

    [HttpPost("sessions/{id}/rate")]
    public async Task<ActionResult> RateSession(int id, [FromBody] RateRequest req, [FromQuery] string? visitorId)
    {
        var session = await _db.ChatSessions.FindAsync(id);
        if (session == null) return NotFound();

        var isAdmin = User.Identity?.IsAuthenticated == true &&
                      User.HasClaim("perm", "admin.chat.manage");
        if (!isAdmin && (string.IsNullOrEmpty(visitorId) || session.VisitorId != visitorId))
            return Unauthorized();

        session.Rating = req.Rating;
        session.RatingComment = req.Comment;
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("upload")]
    public async Task<ActionResult<dynamic>> UploadFile([FromForm] IFormFile file, [FromQuery] string? visitorId = null)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "فایلی ارسال نشده است." });

        var isAdmin = User.Identity?.IsAuthenticated == true &&
                      User.HasClaim("perm", "admin.chat.manage");

        if (!isAdmin)
        {
            if (string.IsNullOrWhiteSpace(visitorId))
                return Unauthorized(new { message = "شناسه بازدیدکننده الزامی است." });

            var activeSession = await _db.ChatSessions
                .Where(s => s.VisitorId == visitorId && s.Status == ChatSessionStatus.Active)
                .AnyAsync();
            if (!activeSession)
                return Unauthorized(new { message = "مکالمه فعالی برای این بازدیدکننده یافت نشد." });
        }

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { message = "حجم فایل نباید بیشتر از ۱۰ مگابایت باشد." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp3", ".ogg", ".wav", ".mp4", ".webm", ".m4a", ".aac", ".oga", ".pdf", ".doc", ".docx" };
        if (!allowedExtensions.Contains(ext))
            return BadRequest(new { message = "فرمت فایل مجاز نیست." });

        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(uploadsRoot, fileName);
        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new
        {
            url = $"/uploads/chat/{fileName}",
            fileName = file.FileName,
            fileSize = file.Length,
            contentType = file.ContentType
        });
    }
}

public class SendChatMessageRequest
{
    public int SessionId { get; set; }
    public string? Content { get; set; }
    public ChatMessageTypeDto MessageType { get; set; } = ChatMessageTypeDto.Text;
    public string? MediaUrl { get; set; }
    public string? VisitorId { get; set; }
    public int? ReplyToId { get; set; }
}

public class BaleSettingsRequest
{
    public string? BaleToken { get; set; }
    public string? BaleGroupId { get; set; }
}

public class RateRequest
{
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class CannedResponseRequest
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Category { get; set; }
    public bool IsShared { get; set; } = true;
}

public class ChatSettingsPayload
{
    public bool EnableBaleChat { get; set; }
    public string? BaleBotToken { get; set; }
    public string? BaleBotGroupId { get; set; }
    public bool EnableTelegramChat { get; set; }
    public string? TelegramBotToken { get; set; }
    public string? TelegramGroupId { get; set; }
    public bool EnableEitaaChat { get; set; }
    public string? EitaaBotToken { get; set; }
    public string? EitaaGroupId { get; set; }
    public string? WelcomeMessage { get; set; }
    public bool EnableAutoMessage { get; set; }
    public int AutoMessageSeconds { get; set; } = 60;
    public bool PhoneRequired { get; set; }
    public bool EnableAiAssistant { get; set; }
    public string? AiProvider { get; set; }
    public string? AiApiUrl { get; set; }
    public string? AiModel { get; set; }
    public string? AiApiKey { get; set; }
    public string? AiSystemPrompt { get; set; }
}

public class TransferRequest
{
    public int OperatorId { get; set; }
}

public class BanRequest
{
    public string VisitorId { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
