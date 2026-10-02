using System.Security.Claims;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public NotificationsController(ErrorServiceDbContext db) => _db = db;

    private int? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("user_id")
                  ?? User.FindFirstValue("sub");
        return int.TryParse(raw, out var id) ? id : null;
    }

    private int? CurrentWorkshopUserId()
    {
        var raw = User.FindFirstValue("workshop_user_id");
        return int.TryParse(raw, out var id) ? id : null;
    }

    private static string SeverityTitle(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Success => "موفق",
        NotificationSeverity.Warning => "هشدار",
        NotificationSeverity.Critical => "بحرانی",
        _ => "اطلاع"
    };

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> GetMine([FromQuery] int take = 50, [FromQuery] bool unreadOnly = false)
    {
        var userId = CurrentUserId();
        var workshopUserId = CurrentWorkshopUserId();
        if (!userId.HasValue && !workshopUserId.HasValue) return Ok(new List<NotificationDto>());
        take = Math.Clamp(take, 1, 100);

        var items = await _db.NotificationRecipients
            .AsNoTracking()
            .Where(x => x.HiddenAt == null &&
                (!unreadOnly || !x.IsRead) &&
                ((userId.HasValue && x.AppUserId == userId.Value) ||
                 (workshopUserId.HasValue && x.WorkshopUserId == workshopUserId.Value)))
            .OrderByDescending(x => x.Notification.CreatedAt)
            .Take(take)
            .Select(x => new NotificationDto
            {
                Id = x.NotificationId,
                EventType = x.Notification.EventType,
                Title = x.Notification.Title,
                Body = x.Notification.Body,
                Severity = (int)x.Notification.Severity,
                SeverityTitle = SeverityTitle(x.Notification.Severity),
                ActionUrl = x.Notification.ActionUrl,
                IsRead = x.IsRead,
                CreatedAt = x.Notification.CreatedAt,
                ReadAt = x.ReadAt
            })
            .ToListAsync();

        return Ok(items);
    }

    [Authorize]
    [HttpGet("summary")]
    public async Task<ActionResult<NotificationSummaryDto>> GetSummary()
    {
        var userId = CurrentUserId();
        var workshopUserId = CurrentWorkshopUserId();
        if (!userId.HasValue && !workshopUserId.HasValue) return Ok(new NotificationSummaryDto());
        var today = DateTimeOffset.UtcNow.Date;

        var query = _db.NotificationRecipients.Where(x => x.HiddenAt == null &&
            ((userId.HasValue && x.AppUserId == userId.Value) ||
             (workshopUserId.HasValue && x.WorkshopUserId == workshopUserId.Value)));
        return Ok(new NotificationSummaryDto
        {
            UnreadCount = await query.CountAsync(x => !x.IsRead),
            TodayCount = await query.CountAsync(x => x.Notification.CreatedAt >= today)
        });
    }

    [Authorize]
    [HttpPut("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        var userId = CurrentUserId();
        var workshopUserId = CurrentWorkshopUserId();
        if (!userId.HasValue && !workshopUserId.HasValue) return Unauthorized();
        var recipient = await _db.NotificationRecipients
            .FirstOrDefaultAsync(x => x.NotificationId == id && x.HiddenAt == null &&
                ((userId.HasValue && x.AppUserId == userId.Value) ||
                 (workshopUserId.HasValue && x.WorkshopUserId == workshopUserId.Value)));
        if (recipient == null) return NotFound();
        recipient.IsRead = true;
        recipient.ReadAt ??= DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpPut("read-all")]
    public async Task<ActionResult<int>> MarkAllRead()
    {
        var userId = CurrentUserId();
        var workshopUserId = CurrentWorkshopUserId();
        if (!userId.HasValue && !workshopUserId.HasValue) return Unauthorized();

        var recipients = await _db.NotificationRecipients
            .Where(x => x.HiddenAt == null && !x.IsRead &&
                ((userId.HasValue && x.AppUserId == userId.Value) ||
                 (workshopUserId.HasValue && x.WorkshopUserId == workshopUserId.Value)))
            .ToListAsync();
        if (recipients.Count == 0) return Ok(0);

        var now = DateTimeOffset.UtcNow;
        foreach (var recipient in recipients)
        {
            recipient.IsRead = true;
            recipient.ReadAt ??= now;
        }
        await _db.SaveChangesAsync();
        return Ok(recipients.Count);
    }

    [Authorize]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Hide(long id)
    {
        var userId = CurrentUserId();
        var workshopUserId = CurrentWorkshopUserId();
        if (!userId.HasValue && !workshopUserId.HasValue) return Unauthorized();
        var recipient = await _db.NotificationRecipients
            .FirstOrDefaultAsync(x => x.NotificationId == id && x.HiddenAt == null &&
                ((userId.HasValue && x.AppUserId == userId.Value) ||
                 (workshopUserId.HasValue && x.WorkshopUserId == workshopUserId.Value)));
        if (recipient == null) return NotFound();
        recipient.HiddenAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.notifications.view")]
    [HttpGet("admin/summary")]
    public async Task<ActionResult<NotificationSummaryDto>> GetAdminSummary()
    {
        var today = DateTimeOffset.UtcNow.Date;
        return Ok(new NotificationSummaryDto
        {
            TodayCount = await _db.Notifications.CountAsync(x => x.CreatedAt >= today),
            PendingDeliveryCount = await _db.NotificationDeliveries.CountAsync(x => x.Status == NotificationDeliveryStatus.Pending),
            FailedDeliveryCount = await _db.NotificationDeliveries.CountAsync(x => x.Status == NotificationDeliveryStatus.Failed),
            UnreadCount = await _db.NotificationRecipients.CountAsync(x => !x.IsRead && x.HiddenAt == null)
        });
    }

    [Authorize(Policy = "perm:admin.notifications.view")]
    [HttpGet("admin")]
    public async Task<ActionResult<List<NotificationDto>>> GetAdminList([FromQuery] int take = 100)
    {
        take = Math.Clamp(take, 1, 200);
        var items = await _db.Notifications
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new NotificationDto
            {
                Id = x.Id,
                EventType = x.EventType,
                Title = x.Title,
                Body = x.Body,
                Severity = (int)x.Severity,
                SeverityTitle = SeverityTitle(x.Severity),
                ActionUrl = x.ActionUrl,
                IsRead = false,
                CreatedAt = x.CreatedAt,
                IsBroadcast = x.IsBroadcast,
                RecipientCount = x.Recipients.Count,
                CreatedByName = x.CreatedByUserId == null
                    ? null
                    : _db.Users.Where(u => u.Id == x.CreatedByUserId).Select(u => u.FullName).FirstOrDefault()
            })
            .ToListAsync();
        return Ok(items);
    }

    [Authorize(Policy = "perm:admin.notifications.manage")]
    [HttpPost("admin/preview-recipients")]
    public async Task<ActionResult<int>> PreviewRecipients([FromBody] NotificationCreateRequest request)
    {
        if (request == null) return BadRequest();
        var (appUserIds, workshopUserIds, resolveError) = await ResolveRecipientsAsync(request);
        if (resolveError != null) return BadRequest(resolveError);
        return Ok(appUserIds.Count + workshopUserIds.Count);
    }

    [Authorize(Policy = "perm:admin.notifications.view")]
    [HttpGet("admin/rules")]
    public async Task<ActionResult<List<NotificationRuleDto>>> GetRules()
    {
        var rules = await _db.NotificationRules
            .AsNoTracking()
            .OrderBy(x => x.Group)
            .ThenBy(x => x.Id)
            .ToListAsync();

        if (rules.Count == 0)
        {
            foreach (var def in NotificationRuleDefaults.All)
            {
                rules.Add(new NotificationRule
                {
                    Id = 0,
                    EventType = def.EventType,
                    Group = def.Group,
                    Label = def.Label,
                    IsEnabled = def.IsEnabled,
                    Severity = def.Severity,
                    TitleTemplate = def.Title,
                    BodyTemplate = def.Body,
                    ActionUrlTemplate = def.ActionUrl,
                    BroadcastToAdmins = def.BroadcastToAdmins
                });
            }
        }

        return Ok(rules.Select(x => new NotificationRuleDto
        {
            Id = x.Id,
            EventType = x.EventType,
            Group = x.Group,
            Label = x.Label,
            IsEnabled = x.IsEnabled,
            Severity = x.Severity.HasValue ? (int)x.Severity.Value : null,
            TitleTemplate = x.TitleTemplate,
            BodyTemplate = x.BodyTemplate,
            ActionUrlTemplate = x.ActionUrlTemplate,
            BroadcastToAdmins = x.BroadcastToAdmins,
            Placeholders = NotificationRuleDefaults.PlaceholdersFor(x.EventType).ToList(),
            UpdatedAt = x.UpdatedAt
        }).ToList());
    }

    [Authorize(Policy = "perm:admin.notifications.manage")]
    [HttpPut("admin/rules/{eventType}")]
    public async Task<ActionResult<NotificationRuleDto>> UpdateRule(string eventType, [FromBody] NotificationRuleUpdateRequest request)
    {
        if (request == null) return BadRequest();
        if (string.IsNullOrWhiteSpace(request.TitleTemplate) || string.IsNullOrWhiteSpace(request.BodyTemplate))
            return BadRequest("عنوان و متن قالب اعلان الزامی است.");
        if (request.TitleTemplate.Trim().Length > 250 || request.BodyTemplate.Trim().Length > 4000)
            return BadRequest("طول قالب بیش از حد مجاز است.");
        if (request.Severity.HasValue && !Enum.IsDefined(typeof(NotificationSeverity), request.Severity.Value))
            return BadRequest("سطح اعلان معتبر نیست.");
        if (request.BroadcastToAdmins && !CanBroadcast())
            return BadRequest("برای فعال‌سازی ارسال پخشی به مدیران، دسترسی «ارسال پخشی» لازم است.");

        var rule = await _db.NotificationRules.FirstOrDefaultAsync(x => x.EventType == eventType);
        if (rule == null) return BadRequest($"رویداد اعلان «{eventType}» شناخته نشده است.");

        rule.IsEnabled = request.IsEnabled;
        rule.Severity = request.Severity.HasValue ? (NotificationSeverity)request.Severity.Value : null;
        rule.TitleTemplate = request.TitleTemplate.Trim();
        rule.BodyTemplate = request.BodyTemplate.Trim();
        rule.ActionUrlTemplate = string.IsNullOrWhiteSpace(request.ActionUrlTemplate) ? null : request.ActionUrlTemplate.Trim();
        rule.BroadcastToAdmins = request.BroadcastToAdmins;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        rule.UpdatedByUserId = CurrentUserId();

        await _db.SaveChangesAsync();

        return Ok(new NotificationRuleDto
        {
            Id = rule.Id,
            EventType = rule.EventType,
            Group = rule.Group,
            Label = rule.Label,
            IsEnabled = rule.IsEnabled,
            Severity = rule.Severity.HasValue ? (int)rule.Severity.Value : null,
            TitleTemplate = rule.TitleTemplate,
            BodyTemplate = rule.BodyTemplate,
            ActionUrlTemplate = rule.ActionUrlTemplate,
            BroadcastToAdmins = rule.BroadcastToAdmins,
            Placeholders = NotificationRuleDefaults.PlaceholdersFor(rule.EventType).ToList(),
            UpdatedAt = rule.UpdatedAt
        });
    }

    private bool CanBroadcast() =>
        User.HasClaim("perm", "admin.notifications.broadcast") || User.IsInRole("super_admin");

    private async Task<(List<int> Ids, string? Error)> ResolvePhoneUsersAsync(List<string> phones)
    {
        var wanted = new Dictionary<string, string>();
        foreach (var raw in phones)
        {
            var canonical = CanonicalPhone(raw);
            if (canonical == null)
                return (new List<int>(), $"شماره موبایل نامعتبر است: {raw.Trim()}");
            wanted.TryAdd(canonical, raw.Trim());
        }
        if (wanted.Count == 0) return (new List<int>(), null);

        var variants = new HashSet<string>();
        foreach (var canonical in wanted.Keys)
        {
            variants.Add(canonical);
            variants.Add(canonical.Length == 11 && canonical[0] == '0' ? canonical[1..] : canonical);
            if (canonical.Length >= 10) variants.Add("98" + (canonical[0] == '0' ? canonical[1..] : canonical));
        }

        var matched = await _db.Users
            .Where(u => u.IsActive && variants.Contains(u.PhoneNumber))
            .Select(u => new { u.Id, u.PhoneNumber })
            .ToListAsync();

        var ids = new List<int>();
        var found = new HashSet<string>();
        foreach (var u in matched)
        {
            var c = CanonicalPhone(u.PhoneNumber);
            if (c != null && wanted.ContainsKey(c) && found.Add(c))
                ids.Add(u.Id);
        }

        var missing = wanted.Where(x => !found.Contains(x.Key)).Select(x => x.Value).ToList();
        if (missing.Count > 0)
            return (new List<int>(), $"این شماره‌ها کاربر فعالی ندارند: {string.Join("، ", missing)}");
        return (ids, null);
    }

    private static string? CanonicalPhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var normalized = NormalizeDigits(raw.Trim());
        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0098")) digits = "0" + digits[4..];
        else if (digits.StartsWith("98") && digits.Length >= 12) digits = "0" + digits[2..];
        else if (digits.Length == 10 && digits[0] != '0') digits = "0" + digits;
        return digits.Length is >= 10 and <= 15 ? digits : null;
    }

    private static string NormalizeDigits(string s)
    {
        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c >= '۰' && c <= '۹') chars[i] = (char)('0' + (c - '۰'));
            else if (c >= '٠' && c <= '٩') chars[i] = (char)('0' + (c - '٠'));
        }
        return new string(chars);
    }

    private async Task<(List<int> AppUserIds, List<int> WorkshopUserIds, string? Error)> ResolveRecipientsAsync(NotificationCreateRequest request)
    {
        if ((request.BroadcastToAdmins || request.IncludeSuperAdmins || request.IncludeAllUsers) && !CanBroadcast())
            return (new List<int>(), new List<int>(), "برای ارسال به مدیران مجاز، سوپر ادمین یا همه کاربران، دسترسی «ارسال پخشی» لازم است.");

        var recipientIds = new HashSet<int>();
        if (request.PhoneNumbers is { Count: > 0 })
        {
            var (phoneUserIds, phoneError) = await ResolvePhoneUsersAsync(request.PhoneNumbers);
            if (phoneError != null)
                return (recipientIds.ToList(), new List<int>(), phoneError);
            recipientIds.UnionWith(phoneUserIds);
        }

        if (request.IncludeSuperAdmins)
        {
            var superAdminIds = await (
                from ur in _db.AppUserRoles
                join u in _db.Users on ur.UserId equals u.Id
                where u.IsActive && ur.Role.Key == "super_admin"
                select u.Id)
                .ToListAsync();
            recipientIds.UnionWith(superAdminIds);
        }

        if (request.BroadcastToAdmins)
        {
            var adminIds = await (
                from ur in _db.AppUserRoles
                join u in _db.Users on ur.UserId equals u.Id
                join rp in _db.RolePermissions on ur.RoleId equals rp.RoleId
                join p in _db.Permissions on rp.PermissionId equals p.Id
                where u.IsActive && p.Key == "admin.notifications.view"
                select ur.UserId)
                .Distinct()
                .ToListAsync();
            recipientIds.UnionWith(adminIds);
        }

        if (request.IncludeAllUsers)
        {
            var allIds = await _db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync();
            recipientIds.UnionWith(allIds);
        }

        if (request.PurchasedProductIds != null && request.PurchasedProductIds.Count > 0)
        {
            var pids = request.PurchasedProductIds.Where(x => x > 0).Distinct().ToList();
            if (pids.Count > 0)
            {
                var buyerIds = await _db.Orders
                    .Where(o => o.UserId != null
                        && (o.Status == OrderStatus.Approved
                            || o.Status == OrderStatus.Completed
                            || o.Status == OrderStatus.ReceiptUploaded)
                        && o.Items.Any(i => pids.Contains(i.ProductId)))
                    .Select(o => o.UserId!.Value)
                    .Distinct()
                    .ToListAsync();
                recipientIds.UnionWith(buyerIds);
            }
        }

        var workshopUserIds = new List<int>();
        if (request.IncludeWorkshopUsers)
        {
            if (!request.WorkshopId.HasValue || request.WorkshopId.Value <= 0)
                return (recipientIds.ToList(), workshopUserIds, "برای ارسال به پرسنل کارگاه، کارگاه را مشخص کنید.");
            workshopUserIds = await _db.WorkshopUsers
                .Where(w => w.WorkshopId == request.WorkshopId.Value && w.IsActive)
                .Select(w => w.Id)
                .ToListAsync();
            if (workshopUserIds.Count == 0)
                return (recipientIds.ToList(), workshopUserIds, "کاربر فعالی برای این کارگاه یافت نشد.");
        }

        if (recipientIds.Count == 0 && workshopUserIds.Count == 0)
            return (new List<int>(), new List<int>(), "حداقل یک گیرنده انتخاب کنید.");

        return (recipientIds.ToList(), workshopUserIds, null);
    }

    [Authorize(Policy = "perm:admin.notifications.manage")]
    [HttpPut("admin/{id:long}")]
    public async Task<ActionResult<NotificationDto>> UpdateAdmin(long id, NotificationUpdateRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest("عنوان و متن اعلان الزامی است.");
        if (!Enum.IsDefined(typeof(NotificationSeverity), request.Severity))
            return BadRequest("سطح اعلان معتبر نیست.");

        var notification = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id);
        if (notification == null) return NotFound("اعلان یافت نشد.");

        notification.Title = request.Title.Trim();
        notification.Body = request.Body.Trim();
        notification.Severity = (NotificationSeverity)request.Severity;
        notification.ActionUrl = string.IsNullOrWhiteSpace(request.ActionUrl) ? null : request.ActionUrl.Trim();
        await _db.SaveChangesAsync();

        return Ok(new NotificationDto
        {
            Id = notification.Id,
            EventType = notification.EventType,
            Title = notification.Title,
            Body = notification.Body,
            Severity = (int)notification.Severity,
            SeverityTitle = SeverityTitle(notification.Severity),
            ActionUrl = notification.ActionUrl,
            CreatedAt = notification.CreatedAt,
            IsBroadcast = notification.IsBroadcast
        });
    }

    [Authorize(Policy = "perm:admin.notifications.manage")]
    [HttpDelete("admin/{id:long}")]
    public async Task<IActionResult> DeleteAdmin(long id)
    {
        var notification = await _db.Notifications
            .Include(x => x.Recipients)
            .Include(x => x.Deliveries)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (notification == null) return NotFound("اعلان یافت نشد.");

        if (notification.Deliveries.Count > 0) _db.NotificationDeliveries.RemoveRange(notification.Deliveries);
        if (notification.Recipients.Count > 0) _db.NotificationRecipients.RemoveRange(notification.Recipients);
        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize(Policy = "perm:admin.notifications.manage")]
    [HttpPost("admin/manual")]
    public async Task<ActionResult<NotificationDto>> CreateManual(NotificationCreateRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            return BadRequest("عنوان و متن اعلان الزامی است.");
        if (!Enum.IsDefined(typeof(NotificationSeverity), request.Severity))
            return BadRequest("سطح اعلان معتبر نیست.");

        var creatorId = CurrentUserId();
        var (appUserIds, workshopUserIds, resolveError) = await ResolveRecipientsAsync(request);
        if (resolveError != null) return BadRequest(resolveError);
        var recipientIds = new HashSet<int>(appUserIds);

        var notification = new Notification
        {
            EventType = "manual",
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            Severity = (NotificationSeverity)request.Severity,
            ActionUrl = string.IsNullOrWhiteSpace(request.ActionUrl) ? null : request.ActionUrl.Trim(),
            WorkshopId = request.WorkshopId,
            CreatedByUserId = creatorId,
            IsBroadcast = request.BroadcastToAdmins || request.IncludeAllUsers || request.IncludeSuperAdmins
        };

        foreach (var appUserId in recipientIds)
        {
            notification.Recipients.Add(new NotificationRecipient { AppUserId = appUserId });
        }
        foreach (var workshopUserId in workshopUserIds)
        {
            notification.Recipients.Add(new NotificationRecipient { WorkshopUserId = workshopUserId });
        }

        foreach (var _ in notification.Recipients)
        {
            notification.Deliveries.Add(new NotificationDelivery
            {
                Channel = NotificationDeliveryChannel.Internal,
                Status = NotificationDeliveryStatus.Sent,
                AttemptCount = 1,
                LastAttemptAt = DateTimeOffset.UtcNow,
                SentAt = DateTimeOffset.UtcNow
            });
        }

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        return Ok(new NotificationDto
        {
            Id = notification.Id,
            EventType = notification.EventType,
            Title = notification.Title,
            Body = notification.Body,
            Severity = (int)notification.Severity,
            SeverityTitle = SeverityTitle(notification.Severity),
            ActionUrl = notification.ActionUrl,
            CreatedAt = notification.CreatedAt
        });
    }
}
