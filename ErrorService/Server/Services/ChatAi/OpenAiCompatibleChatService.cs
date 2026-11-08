using ErrorService.Server.Data;
using ErrorService.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Net;
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
        "nvidia/nemotron-3.5-lightning:free",
        "google/gemma-4-31b-it:free",
        "nvidia/nemotron-3-ultra-550b-a55b:free",
        "thinkingmachines/inkling:free",
        "liquid/lfm-2.5-2.6b:free"
    };

    private static readonly TimeSpan PerAttemptTimeout = TimeSpan.FromSeconds(25);

    // تولید مقاله طولانی‌تر و کندتر از پاسخ چت است؛ مدل‌های رایگان گاهی بیش از ۲۵ ثانیه طول می‌کشند.
    private static readonly TimeSpan ContentAttemptTimeout = TimeSpan.FromSeconds(75);
    private static readonly TimeSpan ContentTotalBudget = TimeSpan.FromMinutes(5);

    // سقف کلی زنجیره: کاربر فقط typing indicator می‌بیند؛ بعد از این سقف پاسخی برنمی‌گردد.
    private static readonly TimeSpan TotalChainBudget = TimeSpan.FromSeconds(60);
    private const int MaxChainAttempts = 8;

    // اگر مدل به خاطر سقف توکن وسط جمله قطع کرد (finish_reason=length)، حداکثر این‌قدر بار ادامه می‌گیریم.
    private const int MaxContinuations = 2;
    private const string ContinuationPrompt = "جمله ناتمام ماند. لطفاً دقیقاً از همان‌جا که متن قبلی تمام شد، بدون تکرار و بدون مقدمه‌چینی ادامه بده.";

    // کش کاندیدها بر اساس کاربرد (چت / استودیوی محتوا)
    private static volatile List<ChainCandidate>? _chatCandidateCache;
    private static long _chatCandidateCacheAtTicks;
    private static volatile List<ChainCandidate>? _studioCandidateCache;
    private static long _studioCandidateCacheAtTicks;
    private static readonly TimeSpan CandidateCacheTtl = TimeSpan.FromSeconds(30);

    public sealed record ProxyOption(int Id, string Url, string? Label);

    // Proxies خالی یعنی اتصال مستقیم؛ در غیر این صورت به ترتیب امتحان می‌شوند.
    public sealed record ChainCandidate(int? KeyId, int? ModelId, string BaseUrl, string ApiKey, string Model, string ProviderName, IReadOnlyList<ProxyOption>? Proxies = null);

    // کلاینت‌های پروکسی‌دار به ازای هر آدرس (تعداد کم؛ برای عمر برنامه نگه داشته می‌شوند).
    private static readonly ConcurrentDictionary<string, HttpClient> ProxyClients = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// کلاینت HTTP با پروکسی اختیاری. بعضی ارائه‌دهنده‌ها (مثل OpenRouter) IP سرور را مسدود می‌کنند؛
    /// با تنظیم پروکسی، درخواست از خروجی تمیز ارسال می‌شود.
    /// </summary>
    public static HttpClient CreateHttpClient(IHttpClientFactory factory, string? proxyUrl)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl)) return factory.CreateClient();
        var key = proxyUrl.Trim();
        return ProxyClients.GetOrAdd(key, url =>
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return factory.CreateClient();

            var proxy = new WebProxy(uri);
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                proxy.Credentials = new NetworkCredential(
                    Uri.UnescapeDataString(parts[0]),
                    parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty);
            }

            var handler = new SocketsHttpHandler
            {
                Proxy = proxy,
                UseProxy = true,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10)
            };
            return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        });
    }

    public sealed record AiGenerationResult(string? Content, string? Error);

    private enum FailKind
    {
        Transient,    // timeout / خطای شبکه / 5xx → کول‌داون کوتاه کلید
        Auth,         // 401/403 کلید → کلید نامعتبر/رد صلاحیت
        NoCredit,     // 402 → شارژ تمام
        RateLimited,  // 429 → سقف نرخ/روزانه
        Model,        // 404/400 مدل → مدل ناموجود/نامعتبر
        ModelAccess,  // 403 دسترسی مدل → فقط همان مدل کول‌داون شود نه کلید
        IpBlocked,    // 403 «Access denied by security policy» → مسدودسازی IP در لبه Cloudflare (نه کلید/مدل)
        Empty         // پاسخ خالی
    }

    public async Task<string?> GetReplyAsync(string userMessage, string operatorName, IReadOnlyList<ChatAiTurn>? history = null)
    {
        try
        {
            var settings = await _db.SiteSettings.FirstOrDefaultAsync();
            if (settings?.ChatEnableAiAssistant != true) return null;

            var candidates = await GetCandidatesAsync(settings, AiUsageScope.Chat);
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

                var (reply, _) = await TryCandidateAsync(candidate, messages, temperature, maxTokens, PerAttemptTimeout, deadline);
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
    /// تولید متن آزاد (مثلاً بازنویسی خبر) با همان زنجیره Provider/Key/Model چت.
    /// برخلاف GetReplyAsync به تنظیم ChatEnableAiAssistant وابسته نیست؛ گیت آن در استودیوی محتواست.
    /// </summary>
    public async Task<AiGenerationResult> GenerateAsync(string systemPrompt, string userPrompt,
        double temperature, int maxTokens, CancellationToken ct = default,
        TimeSpan? budget = null, int maxAttempts = 4)
    {
        try
        {
            var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            if (settings is null)
                return new AiGenerationResult(null, "تنظیمات سایت پیدا نشد.");

            var candidates = await GetCandidatesAsync(settings, AiUsageScope.ContentStudio);
            if (candidates.Count == 0)
                return new AiGenerationResult(null, await DescribeEmptyChainAsync(settings, AiUsageScope.ContentStudio));

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            };

            // تولید محتوا طولانی‌تر از پاسخ چت است؛ بودجه و تعداد تلاش بیش‌تر.
            var deadline = DateTime.UtcNow + (budget ?? ContentTotalBudget);
            var attempts = 0;
            var errors = new List<string>();
            foreach (var candidate in candidates)
            {
                if (ct.IsCancellationRequested)
                    return new AiGenerationResult(null, "تولید محتوا لغو شد.");
                if (attempts >= Math.Max(1, maxAttempts)) break;
                if (deadline - DateTime.UtcNow < TimeSpan.FromSeconds(10)) break;
                attempts++;

                var (reply, error) = await TryCandidateAsync(candidate, messages, temperature, maxTokens,
                    ContentAttemptTimeout, deadline, ct);
                if (!string.IsNullOrWhiteSpace(reply))
                {
                    await RecordSuccessAsync(candidate);
                    _logger.LogInformation("ContentAi chain: reply from {Provider} / {Model} (attempt {Attempt})",
                        candidate.ProviderName, candidate.Model, attempts);
                    return new AiGenerationResult(reply, null);
                }

                if (!string.IsNullOrWhiteSpace(error))
                    errors.Add($"{candidate.ProviderName} / {candidate.Model}: {error}");
            }

            _logger.LogWarning("ContentAi chain: no reply after {Attempts} attempts", attempts);
            var detail = errors.Count > 0
                ? string.Join(" | ", errors.Distinct().Take(4))
                : "مدل‌ها پاسخی ندادند.";
            return new AiGenerationResult(null, TruncateDetail("پاسخ معتبر از AI دریافت نشد. " + detail, 900));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new AiGenerationResult(null, "تولید محتوا لغو شد.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAiCompatibleChatService: content generation failed");
            return new AiGenerationResult(null, TruncateDetail("خطای غیرمنتظره در تولید محتوا: " + ex.Message, 900));
        }
    }

    /// <summary>
    /// تلاش روی یک کاندید + ادامه‌گیری از finish_reason=length. در صورت شکست، وضعیت کلید/مدل در DB به‌روز می‌شود.
    /// </summary>
    private async Task<(string? Reply, string? Error)> TryCandidateAsync(ChainCandidate candidate, List<object> messages,
        double temperature, int maxTokens, TimeSpan perAttemptTimeout, DateTime deadline, CancellationToken ct = default)
    {
        var parts = new List<string>();
        var turns = new List<object>(messages);

        for (var cont = 0; cont <= MaxContinuations; cont++)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining < TimeSpan.FromSeconds(5)) break;

            var (content, truncated, failKind, failDetail) = await SendOnceAsync(candidate, turns, temperature, maxTokens, perAttemptTimeout, deadline, ct);

            if (ct.IsCancellationRequested) return (null, "درخواست لغو شد.");

            if (failKind != null)
            {
                await RecordFailureAsync(candidate, failKind.Value, failDetail);
                return (null, DescribeFailure(failKind.Value, failDetail)); // کاندید بعدی
            }

            if (string.IsNullOrWhiteSpace(content)) break;

            parts.Add(content);
            if (!truncated || cont == MaxContinuations) break;

            turns.Add(new { role = "assistant", content = string.Concat(parts) });
            turns.Add(new { role = "user", content = ContinuationPrompt });
        }

        if (parts.Count == 0) return (null, "پاسخ خالی بود.");
        var reply = string.Concat(parts).Trim();
        if (string.IsNullOrWhiteSpace(reply)) return (null, "پاسخ خالی بود.");

        // پاسخ خالی/فقط فاصله: به‌عنوان شکست مدل ثبت و کاندید بعدی امتحان شود.
        if (reply.Length < 2)
        {
            await RecordFailureAsync(candidate, FailKind.Empty, "پاسخ خالی");
            return (null, "پاسخ مدل خیلی کوتاه بود.");
        }
        return (reply, null);
    }

    private static string DescribeFailure(FailKind kind, string? detail)
    {
        var baseMessage = kind switch
        {
            FailKind.Auth => "کلید API رد شد (401/403).",
            FailKind.NoCredit => "اعتبار کلید تمام شده است (402).",
            FailKind.RateLimited => "سهمیه یا محدودیت نرخ درخواست پر شده است (429).",
            FailKind.Model => "مدل در دسترس نیست یا شناسه آن اشتباه است (400/404).",
            FailKind.ModelAccess => "دسترسی این کلید به این مدل بسته است (403).",
            FailKind.IpBlocked => "IP خروجی توسط Cloudflare/OpenRouter مسدود شده است (Access denied by security policy)؛ برای همین سرویس‌دهنده پروکسی اضافه یا پروکسی دیگری انتخاب کنید.",
            FailKind.Empty => "مدل پاسخ خالی برگرداند.",
            _ when string.Equals(detail, "timeout", StringComparison.OrdinalIgnoreCase)
                => "مدل در زمان مجاز پاسخ نداد (کند است).",
            _ => "خطای موقت یا شبکه."
        };

        if (!string.IsNullOrWhiteSpace(detail)
            && !string.Equals(detail, "timeout", StringComparison.OrdinalIgnoreCase)
            && !baseMessage.Contains(detail, StringComparison.OrdinalIgnoreCase))
        {
            baseMessage += " " + detail;
        }
        return TruncateDetail(baseMessage, 300);
    }

    /// <summary>
    /// ارسال با فِیل‌اور پروکسی: پروکسی‌ها به ترتیب امتحان می‌شوند؛ خطای شبکه/مسدودی IP
    /// پروکسی را کول‌داون می‌کند و بعدی امتحان می‌شود. خطای کلید/مدل باعث تغییر پروکسی نمی‌شود.
    /// </summary>
    private async Task<(string? Content, bool Truncated, FailKind? FailKind, string? FailDetail)> SendOnceAsync(
        ChainCandidate candidate, List<object> messages, double temperature, int maxTokens,
        TimeSpan perAttemptTimeout, DateTime deadline, CancellationToken ct = default)
    {
        var proxies = candidate.Proxies is { Count: > 0 }
            ? candidate.Proxies
            : new List<ProxyOption> { new(0, "", null) }; // بدون پروکسی = اتصال مستقیم

        FailKind? lastKind = null;
        string? lastDetail = null;

        for (var i = 0; i < proxies.Count; i++)
        {
            var proxy = proxies[i];
            var remaining = deadline - DateTime.UtcNow;
            if (remaining < TimeSpan.FromSeconds(4))
            {
                lastKind ??= FailKind.Transient;
                lastDetail ??= "زمان کل زنجیره تمام شد.";
                break;
            }

            var timeout = TimeSpan.FromSeconds(Math.Clamp(remaining.TotalSeconds, 4, perAttemptTimeout.TotalSeconds));
            var (content, truncated, kind, detail) = await SendOnceViaProxyAsync(
                candidate, proxy.Url, messages, temperature, maxTokens, timeout, ct);

            if (ct.IsCancellationRequested) return (null, false, null, "canceled");

            if (kind == null)
            {
                if (proxy.Id > 0) await RecordProxySuccessAsync(proxy.Id);
                return (content, truncated, null, null);
            }

            lastKind = kind;
            lastDetail = detail;

            if (kind is FailKind.Transient or FailKind.IpBlocked)
            {
                if (proxy.Id > 0) await RecordProxyFailureAsync(proxy.Id, kind.Value, detail);
                _logger.LogWarning("ChatAi proxy failed ({Proxy}); switching to next",
                    proxy.Url.Length == 0 ? "direct" : proxy.Url);
                continue;
            }

            // پاسخ از سرویس‌دهنده رسید؛ پروکسی سالم است و خطا مربوط به کلید/مدل است.
            if (proxy.Id > 0) await RecordProxySuccessAsync(proxy.Id);
            return (null, false, kind, detail);
        }

        return (null, false, lastKind, lastDetail ?? "هیچ پروکسی برای ارسال درخواست باقی نماند.");
    }

    private async Task<(string? Content, bool Truncated, FailKind? FailKind, string? FailDetail)> SendOnceViaProxyAsync(
        ChainCandidate candidate, string? proxyUrl, List<object> messages, double temperature, int maxTokens, TimeSpan timeout,
        CancellationToken ct = default)
    {
        try
        {
            var client = CreateHttpClient(_httpClientFactory, proxyUrl);
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
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ct);
            var response = await client.SendAsync(request, linked.Token);
            var body = await response.Content.ReadAsStringAsync(linked.Token);

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
            if (!choice.TryGetProperty("message", out var message))
                return (null, false, FailKind.Empty, "ساختار پاسخ مدل نامعتبر بود (message ندارد)");

            var content = ExtractMessageContent(message);
            if (string.IsNullOrWhiteSpace(content))
            {
                var hasReasoning = message.TryGetProperty("reasoning", out var reasoning)
                    && reasoning.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(reasoning.GetString());
                return (null, false, FailKind.Empty, hasReasoning
                    ? "مدل فقط بخش استدلال (reasoning) برگرداند؛ سقف توکن خروجی را بالا ببرید یا مدل غیراستدلالی انتخاب کنید."
                    : "محتوای خالی");
            }

            var truncated = choice.TryGetProperty("finish_reason", out var fr)
                && fr.ValueKind == JsonValueKind.String
                && string.Equals(fr.GetString(), "length", StringComparison.OrdinalIgnoreCase);

            return (content.Trim(), truncated, null, null);
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested) return (null, false, null, "canceled");
            _logger.LogWarning("ChatAi chain: {Provider}/{Model} timed out", candidate.ProviderName, candidate.Model);
            return (null, false, FailKind.Transient, "timeout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChatAi chain: {Provider}/{Model} error", candidate.ProviderName, candidate.Model);
            return (null, false, FailKind.Transient, TruncateDetail(ex.Message));
        }
    }

    // برخی مدل‌های چندوجهی content را به‌صورت آرایه‌ای از part برمی‌گردانند، نه رشته.
    private static string? ExtractMessageContent(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var content)) return null;

        if (content.ValueKind == JsonValueKind.String)
            return content.GetString();

        if (content.ValueKind != JsonValueKind.Array) return null;

        var sb = new StringBuilder();
        foreach (var part in content.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                sb.Append(part.GetString());
            }
            else if (part.ValueKind == JsonValueKind.Object
                     && part.TryGetProperty("text", out var text)
                     && text.ValueKind == JsonValueKind.String)
            {
                sb.Append(text.GetString());
            }
        }
        return sb.ToString();
    }

    // ===== طبقه‌بندی خطا =====

    private static FailKind ClassifyStatus(int status, string body) => status switch
    {
        401 => FailKind.Auth,
        // 403 می‌تواند یعنی کلید رد صلاحیت شده، یا فقط دسترسی به همین مدل بسته است
        // (مثل «Access denied by security policy» روی مدل‌های free)؛ در حالت دوم کلید سالم است.
        403 when LooksLikeIpBlock(body) => FailKind.IpBlocked,
        403 => LooksLikeModelIssue(body) ? FailKind.ModelAccess : FailKind.Auth,
        402 => FailKind.NoCredit,
        429 => FailKind.RateLimited,
        404 or 400 when LooksLikeModelIssue(body) => FailKind.Model,
        404 => FailKind.Model,
        _ => FailKind.Transient
    };

    private static bool LooksLikeIpBlock(string body)
    {
        if (string.IsNullOrEmpty(body)) return false;
        return body.Contains("security policy", StringComparison.OrdinalIgnoreCase)
            || body.Contains("access denied by security", StringComparison.OrdinalIgnoreCase);
    }

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
        => TruncateDetail(s, 300);

    private static string TruncateDetail(string? s, int max)
        => string.IsNullOrEmpty(s) ? "" : (s.Length > max ? s[..max] + "…" : s);

    // ===== کول‌داون‌ها و آمار (برای سویچ خودکار + نمایش در پنل ادمین) =====

    private static (TimeSpan? KeyCooldown, TimeSpan? ModelCooldown) CooldownsFor(FailKind kind) => kind switch
    {
        FailKind.Auth => (TimeSpan.FromMinutes(30), null),
        FailKind.NoCredit => (TimeSpan.FromMinutes(60), null),
        FailKind.RateLimited => (TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)),
        FailKind.Model => (null, TimeSpan.FromMinutes(360)),
        // فقط همین مدل بسته است؛ کلید برای بقیه مدل‌ها سالم می‌ماند.
        FailKind.ModelAccess => (null, TimeSpan.FromMinutes(60)),
        // مسدودسازی IP ربطی به کلید/مدل ندارد؛ کول‌داون کوتاه تا بعد از تنظیم پروکسی سریع بازیابی شود.
        FailKind.IpBlocked => (TimeSpan.FromMinutes(5), null),
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

    private static TimeSpan ProxyCooldownFor(FailKind kind) => kind switch
    {
        // IP پروکسی هم مسدود است؛ کمی بیشتر کنار گذاشته می‌شود.
        FailKind.IpBlocked => TimeSpan.FromMinutes(30),
        // پروکسی جواب نداد/تایم‌اوت شد؛ چند دقیقه بعد دوباره امتحان می‌شود.
        FailKind.Transient => TimeSpan.FromMinutes(10),
        _ => TimeSpan.FromMinutes(5)
    };

    private async Task RecordProxyFailureAsync(int proxyId, FailKind kind, string? detail)
    {
        try
        {
            var proxy = await _db.ChatAiProxies.FindAsync(proxyId);
            if (proxy is null) return;

            var now = DateTime.UtcNow;
            proxy.ConsecutiveFailures++;
            proxy.FailureCount++;
            proxy.LastError = TruncateDetail(detail);
            proxy.LastUsedAt = now;
            proxy.CooldownUntil = now + ProxyCooldownFor(kind);
            await _db.SaveChangesAsync();

            // کاندیدهای کش‌شده نباید پروکسیِ تازه‌کول‌داون‌شده را دوباره اول صف بگذارند.
            InvalidateCandidateCache();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChatAi chain: failed to record proxy failure");
        }
    }

    private async Task RecordProxySuccessAsync(int proxyId)
    {
        try
        {
            var proxy = await _db.ChatAiProxies.FindAsync(proxyId);
            if (proxy is null) return;

            proxy.ConsecutiveFailures = 0;
            proxy.CooldownUntil = null;
            proxy.LastError = null;
            proxy.LastUsedAt = DateTime.UtcNow;
            proxy.SuccessCount++;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChatAi chain: failed to record proxy success");
        }
    }

    // ===== ساخت زنجیره کاندیدها: Provider ← Key ← Model =====

    private async Task<List<ChainCandidate>> GetCandidatesAsync(SiteSettings settings, AiUsageScope scope)
    {
        var cached = scope == AiUsageScope.Chat ? _chatCandidateCache : _studioCandidateCache;
        var cachedAt = scope == AiUsageScope.Chat
            ? Interlocked.Read(ref _chatCandidateCacheAtTicks)
            : Interlocked.Read(ref _studioCandidateCacheAtTicks);
        if (cached != null && DateTime.UtcNow.Ticks - cachedAt < CandidateCacheTtl.Ticks)
            return cached;

        var list = await BuildCandidatesAsync(settings, scope);
        if (scope == AiUsageScope.Chat)
        {
            _chatCandidateCache = list;
            Interlocked.Exchange(ref _chatCandidateCacheAtTicks, DateTime.UtcNow.Ticks);
        }
        else
        {
            _studioCandidateCache = list;
            Interlocked.Exchange(ref _studioCandidateCacheAtTicks, DateTime.UtcNow.Ticks);
        }
        return list;
    }

    /// <summary>
    /// بیلد زنجیره از جداول جدید برای کاربرد مشخص؛ اگر هیچ سرویس‌دهنده‌ای برای این کاربرد
    /// انتخاب نشده باشد از پیکربندی قدیمی fallback می‌گیرد.
    /// </summary>
    public async Task<List<ChainCandidate>> BuildCandidatesAsync(SiteSettings settings, AiUsageScope scope = AiUsageScope.Chat)
    {
        var now = DateTime.UtcNow;
        var enabledProviders = await _db.ChatAiProviders.AsNoTracking()
            .Include(p => p.ApiKeys)
            .Include(p => p.Models)
            .Include(p => p.Proxies)
            .Where(p => p.Enabled)
            .OrderBy(p => p.Priority).ThenBy(p => p.Id)
            .ToListAsync();

        var providers = enabledProviders
            .Where(p => scope == AiUsageScope.Chat ? p.UseForChat : p.UseForContentStudio)
            .ToList();

        var list = new List<ChainCandidate>();
        foreach (var p in providers)
        {
            if (string.IsNullOrWhiteSpace(p.BaseUrl)) continue;

            var proxyEntities = p.Proxies
                .Where(x => x.Enabled && !string.IsNullOrWhiteSpace(x.Url))
                .OrderBy(x => x.Priority).ThenBy(x => x.Id)
                .ToList();
            var activeProxies = proxyEntities
                .Where(x => x.CooldownUntil == null || x.CooldownUntil < now)
                .Select(x => new ProxyOption(x.Id, x.Url.Trim(), x.Label))
                .ToList();

            // پروکسی تعریف شده اما همه در کول‌داون‌اند؛ تا بازیابی، این سرویس‌دهنده کاندید ندارد.
            if (proxyEntities.Count > 0 && activeProxies.Count == 0) continue;

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
                    list.Add(new ChainCandidate(k.Id, m.Id, p.BaseUrl.TrimEnd('/'), k.KeyValue, m.ModelId, p.Name, activeProxies));
        }

        if (list.Count > 0) return list;

        // سرویس‌دهنده برای این کاربرد انتخاب شده اما کلید/مدل سالم ندارد؛
        // نباید بی‌صدا به پیکربندی قدیمی (که ممکن است برای کاربرد دیگری باشد) برگردیم.
        if (providers.Count > 0) return list;

        // fallback: پیکربندی قدیمی تک‌کلیدی (هیچ‌چیز برای ادمین تغییر نکرده باشد هم کار می‌کند)
        if (!string.IsNullOrWhiteSpace(settings.ChatAiApiUrl) && !string.IsNullOrWhiteSpace(settings.ChatAiModel))
        {
            var baseUrl = settings.ChatAiApiUrl.TrimEnd('/');
            var apiKey = settings.ChatAiApiKey ?? "";
            var models = string.Equals(settings.ChatAiModel, "auto", StringComparison.OrdinalIgnoreCase)
                ? AutoModels
                : new[] { settings.ChatAiModel! };
            foreach (var m in models)
                list.Add(new ChainCandidate(null, null, baseUrl, apiKey, m, "پیکربندی قدیمی", Array.Empty<ProxyOption>()));
        }

        return list;
    }

    /// <summary>
    /// وقتی زنجیره خالی است، دلیل واقعی را برمی‌گرداند تا پیام «سرویس فعال نیست» گمراه‌کننده نباشد
    /// (مثلاً همه مدل‌ها در کول‌داون باشند).
    /// </summary>
    public async Task<string> DescribeEmptyChainAsync(SiteSettings settings, AiUsageScope scope)
    {
        var now = DateTime.UtcNow;
        var scopeName = scope == AiUsageScope.Chat ? "چت" : "استودیوی محتوا";

        var providers = await _db.ChatAiProviders.AsNoTracking()
            .Include(p => p.ApiKeys).Include(p => p.Models).Include(p => p.Proxies)
            .Where(p => p.Enabled)
            .OrderBy(p => p.Priority).ThenBy(p => p.Id)
            .ToListAsync();

        var scoped = providers
            .Where(p => scope == AiUsageScope.Chat ? p.UseForChat : p.UseForContentStudio)
            .Where(p => !string.IsNullOrWhiteSpace(p.BaseUrl))
            .ToList();

        if (scoped.Count == 0)
            return $"هیچ سرویس‌دهنده فعالی برای «{scopeName}» انتخاب نشده است. در تنظیمات هوش مصنوعی سرویس‌دهنده را فعال و کاربرد «{scopeName}» را روشن کنید.";

        var keys = scoped.SelectMany(p => p.ApiKeys.Where(k => k.Enabled && !string.IsNullOrWhiteSpace(k.KeyValue))).ToList();
        if (keys.Count == 0)
            return $"برای سرویس‌دهنده‌های «{scopeName}» کلید فعال ثبت نشده است.";

        var models = scoped.SelectMany(p => p.Models.Where(m => m.Enabled && !string.IsNullOrWhiteSpace(m.ModelId))).ToList();
        if (models.Count == 0)
            return $"برای سرویس‌دهنده‌های «{scopeName}» مدل فعال ثبت نشده است.";

        var proxies = scoped
            .SelectMany(p => p.Proxies.Where(x => x.Enabled && !string.IsNullOrWhiteSpace(x.Url)))
            .ToList();
        if (proxies.Count > 0 && proxies.All(x => x.CooldownUntil != null && x.CooldownUntil > now))
        {
            var until = proxies.Select(x => x.CooldownUntil!.Value).Max().ToLocalTime().ToString("HH:mm");
            var lastError = proxies.Select(x => x.LastError).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
            var detail = string.IsNullOrWhiteSpace(lastError) ? "" : $"؛ آخرین خطا: {TruncateDetail(lastError, 160)}";
            return $"همه پروکسی‌های «{scopeName}» موقتاً در کول‌داون هستند (تا {until}){detail}. «پاک‌کردن کول‌داون» را بزنید یا پروکسی دیگری اضافه کنید.";
        }

        var cooledKeys = keys.Where(k => k.CooldownUntil != null && k.CooldownUntil > now).ToList();
        var cooledModels = models.Where(m => m.CooldownUntil != null && m.CooldownUntil > now).ToList();
        if (cooledKeys.Count > 0 || cooledModels.Count > 0)
        {
            var until = cooledKeys.Select(k => k.CooldownUntil!.Value)
                .Concat(cooledModels.Select(m => m.CooldownUntil!.Value))
                .Max().ToLocalTime().ToString("HH:mm");
            var lastError = cooledModels.Select(m => m.LastError)
                .Concat(cooledKeys.Select(k => k.LastError))
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
            var detail = string.IsNullOrWhiteSpace(lastError) ? "" : $"؛ آخرین خطا: {TruncateDetail(lastError, 160)}";
            return $"کلید/مدل‌های «{scopeName}» موقتاً در کول‌داون هستند (تا {until}){detail}. در تنظیمات هوش مصنوعی دکمه «پاک‌کردن کول‌داون» را بزنید یا تا پایان کول‌داون صبر کنید.";
        }

        return $"کاندید فعالی برای «{scopeName}» پیدا نشد؛ سرویس‌دهنده، کلید و مدل‌ها را در تنظیمات هوش مصنوعی بررسی کنید.";
    }

    public static void InvalidateCandidateCache()
    {
        Interlocked.Exchange(ref _chatCandidateCacheAtTicks, 0);
        Interlocked.Exchange(ref _studioCandidateCacheAtTicks, 0);
    }
}
