using ErrorService.Server.Data;
using ErrorService.Server.Hubs;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Server.Services.ChatAi;
using ErrorService.Server.Services.Messenger;
using ErrorService.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly NotificationEventService _notificationEvents;
    private readonly ChatAiResponder _aiResponder;
    private readonly ChatAiCoordinator _aiCoordinator;
    private readonly IServiceScopeFactory _scopeFactory;

    public ChatController(ErrorServiceDbContext db, IHttpClientFactory httpClientFactory, BaleBotService baleBot,
        IConfiguration config, IHubContext<ChatHub> chatHub, MessengerRouter router, ILogger<ChatController> logger,
        NotificationEventService notificationEvents, ChatAiResponder aiResponder, ChatAiCoordinator aiCoordinator,
        IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _baleBot = baleBot;
        _config = config;
        _chatHub = chatHub;
        _router = router;
        _notificationEvents = notificationEvents;
        _aiResponder = aiResponder;
        _aiCoordinator = aiCoordinator;
        _scopeFactory = scopeFactory;
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

    [HttpPost("ai-test")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> TestAi([FromBody] AiTestRequest? req = null)
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        var enabled = settings?.ChatEnableAiAssistant == true;
        var apiUrl = !string.IsNullOrWhiteSpace(req?.ApiUrl) ? req!.ApiUrl!.Trim() : settings?.ChatAiApiUrl?.Trim() ?? "";
        var model = !string.IsNullOrWhiteSpace(req?.Model) ? req!.Model!.Trim() : settings?.ChatAiModel?.Trim() ?? "";
        var apiKey = !string.IsNullOrWhiteSpace(req?.ApiKey) ? req!.ApiKey!.Trim() : settings?.ChatAiApiKey?.Trim() ?? "";
        var temperature = req?.Temperature is > 0 and <= 2 ? req.Temperature.Value : (settings?.ChatAiTemperature is > 0 and <= 2 ? settings.ChatAiTemperature : 0.6);
        var maxTokens = req is { MaxTokens: > 0 } ? req.MaxTokens.Value : (settings?.ChatAiMaxTokens > 0 ? settings.ChatAiMaxTokens : 450);

        if (apiUrl.Length == 0 || model.Length == 0)
            return Ok(new { success = false, message = "آدرس API یا نام مدل خالی است.", apiUrl, model });
        if (apiKey.Length == 0)
            return Ok(new { success = false, message = "کلید API ثبت نشده است.", apiUrl, model });

        try
        {
            var client = _httpClientFactory.CreateClient();
            var isAuto = string.Equals(model, "auto", StringComparison.OrdinalIgnoreCase);
            var candidates = isAuto
                ? OpenAiCompatibleChatService.AutoModels
                : new[] { model };

            string? reply = null;
            string? matchedModel = null;
            long elapsedTotal = 0;
            var candidateErrors = new List<string>();

            foreach (var candidate in candidates)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl.TrimEnd('/')}/chat/completions");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                var messages = new List<object>
                {
                    new { role = "system", content = "تو دستیار پشتیبانی فروشگاه هستی. به فارسی، کوتاه و مؤدبانه پاسخ بده." },
                    new { role = "user", content = "سلام، دستگاه لباسشویی‌ام خطای EF می‌دهد، چیکار کنم؟" }
                };
                var payload = new { model = candidate, messages, temperature, max_tokens = Math.Min(maxTokens, 600) };
                request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var response = await client.SendAsync(request, cts.Token);
                var body = await response.Content.ReadAsStringAsync();
                sw.Stop();
                elapsedTotal += sw.ElapsedMilliseconds;

                if (!response.IsSuccessStatusCode)
                {
                    var errDetail = ExtractApiError(body, (int)response.StatusCode);
                    candidateErrors.Add($"«{candidate}»: {errDetail}");
                    if (!isAuto) break;
                    continue;
                }

                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                {
                    candidateErrors.Add($"«{candidate}» پاسخ خالی برگرداند (احتمالاً مدل موقتاً در دسترس نیست).");
                    if (!isAuto) break;
                    continue;
                }

                var content = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                if (string.IsNullOrWhiteSpace(content))
                {
                    candidateErrors.Add($"«{candidate}» پاسخ خالی برگرداند.");
                    if (!isAuto) break;
                    continue;
                }

                reply = content.Trim();
                matchedModel = candidate;
                var truncated = choices[0].TryGetProperty("finish_reason", out var fr)
                    && fr.ValueKind == System.Text.Json.JsonValueKind.String
                    && string.Equals(fr.GetString(), "length", StringComparison.OrdinalIgnoreCase);
                if (truncated)
                    candidateErrors.Add($"«{candidate}» پاسخ را به دلیل سقف توکن (max_tokens) وسط جمله برید؛ مقدار «سقف توکن پاسخ» را در تنظیمات چت بالاتر ببرید.");
                break;
            }

            if (reply == null)
            {
                var allErrors = candidateErrors.Count > 0
                    ? string.Join(" | ", candidateErrors)
                    : "پاسخی برنگشت.";
                var hint = candidateErrors.Any(e => e.Contains("مجاز نیست") || e.Contains("Access denied") || e.Contains("security policy"))
                    ? "\n💡 این خطا یعنی کلید شما به آن مدل دسترسی ندارد (مدل‌های free گاهی بسته می‌شوند). در تنظیمات چت مدل دیگری انتخاب کنید یا از «سرویس‌دهنده‌ها و زنجیره خودکار» چند مدل اضافه کنید تا سویچ خودکار انجام شود."
                    : "";
                return Ok(new { success = false, message = $"مدل پاسخ نداد. نام مدل، اعتبار کلید و سقف رایگان را بررسی کنید. {allErrors}{hint}", apiUrl, model, detail = allErrors, elapsedMs = elapsedTotal });
            }

            var successBase = isAuto
                ? $"✅ زنجیره خودکار سالم است («{matchedModel}» پاسخ داد)."
                : "✅ مدل سالم است و پاسخ داد.";
            var truncationNote = candidateErrors.FirstOrDefault(e => e.StartsWith($"«{matchedModel}» پاسخ را به دلیل"));
            if (truncationNote != null)
                successBase += "\n⚠️ " + truncationNote;
            return Ok(new { success = true, message = successBase, reply, apiUrl, model = matchedModel ?? model, elapsedMs = elapsedTotal, enabled });
        }
        catch (OperationCanceledException)
        {
            return Ok(new { success = false, message = "⚠️ مدل بیش از ۳۰ ثانیه طول کشید (کند است).", apiUrl, model });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"خطا: {ex.Message}", apiUrl, model });
        }
    }

    /// <summary>
    /// خطای واقعی API را از بدنه پاسخ استخراج و به فارسی دوستانه ترجمه می‌کند.
    /// </summary>
    private static string ExtractApiError(string body, int statusCode)
    {
        string? providerMsg = null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (err.TryGetProperty("message", out var em) && em.ValueKind == System.Text.Json.JsonValueKind.String)
                        providerMsg = em.GetString();
                }
                else if (err.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    providerMsg = err.GetString();
                }
            }
            if (providerMsg == null && doc.RootElement.TryGetProperty("message", out var m2) && m2.ValueKind == System.Text.Json.JsonValueKind.String)
                providerMsg = m2.GetString();
        }
        catch { }

        var technical = providerMsg;
        if (!string.IsNullOrWhiteSpace(technical) && technical.Length > 220) technical = technical[..220] + "…";

        var friendly = statusCode switch
        {
            401 => "کلید API نامعتبر یا منقضی است.",
            402 => "اعتبار حساب کافی نیست (شارژ کنید).",
            403 => "دسترسی به این مدل مجاز نیست.",
            404 => "نام مدل اشتباه است یا از فهرست حذف شده.",
            429 => "محدودیت تعداد درخواست (rate-limit) — کمی بعد دوباره تست کنید.",
            >= 500 => "سرویس ارائه‌دهنده موقتاً از دسترس خارج است.",
            _ => $"سرور با کد {statusCode} پاسخ داد."
        };

        return string.IsNullOrWhiteSpace(technical) ? friendly : $"{friendly} جزئیات: {technical}";
    }

    [HttpGet("ai-quota")]
    public async Task<IActionResult> GetAiQuota([FromQuery] string? visitorId)
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        var assistantEnabled = settings?.ChatEnableAiAssistant == true;
        var guestEnabled = settings?.ChatAiGuestEnabled == true;
        var dailyLimitSetting = settings?.ChatAiDailyLimit > 0 ? settings.ChatAiDailyLimit : 3;
        var sessionLimit = settings?.ChatAiSessionLimit > 0 ? settings.ChatAiSessionLimit : 12;

        int? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var uidRaw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (uidRaw != null && int.TryParse(uidRaw, out var uid)) userId = uid;
        }

        ChatSession? session = null;
        if (!string.IsNullOrEmpty(visitorId))
        {
            session = await _db.ChatSessions.AsNoTracking()
                .Where(s => s.VisitorId == visitorId && s.Status == ChatSessionStatus.Active)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
            if (session != null && session.UserId != null && userId == null) userId = session.UserId;
        }

        var isGuest = userId == null;
        var tehranTodayUtc = DateTime.UtcNow.AddMinutes(210).Date.AddMinutes(-210);

        int dailyLimit = dailyLimitSetting;
        int usedToday = 0;
        bool memberDisabled = false;

        if (!isGuest && userId.HasValue)
        {
            var uid = userId.Value;
            var member = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid);
            if (member != null)
            {
                memberDisabled = member.ChatAiDisabled;
                dailyLimit = member.ChatAiDailyLimitOverride ?? dailyLimitSetting;
                usedToday = await _db.ChatMessages.AsNoTracking()
                    .Where(m => m.MessageType == ChatMessageType.Text
                        && m.SenderType == ChatSenderType.Operator
                        && m.SenderId == "پاسخ خودکار"
                        && m.CreatedAt >= tehranTodayUtc
                        && m.Session.UserId == uid)
                    .CountAsync();
            }
        }

        int usedInSession = 0;
        if (session != null)
        {
            usedInSession = await _db.ChatMessages.AsNoTracking()
                .CountAsync(m => m.SessionId == session.Id
                    && m.SenderType == ChatSenderType.Operator
                    && m.SenderId == "پاسخ خودکار");
        }

        var remainingToday = isGuest ? (int?)null : Math.Max(0, dailyLimit - usedToday);
        var remainingInSession = Math.Max(0, sessionLimit - usedInSession);
        var assistantAvailable = assistantEnabled
            && !memberDisabled
            && (isGuest ? guestEnabled : dailyLimit > 0);

        return Ok(new
        {
            assistantEnabled,
            guestEnabled,
            isGuest,
            isLoggedIn = !isGuest,
            memberDisabled,
            dailyLimit,
            usedToday,
            remainingToday,
            sessionLimit,
            usedInSession,
            remainingInSession,
            assistantAvailable,
            registerUrl = "/auth/register",
            loginUrl = "/auth/login"
        });
    }

    [HttpGet("ai-usage")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> GetAiUsage()
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        var tehranTodayUtc = DateTime.UtcNow.AddMinutes(210).Date.AddMinutes(-210);

        var q = _db.ChatMessages.AsNoTracking()
            .Where(m => m.SenderType == ChatSenderType.Operator
                && m.SenderId == "پاسخ خودکار"
                && m.MessageType == ChatMessageType.Text);

        var total = await q.CountAsync();
        var today = await q.CountAsync(m => m.CreatedAt >= tehranTodayUtc);
        var last30 = await q.CountAsync(m => m.CreatedAt >= tehranTodayUtc.AddDays(-29));
        var guests = await q.CountAsync(m => m.Session.UserId == null);
        var sessions = await q.Select(m => m.SessionId).Distinct().CountAsync();

        var msgQ = _db.ChatMessages.AsNoTracking();
        var allToday = await msgQ.CountAsync(m => m.CreatedAt >= tehranTodayUtc);

        return Ok(new
        {
            enabled = settings?.ChatEnableAiAssistant == true,
            guestEnabled = settings?.ChatAiGuestEnabled == true,
            dailyLimit = settings?.ChatAiDailyLimit ?? 3,
            sessionLimit = settings?.ChatAiSessionLimit ?? 12,
            maxTokens = settings?.ChatAiMaxTokens ?? 450,
            temperature = settings?.ChatAiTemperature ?? 0.6,
            maxQuestionChars = settings?.ChatAiMaxQuestionChars ?? 300,
            totalAiReplies = total,
            todayAiReplies = today,
            last30AiReplies = last30,
            guestAiReplies = guests,
            memberAiReplies = total - guests,
            sessionsWithAi = sessions,
            allMessagesToday = allToday
        });
    }

    // ===== زنجیره چندسرویس‌دهنده‌ای هوش مصنوعی =====

    [HttpGet("ai-providers")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> GetAiProviders()
    {
        var providers = await _db.ChatAiProviders.AsNoTracking()
            .Include(p => p.ApiKeys).Include(p => p.Models)
            .OrderBy(p => p.Priority).ThenBy(p => p.Id)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var legacy = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();

        return Ok(new
        {
            providers = providers.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                baseUrl = p.BaseUrl,
                kind = p.Kind,
                enabled = p.Enabled,
                priority = p.Priority,
                keys = p.ApiKeys.OrderBy(k => k.Priority).ThenBy(k => k.Id).Select(k => new
                {
                    id = k.Id,
                    keyValue = k.KeyValue,
                    label = k.Label,
                    enabled = k.Enabled,
                    priority = k.Priority,
                    inCooldown = k.CooldownUntil != null && k.CooldownUntil > now,
                    cooldownUntil = k.CooldownUntil,
                    consecutiveFailures = k.ConsecutiveFailures,
                    lastError = k.LastError,
                    lastUsedAt = k.LastUsedAt,
                    successCount = k.SuccessCount,
                    failureCount = k.FailureCount
                }),
                models = p.Models.OrderBy(m => m.Priority).ThenBy(m => m.Id).Select(m => new
                {
                    id = m.Id,
                    modelId = m.ModelId,
                    label = m.Label,
                    enabled = m.Enabled,
                    priority = m.Priority,
                    inCooldown = m.CooldownUntil != null && m.CooldownUntil > now,
                    cooldownUntil = m.CooldownUntil,
                    consecutiveFailures = m.ConsecutiveFailures,
                    lastError = m.LastError,
                    lastUsedAt = m.LastUsedAt,
                    successCount = m.SuccessCount,
                    failureCount = m.FailureCount
                })
            }),
            legacy = new
            {
                apiUrl = legacy?.ChatAiApiUrl,
                apiKey = legacy?.ChatAiApiKey,
                model = legacy?.ChatAiModel
            }
        });
    }

    [HttpPut("ai-providers")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> SaveAiProviders([FromBody] List<ChatAiProviderPayload> payload)
    {
        if (payload == null) return Ok(new { success = true }); // null = تغییری نداده

        // حذف سرویس‌دهنده‌هایی که از پنل حذف شده‌اند (فقط رکوردهای جدول جدید؛ هیچ جدول دیگری دست نمی‌خورد)
        var incomingIds = payload.Where(p => p.Id > 0).Select(p => p.Id).ToHashSet();
        var existing = await _db.ChatAiProviders.ToListAsync();
        var toRemove = existing.Where(e => !incomingIds.Contains(e.Id)).ToList();
        foreach (var r in toRemove)
        {
            var keys = await _db.ChatAiApiKeys.Where(k => k.ProviderId == r.Id).ToListAsync();
            var models = await _db.ChatAiModels.Where(m => m.ProviderId == r.Id).ToListAsync();
            _db.ChatAiApiKeys.RemoveRange(keys);
            _db.ChatAiModels.RemoveRange(models);
            _db.ChatAiProviders.Remove(r);
        }

        int order = 0;
        foreach (var p in payload)
        {
            if (string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.BaseUrl)) continue;

            var entity = p.Id > 0 ? existing.FirstOrDefault(e => e.Id == p.Id) : null;
            if (entity == null)
            {
                entity = new ChatAiProvider();
                _db.ChatAiProviders.Add(entity);
            }
            entity.Name = p.Name.Trim();
            entity.BaseUrl = p.BaseUrl.Trim();
            entity.Kind = string.IsNullOrWhiteSpace(p.Kind) ? "custom" : p.Kind.Trim();
            entity.Enabled = p.Enabled;
            entity.Priority = p.Priority > 0 ? p.Priority : ++order;

            await _db.SaveChangesAsync();

            await SyncKeysAsync(entity.Id, p.Keys ?? new List<ChatAiKeyPayload>());
            await SyncModelsAsync(entity.Id, p.Models ?? new List<ChatAiModelPayload>());
        }

        await _db.SaveChangesAsync();
        OpenAiCompatibleChatService.InvalidateCandidateCache();
        return Ok(new { success = true, message = "سرویس‌دهنده‌های هوش مصنوعی ذخیره شد." });
    }

    private async Task SyncKeysAsync(int providerId, List<ChatAiKeyPayload> src)
    {
        var current = await _db.ChatAiApiKeys.Where(k => k.ProviderId == providerId).ToListAsync();
        var valid = src.Where(s => !string.IsNullOrWhiteSpace(s.KeyValue)).ToList();

        int order = 0;
        foreach (var s in valid)
        {
            order++;
            var entity = s.Id > 0 ? current.FirstOrDefault(c => c.Id == s.Id) : null;
            if (entity == null)
            {
                entity = new ChatAiApiKey { ProviderId = providerId };
                _db.ChatAiApiKeys.Add(entity);
            }
            entity.KeyValue = s.KeyValue.Trim();
            entity.Label = s.Label;
            entity.Enabled = s.Enabled;
            entity.Priority = order;
        }

        var validIds = valid.Where(s => s.Id > 0).Select(s => s.Id).ToHashSet();
        foreach (var c in current.Where(c => !validIds.Contains(c.Id)))
            _db.ChatAiApiKeys.Remove(c);

        await _db.SaveChangesAsync();
    }

    private async Task SyncModelsAsync(int providerId, List<ChatAiModelPayload> src)
    {
        var current = await _db.ChatAiModels.Where(m => m.ProviderId == providerId).ToListAsync();
        var valid = src.Where(s => !string.IsNullOrWhiteSpace(s.ModelId)).ToList();

        int order = 0;
        foreach (var s in valid)
        {
            order++;
            var entity = s.Id > 0 ? current.FirstOrDefault(c => c.Id == s.Id) : null;
            if (entity == null)
            {
                // جلوگیری از نقض یکتایی (ProviderId, ModelId)
                if (current.Any(c => c.Id != s.Id && string.Equals(c.ModelId, s.ModelId.Trim(), StringComparison.OrdinalIgnoreCase)))
                    continue;
                entity = new ChatAiModel { ProviderId = providerId };
                _db.ChatAiModels.Add(entity);
            }
            entity.ModelId = s.ModelId.Trim();
            entity.Label = s.Label;
            entity.Enabled = s.Enabled;
            entity.Priority = order;
        }

        var validIds = valid.Where(s => s.Id > 0).Select(s => s.Id).ToHashSet();
        foreach (var c in current.Where(c => !validIds.Contains(c.Id)))
            _db.ChatAiModels.Remove(c);

        await _db.SaveChangesAsync();
    }

    [HttpPost("ai-chain-test")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> TestAiChain([FromBody] List<ChatAiProviderPayload>? payload = null)
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        var maxTokens = settings?.ChatAiMaxTokens > 0 ? settings.ChatAiMaxTokens : 450;
        var temperature = settings?.ChatAiTemperature is > 0 and <= 2 ? settings.ChatAiTemperature : 0.6;

        // اگر payload فرستاده شده همون ویرایش‌نشده‌ست؛ وگرنه از DB ذخیره‌شده
        var candidates = new List<(string Provider, string KeyLabel, string Model, string BaseUrl, string ApiKey)>();
        if (payload != null && payload.Count > 0)
        {
            foreach (var p in payload.Where(x => x.Enabled && !string.IsNullOrWhiteSpace(x.BaseUrl))
                         .OrderBy(x => x.Priority))
                foreach (var k in (p.Keys ?? new()).Where(x => x.Enabled && !string.IsNullOrWhiteSpace(x.KeyValue)).OrderBy(x => x.Priority))
                    foreach (var m in (p.Models ?? new()).Where(x => x.Enabled && !string.IsNullOrWhiteSpace(x.ModelId)).OrderBy(x => x.Priority))
                        candidates.Add((p.Name, k.Label ?? MaskKey(k.KeyValue), m.ModelId, p.BaseUrl.TrimEnd('/'), k.KeyValue));
        }
        else
        {
            var svc = new OpenAiCompatibleChatService(_db, _httpClientFactory,
                HttpContext.RequestServices.GetRequiredService<ILogger<OpenAiCompatibleChatService>>());
            var chain = await svc.BuildCandidatesAsync(settings ?? new SiteSettings());
            candidates = chain.Select(c => (c.ProviderName, c.KeyId is int kid ? MaskKeyById(kid) : "کلید قدیمی", c.Model, c.BaseUrl, c.ApiKey)).ToList();
        }

        if (candidates.Count == 0)
            return Ok(new { success = true, results = Array.Empty<object>(), message = "کاندیدایی برای تست وجود ندارد." });

        var results = new System.Collections.Concurrent.ConcurrentQueue<object>();
        using var sem = new SemaphoreSlim(3);
        var tasks = candidates.Take(30).Select(async c =>
        {
            await sem.WaitAsync();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var (ok, msg) = await ProbeCandidateAsync(c.BaseUrl, c.ApiKey, c.Model, temperature, Math.Min(maxTokens, 100));
                sw.Stop();
                results.Enqueue(new { provider = c.Provider, key = c.KeyLabel, model = c.Model, ok, ms = sw.ElapsedMilliseconds, message = msg });
            }
            finally { sem.Release(); }
        });
        await Task.WhenAll(tasks);

        var ordered = results.OrderBy(r => r.GetType().GetProperty("provider")!.GetValue(r)?.ToString())
            .ThenBy(r => r.GetType().GetProperty("model")!.GetValue(r)?.ToString());
        var list = ordered.ToList();
        var okCount = list.Count(r => (bool)r.GetType().GetProperty("ok")!.GetValue(r)!);
        return Ok(new
        {
            success = true,
            results = list,
            total = list.Count,
            healthy = okCount,
            message = okCount > 0
                ? $"{okCount} از {list.Count} کاندید سالم است."
                : "هیچ کاندیدایی پاسخ نداد — کلیدها و نام مدل‌ها را بررسی کنید."
        });
    }

    private string MaskKeyById(int keyId)
    {
        var k = _db.ChatAiApiKeys.AsNoTracking().FirstOrDefault(x => x.Id == keyId);
        return k == null ? "کلید" : MaskKey(k.KeyValue);
    }

    private static string MaskKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "کلید";
        return key.Length <= 8 ? "****" : $"{key[..4]}…{key[^4..]}";
    }

    private async Task<(bool Ok, string Message)> ProbeCandidateAsync(string baseUrl, string apiKey, string model,
        double temperature, int maxTokens)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions");
            if (!string.IsNullOrWhiteSpace(apiKey))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
            var payload = new
            {
                model,
                messages = new object[]
                {
                    new { role = "system", content = "پاسخ کوتاه فارسی بده." },
                    new { role = "user", content = "سلام" }
                },
                temperature,
                max_tokens = maxTokens
            };
            request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var response = await client.SendAsync(request, cts.Token);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return (false, ExtractApiError(body, (int)response.StatusCode));

            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return (false, "پاسخ خالی برگرداند.");
            var content = choices[0].GetProperty("message").GetProperty("content").GetString();
            return string.IsNullOrWhiteSpace(content) ? (false, "محتوای خالی") : (true, "سالم");
        }
        catch (OperationCanceledException) { return (false, "بیش از ۲۰ ثانیه طول کشید (کند)."); }
        catch (Exception ex) { return (false, ex.Message.Length > 120 ? ex.Message[..120] + "…" : ex.Message); }
    }

    [HttpGet("ai-models/catalog")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> GetAiModelCatalog([FromQuery] string kind = "openrouter", [FromQuery] string? baseUrl = null, [FromQuery] string? apiKey = null)
    {
        try
        {
            if (string.Equals(kind, "openrouter", StringComparison.OrdinalIgnoreCase))
            {
                var models = await FetchOpenRouterFreeModelsAsync();
                if (models.Count > 0)
                    return Ok(new { success = true, source = "openrouter", models });
                return Ok(new { success = false, source = "openrouter", models = StaticFreeModels, message = "دریافت زنده ناموفق بود؛ از فهرست ثابت استفاده کنید." });
            }

            // سرویس‌دهنده سازگار با OpenAI: تلاش برای GET {baseUrl}/models
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                var remote = await FetchOpenAiCompatibleModelsAsync(baseUrl!, apiKey);
                if (remote.Count > 0) return Ok(new { success = true, source = "endpoint", models = remote });
            }
            return Ok(new { success = true, source = "static", models = StaticFreeModels, message = "فهرست سرویس‌دهنده دریافت نشد؛ فهرست ثابت نمایش داده شد." });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChatController: model catalog fetch failed");
            return Ok(new { success = false, source = "static", models = StaticFreeModels, message = $"خطا در دریافت فهرست: {ex.Message}" });
        }
    }

    private async Task<List<object>> FetchOpenRouterFreeModelsAsync()
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        var body = await client.GetStringAsync("https://openrouter.ai/api/v1/models");
        var list = new List<object>();
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != System.Text.Json.JsonValueKind.Array)
            return list;

        foreach (var m in data.EnumerateArray())
        {
            var id = m.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id)) continue;

            var isFree = id.EndsWith(":free", StringComparison.OrdinalIgnoreCase)
                || IsZeroPrice(m, "prompt") && IsZeroPrice(m, "completion");
            if (!isFree) continue;

            var name = m.TryGetProperty("name", out var nEl) ? nEl.GetString() : id;
            var ctx = m.TryGetProperty("context_length", out var cEl) && cEl.ValueKind == System.Text.Json.JsonValueKind.Number
                ? cEl.GetInt32() : (int?)null;
            list.Add(new { id, name, contextLength = ctx });
        }
        return list.OrderBy(x => (string)x.GetType().GetProperty("id")!.GetValue(x)!).ToList();

        static bool IsZeroPrice(System.Text.Json.JsonElement m, string prop)
            => m.TryGetProperty("pricing", out var p)
               && p.TryGetProperty(prop, out var v)
               && v.ValueKind == System.Text.Json.JsonValueKind.String
               && (v.GetString() == "0" || v.GetString() == "0.000000");
    }

    private async Task<List<object>> FetchOpenAiCompatibleModelsAsync(string baseUrl, string? apiKey)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/models");
            if (!string.IsNullOrWhiteSpace(apiKey))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) return new();
            using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != System.Text.Json.JsonValueKind.Array)
                return new();

            var list = new List<object>();
            foreach (var m in data.EnumerateArray())
            {
                var id = m.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(id)) continue;
                list.Add(new { id, name = id, contextLength = (int?)null });
            }
            return list;
        }
        catch { return new(); }
    }

    // فهرست ثابت مدل‌های رایگان (fallback وقتی دریافت زنده ممکن نیست)
    private static readonly object[] StaticFreeModels =
    {
        new { id = "liquid/lfm-2.5-2.6b:free", name = "Liquid LFM 2.5 2.6B", contextLength = (int?)null },
        new { id = "nvidia/nemotron-3-ultra-550b-a55b:free", name = "Nemotron 3 Ultra 550B", contextLength = (int?)null },
        new { id = "nvidia/nemotron-3-super-120b-a12b:free", name = "Nemotron 3 Super 120B", contextLength = (int?)null },
        new { id = "nvidia/nemotron-3.5-lightning:free", name = "Nemotron 3.5 Lightning", contextLength = (int?)null }
    };

    [HttpGet("ai-credits")]
    [Authorize(Policy = "perm:admin.chat.manage")]
    public async Task<IActionResult> GetAiCredits()
    {
        var settings = await _db.SiteSettings.FirstOrDefaultAsync();
        var apiUrl = settings?.ChatAiApiUrl?.Trim().TrimEnd('/') ?? "";
        var key = settings?.ChatAiApiKey?.Trim() ?? "";

        if (apiUrl.Length == 0 || key.Length == 0)
            return Ok(new { supported = false, ok = false, message = "آدرس API یا کلید API هنوز ثبت نشده است." });

        if (!apiUrl.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase))
            return Ok(new { supported = false, ok = false, message = "استعلام اعتبار به‌صورت خودکار فقط برای OpenRouter در دسترس است؛ برای ارائه‌دهنده دیگر از پنل خودش استعلام بگیرید." });

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);

            var resp = await client.GetAsync("https://openrouter.ai/api/v1/credits");
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return Ok(new { supported = true, ok = false, message = $"سرویس OpenRouter کد {(int)resp.StatusCode} برگرداند." });

            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return Ok(new { supported = true, ok = false, message = "پاسخ نامعتبر از OpenRouter." });

            var total = data.TryGetProperty("total_credits", out var tc) ? tc.GetDouble() : 0;
            var usage = data.TryGetProperty("total_usage", out var tu) ? tu.GetDouble() : 0;
            double? limit = data.TryGetProperty("limit", out var lm) && lm.ValueKind == System.Text.Json.JsonValueKind.Number
                ? lm.GetDouble()
                : (double?)null;

            return Ok(new
            {
                supported = true,
                ok = true,
                source = "OpenRouter",
                totalCredits = Math.Round(total, 4),
                totalUsage = Math.Round(usage, 4),
                remaining = Math.Round(total - usage, 4),
                limit,
                currency = "USD",
                fetchedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Ok(new { supported = true, ok = false, message = $"خطا در استعلام: {ex.Message}" });
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
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(0, skip);
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

            if (req.MessageType == ChatMessageTypeDto.Text)
            {
                var sessionId = session.Id;
                var text = req.Content ?? "";
                _ = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var responder = scope.ServiceProvider.GetRequiredService<ChatAiResponder>();
                    await responder.TryReplyAsync(sessionId, text);
                });
            }
        }
        else
        {
            _aiResponder.BlockSession(session.Id);
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
        take = Math.Clamp(take, 1, 200);
        skip = Math.Max(0, skip);
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

        var previousOperatorId = session.OperatorId;
        session.OperatorId = req.OperatorId;

        if (req.OperatorId != 0 && req.OperatorId != previousOperatorId)
        {
            var visitorName = string.IsNullOrWhiteSpace(session.UserName) ? "مشتری" : session.UserName;
            await _notificationEvents.NotifyAsync(
                eventType: "chat.session_transferred",
                values: new Dictionary<string, string?>
                {
                    ["UserName"] = visitorName,
                    ["SessionId"] = session.Id.ToString()
                },
                idempotencyKey: $"chat.transferred:{session.Id}:{previousOperatorId ?? 0}:{req.OperatorId}:{DateTimeOffset.UtcNow:yyyyMMddHH}",
                fallbackSeverity: NotificationSeverity.Info,
                fallbackTitle: "گفتگو به شما منتقل شد",
                fallbackBody: $"گفتگوی #{session.Id} با {visitorName} به شما منتقل شد.",
                fallbackUrl: "/admin/chat",
                appUserId: req.OperatorId);
        }

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
            EnableBaleNotifications = settings.EnableBaleNotifications,
            EnableTelegramNotifications = settings.EnableTelegramNotifications,
            EnableEitaaNotifications = settings.EnableEitaaNotifications,
            BaleSafirApiKey = settings.BaleSafirApiKey,
            WelcomeMessage = settings.ChatWelcomeMessage,
            EnableAutoMessage = settings.ChatEnableAutoMessage,
            AutoMessageSeconds = settings.ChatAutoMessageSeconds,
            PhoneRequired = settings.ChatPhoneRequired,
            EnableAiAssistant = settings.ChatEnableAiAssistant,
            AiProvider = settings.ChatAiProvider,
            AiApiUrl = settings.ChatAiApiUrl,
            AiModel = settings.ChatAiModel,
            AiApiKey = settings.ChatAiApiKey,
            AiSystemPrompt = settings.ChatAiSystemPrompt,
            AiSuggestedTopics = settings.ChatSuggestedTopics,
            AiGuestEnabled = settings.ChatAiGuestEnabled,
            AiDailyLimit = settings.ChatAiDailyLimit,
            AiMaxQuestionChars = settings.ChatAiMaxQuestionChars > 0 ? settings.ChatAiMaxQuestionChars : 300,
            AiMaxTokens = settings.ChatAiMaxTokens > 0 ? settings.ChatAiMaxTokens : 450,
            AiTemperature = settings.ChatAiTemperature,
            AiSessionLimit = settings.ChatAiSessionLimit > 0 ? settings.ChatAiSessionLimit : 12,
            AiBlockKeywords = settings.ChatAiBlockKeywords
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
        settings.EnableBaleNotifications = p.EnableBaleNotifications;
        settings.EnableTelegramNotifications = p.EnableTelegramNotifications;
        settings.EnableEitaaNotifications = p.EnableEitaaNotifications;
        settings.BaleSafirApiKey = string.IsNullOrWhiteSpace(p.BaleSafirApiKey) ? null : p.BaleSafirApiKey.Trim();
        settings.ChatWelcomeMessage = p.WelcomeMessage;
        settings.ChatEnableAutoMessage = p.EnableAutoMessage;
        settings.ChatAutoMessageSeconds = p.AutoMessageSeconds > 0 ? p.AutoMessageSeconds : 60;
        settings.ChatPhoneRequired = p.PhoneRequired;
        settings.ChatEnableAiAssistant = p.EnableAiAssistant;
        settings.ChatAiApiUrl = string.IsNullOrWhiteSpace(p.AiApiUrl) ? null : p.AiApiUrl.Trim();
        settings.ChatAiProvider = string.IsNullOrWhiteSpace(p.AiProvider)
            ? AiProviderDetector.Detect(settings.ChatAiApiUrl)
            : p.AiProvider!.Trim();
        settings.ChatAiModel = p.AiModel;
        settings.ChatAiApiKey = string.IsNullOrWhiteSpace(p.AiApiKey) ? null : p.AiApiKey.Trim();
        settings.ChatAiSystemPrompt = p.AiSystemPrompt;
        settings.ChatSuggestedTopics = string.IsNullOrWhiteSpace(p.AiSuggestedTopics) ? null : p.AiSuggestedTopics.Trim();
        settings.ChatAiGuestEnabled = p.AiGuestEnabled;
        settings.ChatAiDailyLimit = p.AiDailyLimit < 0 ? 0 : p.AiDailyLimit;
        settings.ChatAiMaxQuestionChars = p.AiMaxQuestionChars < 50 ? 50 : Math.Min(p.AiMaxQuestionChars, 4000);
        settings.ChatAiMaxTokens = p.AiMaxTokens < 50 ? 50 : Math.Min(p.AiMaxTokens, 4000);
        settings.ChatAiTemperature = p.AiTemperature < 0 ? 0 : Math.Min(p.AiTemperature, 2);
        settings.ChatAiSessionLimit = p.AiSessionLimit < 0 ? 0 : Math.Min(p.AiSessionLimit, 500);
        settings.ChatAiBlockKeywords = string.IsNullOrWhiteSpace(p.AiBlockKeywords) ? null : p.AiBlockKeywords.Trim();
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

        if (session.OperatorId.HasValue && session.Rating.HasValue)
        {
            var visitorName = string.IsNullOrWhiteSpace(session.UserName) ? "مشتری" : session.UserName;
            await _notificationEvents.NotifyAsync(
                eventType: "chat.session_rated",
                values: new Dictionary<string, string?>
                {
                    ["UserName"] = visitorName,
                    ["SessionId"] = session.Id.ToString(),
                    ["Rating"] = session.Rating.Value.ToString()
                },
                idempotencyKey: $"chat.rated:{session.Id}",
                fallbackSeverity: NotificationSeverity.Info,
                fallbackTitle: "امتیاز جدید برای گفتگو",
                fallbackBody: $"مشتری {visitorName} به گفتگوی #{session.Id} امتیاز {session.Rating} از ۵ داد.",
                fallbackUrl: "/admin/chat",
                appUserId: session.OperatorId);
        }

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

public class AiTestRequest
{
    public string? Model { get; set; }
    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
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
    public bool EnableBaleNotifications { get; set; } = true;
    public bool EnableTelegramNotifications { get; set; } = true;
    public bool EnableEitaaNotifications { get; set; } = true;
    public string? BaleSafirApiKey { get; set; }
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
    public string? AiSuggestedTopics { get; set; }
    public bool AiGuestEnabled { get; set; }
    public int AiDailyLimit { get; set; } = 3;
    public int AiMaxQuestionChars { get; set; } = 300;
    public int AiMaxTokens { get; set; } = 450;
    public double AiTemperature { get; set; } = 0.6;
    public int AiSessionLimit { get; set; } = 12;
    public string? AiBlockKeywords { get; set; }
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

internal static class AiProviderDetector
{
    public static string? Detect(string? apiUrl)
    {
        if (string.IsNullOrWhiteSpace(apiUrl)) return null;
        var u = apiUrl.ToLowerInvariant();
        if (u.Contains("openrouter")) return "openrouter";
        if (u.Contains("api.openai.com")) return "openai";
        if (u.Contains("anthropic")) return "anthropic";
        if (u.Contains("mistral")) return "mistral";
        if (u.Contains("deepseek")) return "deepseek";
        if (u.Contains("groq")) return "groq";
        if (u.Contains("together")) return "together";
        if (u.Contains("gemini") || u.Contains("googleapis")) return "google";
        if (u.Contains(".ir/") || u.EndsWith(".ir")) return "سرویس ایرانی";
        return null;
    }
}
