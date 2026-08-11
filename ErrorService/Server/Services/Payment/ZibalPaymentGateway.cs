using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorService.Server.Models;
using ErrorService.Shared;

namespace ErrorService.Server.Services.Payment;

/// <summary>
/// Zibal (زیبال) gateway implementation.
/// Request:  POST https://gateway.zibal.ir/v1/request
/// Verify:   POST https://gateway.zibal.ir/v1/verify
/// Payment:  https://gateway.zibal.ir/start/{trackId}
/// </summary>
public sealed class ZibalPaymentGateway : IShopPaymentGateway
{
    private readonly string _merchant;
    private readonly string? _callbackBaseUrl;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ZibalPaymentGateway> _logger;

    public ZibalPaymentGateway(PaymentGateway row, IHttpClientFactory httpClientFactory, ILogger<ZibalPaymentGateway> logger)
    {
        _merchant = PaymentGatewayFactory.GetConfigValue(row, "merchant") ?? "zibal";
        _callbackBaseUrl = row.CallbackBaseUrl;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public PaymentProvider Provider => PaymentProvider.Zibal;

    private const string RequestUrl = "https://gateway.zibal.ir/v1/request";
    private const string VerifyUrl = "https://gateway.zibal.ir/v1/verify";
    private const string StartPayBase = "https://gateway.zibal.ir/start/";

    public async Task<PaymentIntentResult> CreateIntentAsync(PaymentIntentRequest request, CancellationToken ct = default)
    {
        var amount = Math.Max(0, (long)Math.Round(request.Amount));
        var callbackUrl = string.IsNullOrWhiteSpace(request.CallbackUrl) ? _callbackBaseUrl : request.CallbackUrl;

        var payload = new ZibalRequest
        {
            Merchant = _merchant,
            Amount = amount,
            CallbackUrl = callbackUrl ?? "",
            Description = string.IsNullOrWhiteSpace(request.Description) ? $"پرداخت سفارش {request.OrderNumber}" : request.Description,
            Mobile = request.Mobile,
            OrderId = request.OrderId.ToString()
        };

        try
        {
            using var http = _httpClientFactory.CreateClient();
            var response = await http.PostAsJsonAsync(RequestUrl, payload, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var result = root.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetInt32() : -1;
            var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;

            if (result == 100)
            {
                var trackId = root.TryGetProperty("trackId", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(trackId))
                    return new PaymentIntentResult { Success = false, Message = "پاسخ درگاه زیبال ناقص بود (trackId دریافت نشد)." };

                return new PaymentIntentResult
                {
                    Success = true,
                    Message = "اتصال به درگاه زیبال برقرار شد.",
                    Authority = trackId,
                    PaymentMode = "Zibal",
                    GatewayUrl = $"{StartPayBase}{trackId}"
                };
            }

            _logger.LogWarning("Zibal request failed result={Result}, message={Message}", result, message);
            return new PaymentIntentResult { Success = false, Message = $"درگاه خطا داد: {message ?? result.ToString()}" };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zibal create-intent failed for order {OrderNumber}", request.OrderNumber);
            return new PaymentIntentResult { Success = false, Message = "خطا در اتصال به درگاه پرداخت." };
        }
    }

    public async Task<PaymentIntentResult> VerifyCallbackAsync(PaymentCallbackPayload payload, CancellationToken ct = default)
    {
        if (!string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentIntentResult { Success = false, Message = "پرداخت توسط مشتری لغو شده یا ناموفق بوده است." };
        }
        if (string.IsNullOrWhiteSpace(payload.Authority))
        {
            return new PaymentIntentResult { Success = false, Message = "شناسه تراکنش (trackId) دریافت نشد." };
        }

        var verify = new ZibalVerifyRequest
        {
            Merchant = _merchant,
            TrackId = payload.Authority!
        };

        try
        {
            using var http = _httpClientFactory.CreateClient();
            var response = await http.PostAsJsonAsync(VerifyUrl, verify, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var result = root.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetInt32() : -1;
            var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            var status = root.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : -1;

            if (result != 100 || status != 1)
            {
                _logger.LogWarning("Zibal verify failed result={Result}, status={Status}, message={Message}", result, status, message);
                return new PaymentIntentResult { Success = false, Message = $"تایید پرداخت ناموفق بود: {message ?? $"(کد {result})"}" };
            }

            var refNumber = root.TryGetProperty("refNumber", out var rf) && rf.ValueKind == JsonValueKind.String ? rf.GetString() : null;
            return new PaymentIntentResult
            {
                Success = true,
                Message = "پرداخت تایید شد.",
                PaymentMode = "Zibal",
                Authority = payload.Authority,
                GatewayUrl = refNumber
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zibal verify failed for order {OrderId}", payload.OrderId);
            return new PaymentIntentResult { Success = false, Message = "خطا در تایید پرداخت." };
        }
    }
}

file sealed class ZibalRequest
{
    [JsonPropertyName("merchant")]
    public string Merchant { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("callbackUrl")]
    public string CallbackUrl { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [JsonPropertyName("orderId")]
    public string? OrderId { get; set; }
}

file sealed class ZibalVerifyRequest
{
    [JsonPropertyName("merchant")]
    public string Merchant { get; set; } = string.Empty;

    [JsonPropertyName("trackId")]
    public string TrackId { get; set; } = string.Empty;
}