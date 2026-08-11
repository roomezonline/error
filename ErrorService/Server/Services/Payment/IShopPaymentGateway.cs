using ErrorService.Shared;

namespace ErrorService.Server.Services.Payment;

public interface IShopPaymentGateway
{
    PaymentProvider Provider { get; }
    Task<PaymentIntentResult> CreateIntentAsync(PaymentIntentRequest request, CancellationToken ct = default);
    Task<PaymentIntentResult> VerifyCallbackAsync(PaymentCallbackPayload payload, CancellationToken ct = default);
}