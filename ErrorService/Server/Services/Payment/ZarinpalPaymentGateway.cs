using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorService.Server.Models;
using ErrorService.Shared;

namespace ErrorService.Server.Services.Payment;

public sealed class ZarinpalPaymentGateway : IShopPaymentGateway
{
    private readonly string? _merchantId;
    private readonly bool _sandbox;
    private readonly string? _callbackBaseUrl;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ZarinpalPaymentGateway> _logger;

    public ZarinpalPaymentGateway(PaymentGateway row, IHttpClientFactory httpClientFactory, ILogger<ZarinpalPaymentGateway> logger)
    {
        _merchantId = PaymentGatewayFactory.GetConfigValue(row, "merchant_id");
        _sandbox = row.Sandbox;
        _callbackBaseUrl = row.CallbackBaseUrl;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public PaymentProvider Provider => PaymentProvider.Zarinpal;

    private string ApiBase => _sandbox
        ? "https://sandbox.zarinpal.com/pg/v4/payment/"
        : "https://payment.zarinpal.com/pg/v4/payment/";

    private string StartPayBase => _sandbox
        ? "https://sandbox.zarinpal.com/pg/StartPay/"
        : "https://payment.zarinpal.com/pg/StartPay/";

    public async Task<PaymentIntentResult> CreateIntentAsync(PaymentIntentRequest request, CancellationToken ct = default)
    {
        var merchantId = _merchantId;
        if (string.IsNullOrWhiteSpace(merchantId))
        {
            return new PaymentIntentResult
            {
                Success = false,
                Message = "درگاه زرین‌پال هنوز پیکربندی نشده است (شناسه درگاه/مرچنت تنظیم نشده)."
            };
        }

        var amount = Math.Max(0, (long)Math.Round(request.Amount));
        var callbackUrl = string.IsNullOrWhiteSpace(request.CallbackUrl) ? _callbackBaseUrl : request.CallbackUrl;

        var payload = new ZarinpalRequest
        {
            MerchantId = merchantId,
            Amount = amount,
            CallbackUrl = callbackUrl ?? "",
            Description = string.IsNullOrWhiteSpace(request.Description) ? $"پرداخت سفارش {request.OrderNumber}" : request.Description,
            Metadata = new ZarinpalMetadata
            {
                Email = request.Email,
                Mobile = request.Mobile
            }
        };

        try
        {
            using var http = _httpClientFactory.CreateClient();
            var response = await http.PostAsJsonAsync($"{ApiBase}request.json", payload, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var code = root.GetProperty("data").GetProperty("code").GetInt32();
            var message = root.TryGetProperty("errors", out var errs) && errs.TryGetProperty("message", out var errMsg) && errMsg.ValueKind == JsonValueKind.String
                ? errMsg.GetString()
                : null;

            if (code == 100)
            {
                var authority = root.GetProperty("data").GetProperty("authority").GetString();
                return new PaymentIntentResult
                {
                    Success = true,
                    Message = "اتصال به درگاه زرین‌پال برقرار شد.",
                    Authority = authority,
                    PaymentMode = "Zarinpal",
                    GatewayUrl = $"{StartPayBase}{authority}"
                };
            }

            _logger.LogWarning("Zarinpal request failed code={Code}, message={Message}", code, message);
            return new PaymentIntentResult { Success = false, Message = $"درگاه خطا داد: {message ?? code.ToString()}" };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zarinpal create-intent failed for order {OrderNumber}", request.OrderNumber);
            return new PaymentIntentResult { Success = false, Message = "خطا در اتصال به درگاه پرداخت." };
        }
    }

    public async Task<PaymentIntentResult> VerifyCallbackAsync(PaymentCallbackPayload payload, CancellationToken ct = default)
    {
        var merchantId = _merchantId;
        if (string.IsNullOrWhiteSpace(merchantId))
        {
            return new PaymentIntentResult { Success = false, Message = "درگاه زرین‌پال پیکربندی نشده است." };
        }
        if (!string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentIntentResult { Success = false, Message = "پرداخت توسط مشتری لغو شده یا ناموفق بوده است." };
        }
        if (string.IsNullOrWhiteSpace(payload.Authority))
        {
            return new PaymentIntentResult { Success = false, Message = "شناسه پرداخت (Authority) دریافت نشد." };
        }

        var verify = new ZarinpalVerifyRequest
        {
            MerchantId = merchantId,
            Amount = Math.Max(0, (long)Math.Round(payload.Amount)),
            Authority = payload.Authority!
        };

        try
        {
            using var http = _httpClientFactory.CreateClient();
            var response = await http.PostAsJsonAsync($"{ApiBase}verify.json", verify, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var data = root.GetProperty("data");
            var code = data.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.Number ? codeEl.GetInt32() : -1;

            if (code is 100 or 101)
            {
                var refId = data.TryGetProperty("ref_id", out var refEl) && refEl.ValueKind == JsonValueKind.String ? refEl.GetString() : null;
                return new PaymentIntentResult
                {
                    Success = true,
                    Message = "پرداخت تایید شد.",
                    PaymentMode = "Zarinpal",
                    Authority = payload.Authority,
                    GatewayUrl = refId
                };
            }

            _logger.LogWarning("Zarinpal verify failed code={Code}, body={Body}", code, body);
            return new PaymentIntentResult { Success = false, Message = $"تایید پرداخت ناموفق بود (کد {code})." };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zarinpal verify failed for order {OrderId}", payload.OrderId);
            return new PaymentIntentResult { Success = false, Message = "خطا در تایید پرداخت." };
        }
    }
}

file sealed class ZarinpalRequest
{
    [JsonPropertyName("merchant_id")]
    public string MerchantId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency => "IRT";

    [JsonPropertyName("callback_url")]
    public string CallbackUrl { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public ZarinpalMetadata Metadata { get; set; } = new();
}

file sealed class ZarinpalMetadata
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }
}

file sealed class ZarinpalVerifyRequest
{
    [JsonPropertyName("merchant_id")]
    public string MerchantId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("authority")]
    public string Authority { get; set; } = string.Empty;
}