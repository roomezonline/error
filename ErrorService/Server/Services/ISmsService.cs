using ErrorService.Shared;

namespace ErrorService.Server.Services;

public interface ISmsService
{
    Task<OtpResultDto> SendVerificationSmsAsync(int workshopId, string mobile, string code, int templateId);
    Task<SendSmsResultDto> SendTechnicianSmsAsync(int workshopId, string mobile, string technicianName, string workshopName, int templateId);
    Task<SendSmsResultDto> SendOrderRegisteredSmsAsync(int workshopId, string mobile, string customerName, string deviceType, string? deviceBrand, string date, string workshopName, string requestId);
    Task<SendSmsResultDto> SendOrderReadyForDeliverySmsAsync(int workshopId, string mobile, string customerName, string deviceType, string? deviceBrand, string date, string workshopName, string state, int? receiptId);
    Task<SendSmsResultDto> SendOrderUnrepairableSmsAsync(int workshopId, string mobile, string customerName, string deviceType, string? deviceBrand, string date, string workshopName, string state, int? receiptId);
    Task<DeliveryCheckResultDto> CheckDeliveryStatusAsync(string messageId, string apiKey);
}

public sealed class DeliveryCheckResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public byte? DeliveryState { get; set; }
    public DateTime? DeliveryDateTime { get; set; }
    public string? ProviderRawStatus { get; set; }
}

public sealed class SendSmsResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? MessageId { get; set; }
}
