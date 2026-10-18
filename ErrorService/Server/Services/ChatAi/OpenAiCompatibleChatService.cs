using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace ErrorService.Server.Services.ChatAi;

public sealed class OpenAiCompatibleChatService : IChatAiService
{
    private readonly ErrorServiceDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAiCompatibleChatService> _logger;

    public OpenAiCompatibleChatService(ErrorServiceDbContext db, IHttpClientFactory httpClientFactory, ILogger<OpenAiCompatibleChatService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => "دستیار هوشمند (سرویس API)";

    // «خودکار»: از مدل سریع و تمیز شروع می‌کند و در صورت شکست/محدودیت به مدل بعدی می‌رود.
    // (فقط برای پیکربندی قدیمی تک‌کلیدی استفاده می‌شود؛ زنجیره جدید از جدول ChatAiProviders می‌آید.)
    // مدلی که کول‌داون بگیرد رد می‌شود؛ اگر کل لیست خطا بدهد پاسخی برنمی‌گردد، پس چند گزینه سالم نگه می‌داریم.
    public static readonly string[] AutoModels =
    {
        "liquid/lfm-2.5-2.6b:free",
        "nvidia/nemotron-3-super-120b-a12b:free",
        "deepseek/deepseek-chat-v3-0324:free",
        "nvidia/nemotron-3-ultra-550b-a55b:free"
    };

    private static readonly TimeSpan PerAttemptTimeout = TimeSpan.FromSeconds(25);

    // سقف کلی زنجیره: کاربر فقط typing indicator می‌بیند؛ بعد از این سقف پاسخی برنمی‌گردد.
    private static readonly TimeSpan TotalChainBudget = TimeSpan.FromSeconds(60);
    private const int MaxChainAttempts = 8;

    // اگر مدل به خاطر سقف توکن وسط جمله قطع کرد (finish_reason=length)، حداکثر این‌قدر بار ادامه می‌گیریم.
    private const int MaxContinuations = 2;
    private const string ContinuationPrompt = "جمله ناتمام ماند. لطفاً دقیقاً از همان‌جا که متن قبلی تمام شد، بدون تکرار و بدون مقدمه‌چینی ادامه بده.";

    // کش کاندیدها (بارگذاری per-pیام لازم نیست)
    private static volatile List<ChainCandidate>? _candidateCache;
    private static long _candidateCacheAtTicks;
    private static readonly TimeSpan CandidateCacheTtl = TimeSpan.FromSeconds(30);

    public sealed record ChainCandidate(int? KeyId, int? ModelId, string BaseUrl, string ApiKey, string Model, string ProviderName);

    private enum FailKind
    {
        Transient,    // timeout / خطای شبکه / 5xx → کول‌داون کوتاه کلید
        Auth,         // 401/403 کلید → کلید نامعتبر/رد صلاحیت
        NoCredit,     // 402 → شارژ تمام
        RateLimited,  // 429 → سقف نرخ/روزانه
        Model,        // 404/400 مدل → مدل ناموجود/نامعتبر
        ModelAccess,  // 403 دسترسی مدل (مثل «Access denied by security policy») → فقط همان مدل کول‌داون شود نه کلید
        Empty         // پاسخ خالی
    }

    public async Task<string?> GetReplyAsync(string userMessage, string operatorName, IReadOnlyList<ChatAiTurn>? history = null)
    {
        try
        {
            var settings = await _db.SiteSettings.FirstOrDefaultAsync();
            if (settings?.ChatEnableAiAssistant != true) return null;

            var candidates = await GetCandidatesAsync(settings);
            if (candidates.Count == 0) return null;

            var systemPrompt = !string.IsNullOrWhiteSpace(settings.ChatAiSystemPrompt)
                ? settings.ChatAiSystemPrompt
                : "تو دستیار پشتیبانی رسمی سایت «ارورسرویس» هستی. فقط و فقط در این چارچوب پاسخ بده: کدهای خطا و عیب‌یابی دستگاه‌ها، خدمات تعمیرات، محصولات و سفارش فروشگاه، راهنمای خرید و پیگیری سفارش. به هیچ سوال خارج از این حوزه پاسخ نده و مؤدبانه بگو که این موضوع خارج از حوزه پشتیبانی است. هرگز قیمت قطعی یا زمان تحویل تضمینی اعلام نکن. لحن: فارسی، کوتاه، مؤدبانه.";

            var messages = new List<object> { new { role = "system", content = systemPrompt } };
            if (history != null)
            {
                foreach (var turn in history.TakeLast(10))
                {
                    if (string.IsNullOrWhiteSpace(turn.Content)) continue;
                    messages.Add(new { role = turn.Role, content = turn.Content });
                }
            }
            messages.Add(new { role = "user", content = userMessage });

            var temperature = settings.ChatAiTemperature is > 0 and <= 2 ? settings.ChatAiTemperature : 0.6;
            var maxTokens = settings.ChatAiMaxTokens > 0 ? settings.ChatAiMaxTokens : 450;

            var deadline = DateTime.UtcNow + TotalChainBudget;
            var attempts = 0;

            foreach (var candidate in candidates)
            {
                if (attempts >= MaxChainAttempts) break;
                var remaining = deadline - DateTime.UtcNow;
                if (remaining < TimeSpan.FromSeconds(5)) break;
                attempts++;

                var reply = await TryCandidateAsync(candidate, messages, temperature, maxTokens, deadline);
                if (!string.IsNullOrWhiteSpace(reply))
                {
                    await RecordSuccessAsync(candidate);
                    _logger.LogInformation("ChatAi chain: reply from {Provider} / {Model} (attempt {Attempt}/{Max})",
                        candidate.ProviderName, candidate.Model, attempts, MaxChainAttempts);
                    return reply;
                }
            }

            _logger.LogWarning("ChatAi chain: no reply after {Attempts} attempts within budget", attempts);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAiCompatibleChatService: chain failed");
            return null;
        }
    }

    /// <summary>
    /// تلاش روی یک کاندید + ادامه‌گیری از finish_reason=length. در صورت شکست، وضعیت کلید/مدل در DB به‌روز می‌شود.
    /// </summary>
    private async Task<string?> TryCandidateAsync(ChainCandidate candidate, List<object> messages,
        double temperature, int maxTokens, DateTime deadline)
    {
        var parts = new List<string>();
        var turns = new List<object>(messages);

        for (var cont = 0; cont <= MaxContinuations; cont++)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining < TimeSpan.FromSeconds(5)) break;

            var attemptTimeout = TimeSpan.FromSeconds(Math.Clamp(remaining.TotalSeconds, 5, PerAttemptTimeout.TotalSeconds));
            var (content, truncated, failKind, failDetail) = await SendOnceAsync(candidate, turns, temperature, maxTokens, attemptTimeout);

            if (failKind != null)
            {
                await RecordFailureAsync(candidate, failKind.Value, failDetail);
                return null; // کاندید بعدی
            }

            if (string.IsNullOrWhiteSpace(content)) break;

            parts.Add(content);
            if (!truncated || cont == MaxContinuations) break;

            turns.Add(new { role = "assistant", content = string.Concat(parts) });
            turns.Add(new { role = "user", content = ContinuationPrompt });
        }

