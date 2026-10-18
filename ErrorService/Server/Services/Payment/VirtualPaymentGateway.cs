using ErrorService.Shared;

namespace ErrorService.Server.Services.Payment;

/// <summary>
/// Simulated online gateway for local testing.
/// CreateIntentAsync returns a guarded redirection to a simulated payment page
/// that immediately bounces back to the callback as a successful payment.
/// </summary>
public sealed class VirtualPaymentGateway : IShopPaymentGateway
{
    public PaymentProvider Provider => PaymentProvider.Virtual;

    public Task<PaymentIntentResult> CreateIntentAsync(PaymentIntentRequest request, CancellationToken ct = default)
        => Task.FromResult(new PaymentIntentResult
        {
            Success = true,
            Message = "درگاه تست (مجازی) آماده است.",
            PaymentMode = "Virtual",
            Authority = $"VIR-{Guid.NewGuid():N}",
            GatewayUrl = $"/api/payment/simulate/{request.OrderId}?authority={Guid.NewGuid():N}&amount={(long)Math.Round(Math.Max(0, request.Amount))}"
        });

    public Task<PaymentIntentResult> VerifyCallbackAsync(PaymentCallbackPayload payload, CancellationToken ct = default)
    {
        // فقط تاییدِ صریح OK با شناسهٔ پرداخت معتبر پذیرفته می‌شود؛
        // هر Status دیگر یا Authority خالی = پرداخت ناموفق.
        if (!string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(payload.Authority)
            || payload.Amount < 0)
            return Task.FromResult(new PaymentIntentResult { Success = false, Message = "پرداخت (تست) ناموفق بود." });

        return Task.FromResult(new PaymentIntentResult
        {
            Success = true,
            Message = "پرداخت (تست) تایید شد.",
            PaymentMode = "Virtual",
            Authority = payload.Authority,
            // مرجع تراکنش برای ثبت روی سفارش (درگاه‌های واقعی همین را برمی‌گردانند)
            GatewayUrl = string.IsNullOrWhiteSpace(payload.RefId) ? payload.Authority : payload.RefId
        });
    }
}