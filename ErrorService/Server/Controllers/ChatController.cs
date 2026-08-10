using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public ChatController(ErrorServiceDbContext db, IHttpClientFactory httpClientFactory, BaleBotService baleBot, IConfiguration config)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _baleBot = baleBot;
        _config = config;
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
        var result = await _baleBot.SetWebhookAsync(webhookUrl, secret);
        return Ok(result);
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

        var items = messages.Select(m => new ChatMessageDto
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
        var session = await _db.ChatSessions.FindAsync(id);
        if (session == null) return NotFound();

        session.OperatorId = req.OperatorId;
        await _db.SaveChangesAsync();

        return Ok(new { message = "مکالمه منتقل شد." });
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
    public async Task<ActionResult<dynamic>> UploadFile([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "فایلی ارسال نشده است." });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { message = "حجم فایل نباید بیشتر از ۱۰ مگابایت باشد." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp3", ".ogg", ".wav", ".mp4", ".pdf", ".doc", ".docx" };
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

public class TransferRequest
{
    public int OperatorId { get; set; }
}

public class BanRequest
{
    public string VisitorId { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