        if (parts.Count == 0) return null;
        var reply = string.Concat(parts).Trim();
        if (string.IsNullOrWhiteSpace(reply)) return null;

        // پاسخ خالی/فقط فاصله: به‌عنوان شکست مدل ثبت و کاندید بعدی امتحان شود.
        if (reply.Length < 2)
        {
            await RecordFailureAsync(candidate, FailKind.Empty, "پاسخ خالی");
            return null;
        }
        return reply;
    }

    private async Task<(string? Content, bool Truncated, FailKind? FailKind, string? FailDetail)> SendOnceAsync(
        ChainCandidate candidate, List<object> messages, double temperature, int maxTokens, TimeSpan timeout)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{candidate.BaseUrl.TrimEnd('/')}/chat/completions");
            if (!string.IsNullOrWhiteSpace(candidate.ApiKey))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", candidate.ApiKey.Trim());

            var payload = new
            {
                model = candidate.Model,
                messages,
                temperature,
                max_tokens = maxTokens
            };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(timeout);
            var response = await client.SendAsync(request, cts.Token);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                var detail = $"HTTP {status}" + ExtractProviderMessage(body);
                _logger.LogWarning("ChatAi chain: {Provider}/{Model} failed: {Detail}", candidate.ProviderName, candidate.Model, detail);
                return (null, false, ClassifyStatus(status, body), TruncateDetail(detail));
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return (null, false, FailKind.Empty, "بدون choices در پاسخ");

            var choice = choices[0];
            var content = choice.GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content)) return (null, false, FailKind.Empty, "محتوای خالی");

            var truncated = choice.TryGetProperty("finish_reason", out var fr)
                && fr.ValueKind == JsonValueKind.String
                && string.Equals(fr.GetString(), "length", StringComparison.OrdinalIgnoreCase);

            return (content.Trim(), truncated, null, null);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ChatAi chain: {Provider}/{Model} timed out", candidate.ProviderName, candidate.Model);
            return (null, false, FailKind.Transient, "timeout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChatAi chain: {Provider}/{Model} error", candidate.ProviderName, candidate.Model);
            return (null, false, FailKind.Transient, TruncateDetail(ex.Message));
        }
    }

    // ===== طبقه‌بندی خطا =====

    private static FailKind ClassifyStatus(int status, string body) => status switch
    {
        401 => FailKind.Auth,
        // 403 می‌تواند یعنی کلید رد صلاحیت شده، یا فقط دسترسی به همین مدل بسته است
        // (مثل «Access denied by security policy» روی مدل‌های free)؛ در حالت دوم کلید سالم است.
        403 => LooksLikeModelIssue(body) ? FailKind.ModelAccess : FailKind.Auth,
        402 => FailKind.NoCredit,
        429 => FailKind.RateLimited,
        404 or 400 when LooksLikeModelIssue(body) => FailKind.Model,
        404 => FailKind.Model,
        _ => FailKind.Transient
    };

    private static bool LooksLikeModelIssue(string body)
    {
        if (string.IsNullOrEmpty(body)) return false;
        return body.Contains("model", StringComparison.OrdinalIgnoreCase)
            || body.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || body.Contains("access denied", StringComparison.OrdinalIgnoreCase)
            || body.Contains("security policy", StringComparison.OrdinalIgnoreCase)
            || body.Contains("permission", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractProviderMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("error", out var err)) return null;
            var msg = err.ValueKind == JsonValueKind.Object && err.TryGetProperty("message", out var em)
                ? em.GetString()
                : err.ValueKind == JsonValueKind.String ? err.GetString() : null;
            return string.IsNullOrWhiteSpace(msg) ? null : " — " + TruncateDetail(msg);
        }
        catch { return null; }
    }

    private static string TruncateDetail(string? s)
        => string.IsNullOrEmpty(s) ? "" : (s.Length > 300 ? s[..300] + "…" : s);

    // ===== کول‌داون‌ها و آمار (برای سویچ خودکار + نمایش در پنل ادمین) =====

    private static (TimeSpan? KeyCooldown, TimeSpan? ModelCooldown) CooldownsFor(FailKind kind) => kind switch
    {
        FailKind.Auth => (TimeSpan.FromMinutes(30), null),
        FailKind.NoCredit => (TimeSpan.FromMinutes(60), null),
        FailKind.RateLimited => (TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)),
        FailKind.Model => (null, TimeSpan.FromMinutes(360)),
        // فقط همین مدل بسته است؛ کلید برای بقیه مدل‌ها سالم می‌ماند.
        FailKind.ModelAccess => (null, TimeSpan.FromMinutes(60)),
        FailKind.Empty => (null, TimeSpan.FromMinutes(10)),
        _ => (TimeSpan.FromMinutes(2), null) // Transient
    };

    private async Task RecordFailureAsync(ChainCandidate candidate, FailKind kind, string? detail)
    {
        try
        {
            var now = DateTime.UtcNow;
            var (keyCd, modelCd) = CooldownsFor(kind);

            if (candidate.KeyId is int keyId)
            {
                var key = await _db.ChatAiApiKeys.FindAsync(keyId);
                if (key != null)
                {
                    key.ConsecutiveFailures++;
                    key.FailureCount++;
                    key.LastError = TruncateDetail(detail);
                    if (keyCd.HasValue) key.CooldownUntil = now + keyCd.Value;
                    await _db.SaveChangesAsync();
                }
            }

            if (candidate.ModelId is int modelId)
            {
                var model = await _db.ChatAiModels.FindAsync(modelId);
                if (model != null)
                {
                    model.ConsecutiveFailures++;
                    model.FailureCount++;
                    model.LastError = TruncateDetail(detail);
                    if (modelCd.HasValue) model.CooldownUntil = now + modelCd.Value;
                    await _db.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChatAi chain: failed to record failure for {Provider}/{Model}", candidate.ProviderName, candidate.Model);
        }
    }

    private async Task RecordSuccessAsync(ChainCandidate candidate)
    {
        try
        {
            var now = DateTime.UtcNow;

            if (candidate.KeyId is int keyId)
            {
                var key = await _db.ChatAiApiKeys.FindAsync(keyId);
                if (key != null)
                {
                    key.ConsecutiveFailures = 0;
                    key.CooldownUntil = null;
                    key.LastError = null;
                    key.LastUsedAt = now;
                    key.SuccessCount++;
                    await _db.SaveChangesAsync();
                }
            }

            if (candidate.ModelId is int modelId)
            {
                var model = await _db.ChatAiModels.FindAsync(modelId);
                if (model != null)
                {
                    model.ConsecutiveFailures = 0;
                    model.CooldownUntil = null;
                    model.LastError = null;
                    model.LastUsedAt = now;
                    model.SuccessCount++;
                    await _db.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChatAi chain: failed to record success for {Provider}/{Model}", candidate.ProviderName, candidate.Model);
        }
    }

    // ===== ساخت زنجیره کاندیدها: Provider ← Key ← Model =====

    private async Task<List<ChainCandidate>> GetCandidatesAsync(SiteSettings settings)
    {
        var cached = _candidateCache;
        if (cached != null && DateTime.UtcNow.Ticks - Interlocked.Read(ref _candidateCacheAtTicks) < CandidateCacheTtl.Ticks)
            return cached;

        var list = await BuildCandidatesAsync(settings);
        _candidateCache = list;
        Interlocked.Exchange(ref _candidateCacheAtTicks, DateTime.UtcNow.Ticks);
        return list;
    }

    /// <summary>بیلد زنجیره از جداول جدید؛ اگر سرویس‌دهنده‌ای ثبت نشده از پیکربندی قدیمی fallback می‌گیرد.</summary>
    public async Task<List<ChainCandidate>> BuildCandidatesAsync(SiteSettings settings)
    {
        var now = DateTime.UtcNow;
        var providers = await _db.ChatAiProviders.AsNoTracking()
            .Include(p => p.ApiKeys)
            .Include(p => p.Models)
            .Where(p => p.Enabled)
            .OrderBy(p => p.Priority).ThenBy(p => p.Id)
            .ToListAsync();

        var list = new List<ChainCandidate>();
        foreach (var p in providers)
        {
            if (string.IsNullOrWhiteSpace(p.BaseUrl)) continue;
            var keys = p.ApiKeys
                .Where(k => k.Enabled && !string.IsNullOrWhiteSpace(k.KeyValue)
                    && (k.CooldownUntil == null || k.CooldownUntil < now))
                .OrderBy(k => k.Priority).ThenBy(k => k.Id);
            var models = p.Models
                .Where(m => m.Enabled && !string.IsNullOrWhiteSpace(m.ModelId)
                    && (m.CooldownUntil == null || m.CooldownUntil < now))
                .OrderBy(m => m.Priority).ThenBy(m => m.Id)
                .ToList();

            foreach (var k in keys)
                foreach (var m in models)
                    list.Add(new ChainCandidate(k.Id, m.Id, p.BaseUrl.TrimEnd('/'), k.KeyValue, m.ModelId, p.Name));
        }

        if (list.Count > 0) return list;

        // fallback: پیکربندی قدیمی تک‌کلیدی (هیچ‌چیز برای ادمین تغییر نکرده باشد هم کار می‌کند)
        if (!string.IsNullOrWhiteSpace(settings.ChatAiApiUrl) && !string.IsNullOrWhiteSpace(settings.ChatAiModel))
        {
            var baseUrl = settings.ChatAiApiUrl.TrimEnd('/');
            var apiKey = settings.ChatAiApiKey ?? "";
            var models = string.Equals(settings.ChatAiModel, "auto", StringComparison.OrdinalIgnoreCase)
                ? AutoModels
                : new[] { settings.ChatAiModel! };
            foreach (var m in models)
                list.Add(new ChainCandidate(null, null, baseUrl, apiKey, m, "پیکربندی قدیمی"));
        }

        return list;
    }

    public static void InvalidateCandidateCache() => Interlocked.Exchange(ref _candidateCacheAtTicks, 0);
}
