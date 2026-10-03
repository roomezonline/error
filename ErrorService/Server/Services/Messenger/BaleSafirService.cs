using System.Text;
using System.Text.Json;
using ErrorService.Server.Models;
using ErrorService.Shared;

namespace ErrorService.Server.Services.Messenger;

public sealed class BaleSafirService
{
    public const string SendUrl = "https://safir.bale.ai/api/v3/send_message";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BaleBotService _baleBot;
    private readonly ILogger<BaleSafirService> _logger;

    private string? _cachedToken;
    private long? _cachedBotId;

    public BaleSafirService(IHttpClientFactory httpClientFactory, BaleBotService baleBot, ILogger<BaleSafirService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _baleBot = baleBot;
        _logger = logger;
    }

    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith("09"))
            digits = "98" + digits[1..];
        else if (digits.Length == 12 && digits.StartsWith("098"))
            digits = "98" + digits[2..];
        else if (digits.Length == 13 && digits.StartsWith("9809"))
            digits = "98" + digits[2..];

        return digits.Length == 12 && digits.StartsWith("98") ? digits : null;
    }

    public async Task<MessengerSendResult> SendToPhoneAsync(SiteSettings? settings, string phone, string text, string? actionUrl)
    {
        var apiKey = settings?.BaleSafirApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            return MessengerSendResult.Fail("کلید دسترسی سفیر (سرویس ارسال بله) در تنظیمات ربات تنظیم نشده است");

        var normalized = NormalizePhone(phone);
        if (normalized == null)
            return MessengerSendResult.Fail("شماره موبایل گیرنده نامعتبر است");

        var botId = await GetBotIdAsync(settings);
        if (botId == null)
            return MessengerSendResult.Fail("شناسه بازوی بله قابل دریافت نیست؛ توکن ربات را بررسی کنید");

        try
        {
            var client = _httpClientFactory.CreateClient("BaleBot");
            var payload = new Dictionary<string, object?>
            {
                ["request_id"] = Guid.NewGuid().ToString("N"),
                ["bot_id"] = botId.Value,
                ["phone_number"] = normalized,
                ["message_data"] = BuildMessageData(text, actionUrl)
            };

            var request = new HttpRequestMessage(HttpMethod.Post, SendUrl);
            request.Headers.Add("api-access-key", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var httpStatus = (int)response.StatusCode;

            if (string.IsNullOrWhiteSpace(body))
                return MessengerSendResult.Fail($"پاسخ خالی از سفیر (HTTP {httpStatus})");

            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
            }

            if (doc == null)
                return MessengerSendResult.Fail($"پاسخ نامعتبر از سفیر (HTTP {httpStatus})");

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.String)
                    return MessengerSendResult.Fail(DescribeSafirError(null, root.GetString()));

                if (root.ValueKind != JsonValueKind.Object)
                    return MessengerSendResult.Fail($"پاسخ نامعتبر از سفیر (HTTP {httpStatus})");

                if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                    return MessengerSendResult.Fail(DescribeSafirError(GetInt(error, "code"), GetString(error, "description")));

                if (root.TryGetProperty("error_data", out var errorData) && errorData.ValueKind == JsonValueKind.Array && errorData.GetArrayLength() > 0)
                {
                    var first = errorData[0];
                    if (first.ValueKind == JsonValueKind.Object)
                        return MessengerSendResult.Fail(DescribeSafirError(GetInt(first, "code"), GetString(first, "description")));
                }

                if (root.TryGetProperty("message_id", out var messageId))
                    return MessengerSendResult.Ok(messageId.ToString());

                return MessengerSendResult.Fail(httpStatus >= 400
                    ? $"سفیر بله درخواست را نپذیرفت (HTTP {httpStatus})"
                    : body.Length > 500 ? body[..500] : body);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "BaleSafir: network failure");
            return MessengerSendResult.Fail("ارتباط با سرویس سفیر بله برقرار نشد؛ اتصال اینترنت سرور را بررسی کنید");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BaleSafir: send to phone failed");
            return MessengerSendResult.Fail(ex.Message);
        }
    }

    private static object BuildMessageData(string text, string? actionUrl)
    {
        var message = new Dictionary<string, object?>
        {
            ["text"] = text
        };

        if (!string.IsNullOrWhiteSpace(actionUrl))
        {
            message["reply_markup"] = new Dictionary<string, object?>
            {
                ["inline_keyboard"] = new[]
                {
                    new object[]
                    {
                        new Dictionary<string, object?> { ["text"] = "مشاهده در سایت", ["url"] = actionUrl }
                    }
                }
            };
        }

        return new Dictionary<string, object?> { ["message"] = message };
    }

    private async Task<long?> GetBotIdAsync(SiteSettings? settings)
    {
        var token = settings?.BaleBotToken?.Trim();
        if (string.IsNullOrWhiteSpace(token))
            return null;
        if (_cachedBotId.HasValue && string.Equals(_cachedToken, token, StringComparison.Ordinal))
            return _cachedBotId;

        var botId = await _baleBot.GetBotIdAsync();
        if (botId.HasValue)
        {
            _cachedToken = token;
            _cachedBotId = botId;
        }
        return botId;
    }

    private static string DescribeSafirError(int? code, string? description)
    {
        var text = description?.Trim().ToLowerInvariant();
        var message = text switch
        {
            "invalid_access_key" => "کلید دسترسی سفیر (Api Access Key) نامعتبر است",
            "invalid_bot_id" or "bot_not_found" => "شناسه بازو (ربات) بله نامعتبر است",
            "invalid_phone_number" or "invalid_phone" => "شماره موبایل گیرنده نامعتبر است",
            "payment_required" or "not_enough_balance" or "insufficient_balance" => "اعتبار سفیر کافی نیست؛ از پنل کسب‌وکار بله شارژ کنید",
            _ => code switch
            {
                2 => "خطای داخلی سرویس سفیر بله",
                3 => "محدودیت نرخ ارسال سفیر (تعداد پیام زیاد است؛ کمی بعد تلاش می‌شود)",
                4 => "درخواست ارسال سفیر نامعتبر است",
                8 => "شماره موبایل گیرنده نامعتبر است",
                17 => "گیرنده در پیام‌رسان بله عضو نیست",
                20 => "اعتبار سفیر کافی نیست؛ از پنل کسب‌وکار بله شارژ کنید",
                21 => "به محدودیت تعداد مخاطب بازو رسیده‌اید",
                null when !string.IsNullOrWhiteSpace(description) => description,
                null => "خطای ناشناخته در سفیر بله",
                _ => description ?? $"خطای سفیر با کد {code.Value}"
            }
        };
        return string.IsNullOrWhiteSpace(message) ? "خطای ناشناخته در سفیر بله" : message;
    }

    private static int? GetInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
