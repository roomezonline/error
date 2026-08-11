using ErrorService.Shared;

namespace ErrorService.Server.Services.Payment;

public sealed class ManualPaymentGateway : IShopPaymentGateway
{
    public PaymentProvider Provider => PaymentProvider.Manual;

    public Task<PaymentIntentResult> CreateIntentAsync(PaymentIntentRequest request, CancellationToken ct = default)
        => Task.FromResult(new PaymentIntentResult
        {
            Success = true,
            Message = "پرداخت به‌صورت کارت‌به‌کارت انجام می‌شود.",
            PaymentMode = "Manual",
            GatewayUrl = $"/payment/offline/{request.OrderId}"
        });

    public Task<PaymentIntentResult> VerifyCallbackAsync(PaymentCallbackPayload payload, CancellationToken ct = default)
        => Task.FromResult(new PaymentIntentResult
        {
            Success = false,
            Message = "پرداخت آنلاین فعال نیست."
        });
}