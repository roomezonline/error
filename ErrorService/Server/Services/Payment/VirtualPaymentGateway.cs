using ErrorService.Shared;

namespace ErrorService.Server.Services.Payment;

/// <summary>
/// Simulated online gateway for local testing.
/// CreateIntentAsync returns a guarded redirection to a simulated payment page
/// that immediately bounces back to the callback as a successful payment.
/// </summary>
public sealed class VirtualPaymentGateway : IShopPaymentGateway
{
    public PaymentProvider Provider => PaymentProvider.Zarinpal;

    public Task<PaymentIntentResult> CreateIntentAsync(PaymentIntentRequest request, CancellationToken ct = default)
        => Task.FromResult(new PaymentIntentResult
        {
            Success = true,
            Message = "درگاه تست (مجازی) آماده است.",
            PaymentMode = "Virtual",
            Authority = $"VIR-{Guid.NewGuid():N}",
            GatewayUrl = $"/payment/simulate/{request.OrderId}?authority={Guid.NewGuid():N}&amount={(long)Math.Round(Math.Max(0, request.Amount))}"
        });

    public Task<PaymentIntentResult> VerifyCallbackAsync(PaymentCallbackPayload payload, CancellationToken ct = default)
        => Task.FromResult(string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase)
            ? new PaymentIntentResult
            {
                Success = true,
                Message = "پرداخت (تست) تایید شد.",
                PaymentMode = "Virtual",
                Authority = payload.Authority,
                GatewayUrl = payload.RefId
            }
            : new PaymentIntentResult { Success = false, Message = "پرداخت (تست) ناموفق بود." });
}