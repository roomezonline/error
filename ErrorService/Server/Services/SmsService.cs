using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

public sealed class SmsService : ISmsService
{
    private readonly ErrorServiceDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SmsService> _logger;

    public SmsService(ErrorServiceDbContext db, IHttpClientFactory httpClientFactory, ILogger<SmsService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OtpResultDto> SendVerificationSmsAsync(int workshopId, string mobile, string code, int templateId)
    {
        var settings = await _db.SmsSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            _logger.LogError("SmsSettings table is empty — no SMS settings configured");
            Console.Error.WriteLine("[SmsService] CRITICAL: SmsSettings table is empty. Go to SMS panel → Settings and save the configuration.");
            return Fail("تنظیمات پیامک پیکربندی نشده است. لطفاً با مدیر سیستم تماس بگیرید.");
        }
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            _logger.LogError("SmsSettings.ApiKey is null/empty");
            Console.Error.WriteLine("[SmsService] CRITICAL: API Key is not set in SMS settings.");
            return Fail("کلید API پیامک تنظیم نشده است. لطفاً با مدیر سیستم تماس بگیرید.");
        }

        var tariff = settings.TariffPerSms;

        var credit = await _db.WorkshopSmsCredits.FirstOrDefaultAsync(c => c.WorkshopId == workshopId);
        if (credit == null)
        {
            _logger.LogError("No WorkshopSmsCredit record for workshop {WorkshopId}", workshopId);
            Console.Error.WriteLine($"[SmsService] CRITICAL: No credit record for workshop #{workshopId}. Create one via SMS panel → Credits.");
            return Fail("خطا در اعتبار سنجی پیامک. لطفاً با پشتیبانی تماس بگیرید.");
        }
        if (credit.Balance < tariff)
        {
            _logger.LogWarning("Insufficient credit for workshop {WorkshopId}: balance {Balance}, tariff {Tariff}",
                workshopId, credit.Balance, tariff);
            Console.Error.WriteLine($"[SmsService] WARNING: Workshop #{workshopId} has balance {credit.Balance} but tariff is {tariff}.");

            var log = new SmsLog
            {
                WorkshopId = workshopId,
                ModuleType = SmsModuleType.ActivationCode,
                RecipientNumber = mobile,
                MessageText = "",
                SendStatus = SmsSendStatus.Failed,
                ErrorMessage = "اعتبار کافی نیست",
                Cost = 0,
                DeliveryStatus = SmsDeliveryStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _db.SmsLogs.Add(log);
            await _db.SaveChangesAsync();

            return Fail("سرویس پیامک بصورت موقت غیرفعال شده است. لطفاً بعداً تلاش کنید یا با پشتیبانی تماس بگیرید.");
        }

        var messageText = $"مشتری گرامی\nکد تائید شما :\nCode:{code}\nمجموعه فنی مهندسی ارور سرویس\nhttps://errorservice.ir/";

        var payload = new SmsIrVerifyRequest
        {
            Mobile = mobile,
            TemplateId = templateId,
            Parameters = new[]
            {
                new SmsIrParameter { Name = "CODE", Value = code }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        try
        {
            using var http = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sms.ir/v1/send/verify");
            request.Headers.Add("x-api-key", settings.ApiKey);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                string? messageId = null;
                string? lineNumber = null;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var data = doc.RootElement.GetProperty("data");
                    var el = data.GetProperty("messageId");
                    messageId = el.ValueKind == JsonValueKind.Number ? el.GetInt64().ToString() : el.GetString();
                    if (data.TryGetProperty("lineNumber", out var ln))
                        lineNumber = ln.ValueKind == JsonValueKind.Number ? ln.GetInt64().ToString() : ln.GetString();
                }
                catch { }

                // Verify API usually doesn't return lineNumber; fetch it from message status API
                if (messageId != null && lineNumber == null)
                {
                    try
                    {
                        var statusReq = new HttpRequestMessage(HttpMethod.Get, $"https://api.sms.ir/v1/send/{messageId}");
                        statusReq.Headers.Add("x-api-key", settings.ApiKey);
                        var statusRes = await http.SendAsync(statusReq);
                        if (statusRes.IsSuccessStatusCode)
                        {
                            var statusBody = await statusRes.Content.ReadAsStringAsync();
                            using var statusDoc = JsonDocument.Parse(statusBody);
                            if (statusDoc.RootElement.TryGetProperty("data", out var stData) &&
                                stData.TryGetProperty("lineNumber", out var ln2))
                            {
                                lineNumber = ln2.ValueKind == JsonValueKind.Number ? ln2.GetInt64().ToString() : ln2.GetString();
                            }
                        }
                    }
                    catch { }
                }

                credit.Balance -= tariff;
                credit.UpdatedAt = DateTime.UtcNow;

                var log = new SmsLog
                {
                    WorkshopId = workshopId,
                    ModuleType = SmsModuleType.ActivationCode,
                    RecipientNumber = mobile,
                    MessageText = messageText,
                    SendStatus = SmsSendStatus.Sent,
                    Cost = tariff,
                    ProviderMessageId = messageId,
                    DeliveryStatus = SmsDeliveryStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                _db.SmsLogs.Add(log);
                await _db.SaveChangesAsync();

                _logger.LogInformation("OTP sent to {Mobile} via sms.ir, messageId={MessageId}, lineNumber={LineNumber}", mobile, messageId, lineNumber);
                return new OtpResultDto { Success = true, LineNumber = lineNumber };
            }
            else
            {
                _logger.LogError("sms.ir API error: {StatusCode} {Body}", response.StatusCode, body);
                Console.Error.WriteLine($"[SmsService] sms.ir API returned {response.StatusCode}: {body}");

                var log = new SmsLog
                {
                    WorkshopId = workshopId,
                    ModuleType = SmsModuleType.ActivationCode,
                    RecipientNumber = mobile,
                    MessageText = messageText,
                    SendStatus = SmsSendStatus.Failed,
                    ErrorMessage = body.Length > 500 ? body[..500] : body,
                    Cost = 0,
                    DeliveryStatus = SmsDeliveryStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                _db.SmsLogs.Add(log);
                await _db.SaveChangesAsync();

                return Fail("سرویس پیامک بصورت موقت غیرفعال شده است. لطفاً بعداً تلاش کنید یا با پشتیبانی تماس بگیرید.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP to {Mobile}", mobile);
            Console.Error.WriteLine($"[SmsService] Exception for {mobile}: {ex.Message}");

            var log = new SmsLog
            {
                WorkshopId = workshopId,
                ModuleType = SmsModuleType.ActivationCode,
                RecipientNumber = mobile,
                MessageText = messageText,
                SendStatus = SmsSendStatus.Failed,
                ErrorMessage = ex.Message,
                Cost = 0,
                DeliveryStatus = SmsDeliveryStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _db.SmsLogs.Add(log);
            await _db.SaveChangesAsync();

            return Fail("سرویس پیامک بصورت موقت غیرفعال شده است. لطفاً بعداً تلاش کنید یا با پشتیبانی تماس بگیرید.");
        }
    }

    public async Task<SendSmsResultDto> SendTechnicianSmsAsync(int workshopId, string mobile, string technicianName, string workshopName, int templateId)
    {
        var settings = await _db.SmsSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            _logger.LogError("SmsSettings table is empty — no SMS settings configured");
            return new SendSmsResultDto { Success = false, Message = "تنظیمات پیامک پیکربندی نشده است" };
        }
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return new SendSmsResultDto { Success = false, Message = "کلید API پیامک تنظیم نشده است" };

        var tariff = settings.TariffPerSms;
        if (tariff <= 0)
            return new SendSmsResultDto { Success = false, Message = "تعرفه پیامک تنظیم نشده است" };

        var credit = await _db.WorkshopSmsCredits.FirstOrDefaultAsync(c => c.WorkshopId == workshopId);
        if (credit == null)
            return new SendSmsResultDto { Success = false, Message = "اعتبار پیامک برای این کارگاه یافت نشد" };
        if (credit.Balance < tariff)
            return new SendSmsResultDto { Success = false, Message = "اعتبار پیامک کارگاه کافی نیست" };

        var payload = new SmsIrVerifyRequest
        {
            Mobile = mobile,
            TemplateId = templateId,
            Parameters = new[]
            {
                new SmsIrParameter { Name = "TECHNAME", Value = technicianName },
                new SmsIrParameter { Name = "WORKSHOP", Value = workshopName }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        try
        {
            using var http = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sms.ir/v1/send/verify");
            request.Headers.Add("x-api-key", settings.ApiKey);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                string? messageId = null;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var el = doc.RootElement.GetProperty("data").GetProperty("messageId");
                    messageId = el.ValueKind == JsonValueKind.Number ? el.GetInt64().ToString() : el.GetString();
                }
                catch { }

                credit.Balance -= tariff;
                credit.UpdatedAt = DateTime.UtcNow;

                var log = new SmsLog
                {
                    WorkshopId = workshopId,
                    ModuleType = SmsModuleType.NewJobForTechnician,
                    RecipientNumber = mobile,
                    MessageText = $"تکنسین گرامی\n{technicianName}\nسفارش جدیدی در سامانه {workshopName} برای شما ثبت شده است.\nمجموعه فنی و مهندسی ارور سرویس",
                    SendStatus = SmsSendStatus.Sent,
                    Cost = tariff,
                    ProviderMessageId = messageId,
                    DeliveryStatus = SmsDeliveryStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                _db.SmsLogs.Add(log);
                await _db.SaveChangesAsync();

                return new SendSmsResultDto { Success = true, MessageId = messageId };
            }
            else
            {
                var log = new SmsLog
                {
                    WorkshopId = workshopId,
                    ModuleType = SmsModuleType.NewJobForTechnician,
                    RecipientNumber = mobile,
                    MessageText = "",
                    SendStatus = SmsSendStatus.Failed,
                    ErrorMessage = body.Length > 500 ? body[..500] : body,
                    Cost = 0,
                    DeliveryStatus = SmsDeliveryStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                _db.SmsLogs.Add(log);
                await _db.SaveChangesAsync();

                _logger.LogError("sms.ir send-like error for {Mobile}: {StatusCode} {Body}", mobile, response.StatusCode, body);
                return new SendSmsResultDto { Success = false, Message = "ارسال پیامک ناموفق بود" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send technician SMS to {Mobile}", mobile);

            var log = new SmsLog
            {
                WorkshopId = workshopId,
                ModuleType = SmsModuleType.NewJobForTechnician,
                RecipientNumber = mobile,
                MessageText = "",
                SendStatus = SmsSendStatus.Failed,
                ErrorMessage = ex.Message,
                Cost = 0,
                DeliveryStatus = SmsDeliveryStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _db.SmsLogs.Add(log);
            await _db.SaveChangesAsync();

            return new SendSmsResultDto { Success = false, Message = "خطا در ارسال پیامک" };
        }
    }

    public async Task<SendSmsResultDto> SendOrderRegisteredSmsAsync(int workshopId, string mobile, string customerName, string deviceType, string? deviceBrand, string date, string workshopName, string requestId)
    {
        var settings = await _db.SmsSettings.FirstOrDefaultAsync();
        if (settings == null)
            return new SendSmsResultDto { Success = false, Message = "تنظیمات پیامک پیکربندی نشده است" };
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return new SendSmsResultDto { Success = false, Message = "کلید API پیامک تنظیم نشده است" };

        var tariff = settings.TariffPerSms;
        if (tariff <= 0)
            return new SendSmsResultDto { Success = false, Message = "تعرفه پیامک تنظیم نشده است" };

        var credit = await _db.WorkshopSmsCredits.FirstOrDefaultAsync(c => c.WorkshopId == workshopId);
        if (credit == null)
            return new SendSmsResultDto { Success = false, Message = "اعتبار پیامک برای این کارگاه یافت نشد" };
        if (credit.Balance < tariff)
            return new SendSmsResultDto { Success = false, Message = "اعتبار پیامک کارگاه کافی نیست" };

        var payload = new SmsIrVerifyRequest
        {
            Mobile = mobile,
            TemplateId = 357720,
            Parameters = new[]
            {
                new SmsIrParameter { Name = "CUSTOMER", Value = customerName },
                new SmsIrParameter { Name = "TYPE", Value = deviceType },
                new SmsIrParameter { Name = "BRAND", Value = deviceBrand ?? "" },
                new SmsIrParameter { Name = "DATE", Value = date },
                new SmsIrParameter { Name = "WORKSHOP", Value = workshopName },
                new SmsIrParameter { Name = "REQUEST_ID", Value = requestId }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        try
        {
            using var http = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sms.ir/v1/send/verify");
            request.Headers.Add("x-api-key", settings.ApiKey);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                string? messageId = null;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var el = doc.RootElement.GetProperty("data").GetProperty("messageId");
                    messageId = el.ValueKind == JsonValueKind.Number ? el.GetInt64().ToString() : el.GetString();
                }
                catch { }

                credit.Balance -= tariff;
                credit.UpdatedAt = DateTime.UtcNow;

                var log = new SmsLog
                {
                    WorkshopId = workshopId,
                    CustomerReceiptId = int.TryParse(requestId, out var rid) ? rid : null,
                    ModuleType = SmsModuleType.OrderRegistered,
                    RecipientNumber = mobile,
                    MessageText = $"مشتری گرامی\n{customerName}\nدستگاه {deviceType} ({deviceBrand ?? ""}) در تاریخ {date} در {workshopName}\nبا موفقیت ثبت گردید.\nشماره پذیرش: {requestId}\nمجموعه فنی و مهندسی ارور سرویس",
                    SendStatus = SmsSendStatus.Sent,
                    Cost = tariff,
                    ProviderMessageId = messageId,
                    DeliveryStatus = SmsDeliveryStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                _db.SmsLogs.Add(log);
                await _db.SaveChangesAsync();

                return new SendSmsResultDto { Success = true, MessageId = messageId };
            }
            else
            {
                var log = new SmsLog
                {
                    WorkshopId = workshopId,
                    CustomerReceiptId = int.TryParse(requestId, out var rid) ? rid : null,
                    ModuleType = SmsModuleType.OrderRegistered,
                    RecipientNumber = mobile,
                    MessageText = "",
                    SendStatus = SmsSendStatus.Failed,
                    ErrorMessage = body.Length > 500 ? body[..500] : body,
                    Cost = 0,
                    DeliveryStatus = SmsDeliveryStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                _db.SmsLogs.Add(log);
                await _db.SaveChangesAsync();

                _logger.LogError("sms.ir send-order-registered error for {Mobile}: {StatusCode} {Body}", mobile, response.StatusCode, body);
                return new SendSmsResultDto { Success = false, Message = "ارسال پیامک ناموفق بود" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order registered SMS to {Mobile}", mobile);

            var log = new SmsLog
            {
                WorkshopId = workshopId,
                CustomerReceiptId = int.TryParse(requestId, out var rid) ? rid : null,
                ModuleType = SmsModuleType.OrderRegistered,
                RecipientNumber = mobile,
                MessageText = "",
                SendStatus = SmsSendStatus.Failed,
                ErrorMessage = ex.Message,
                Cost = 0,
                DeliveryStatus = SmsDeliveryStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _db.SmsLogs.Add(log);
            await _db.SaveChangesAsync();

            return new SendSmsResultDto { Success = false, Message = "خطا در ارسال پیامک" };
        }
    }

    public async Task<SendSmsResultDto> SendOrderReadyForDeliverySmsAsync(int workshopId, string mobile, string customerName, string deviceType, string? deviceBrand, string date, string workshopName, string state, int? receiptId)
    {
        var settings = await _db.SmsSettings.FirstOrDefaultAsync();
        if (settings == null)
            return new SendSmsResultDto { Success = false, Message = "تنظیمات پیامک پیکربندی نشده است" };
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return new SendSmsResultDto { Success = false, Message = "کلید API پیامک تنظیم نشده است" };

        var tariff = settings.TariffPerSms;
        if (tariff <= 0)
            return new SendSmsResultDto { Success = false, Message = "تعرفه پیامک تنظیم نشده است" };

        var credit = await _db.WorkshopSmsCredits.FirstOrDefaultAsync(c => c.WorkshopId == workshopId);
        if (credit == null)
            return new SendSmsResultDto { Success = false, Message = "اعتبار پیامک برای این کارگاه یافت نشد" };
        if (credit.Balance < tariff)
            return new SendSmsResultDto { Success = false, Message = "اعتبار پیامک کارگاه کافی نیست" };

        var payload = new SmsIrVerifyRequest
        {
            Mobile = mobile,
            TemplateId = 202660,
            Parameters = new[]
            {
                new SmsIrParameter { Name = "CUSTOMER", Value = customerName },
                new SmsIrParameter { Name = "TYPE", Value = deviceType },
                new SmsIrParameter { Name = "BRAND", Value = deviceBrand ?? "" },
                new SmsIrParameter { Name = "DATE", Value = date },
                new SmsIrParameter { Name = "WORKSHOP", Value = workshopName },
                new SmsIrParameter { Name = "STATE", Value = state }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        try
        {
            using var http = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sms.ir/v1/send/verify");
            request.Headers.Add("x-api-key", settings.ApiKey);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                string? messageId = null;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var el = doc.RootElement.GetProperty("data").GetProperty("messageId");
                    messageId = el.ValueKind == JsonValueKind.Number ? el.GetInt64().ToString() : el.GetString();
                }
                catch { }

                credit.Balance -= tariff;
                credit.UpdatedAt = DateTime.UtcNow;

                var log = new SmsLog
                {
                    WorkshopId = workshopId,
                    CustomerReceiptId = receiptId,
                    ModuleType = SmsModuleType.OrderReadyForDelivery,
                    RecipientNumber = mobile,
                    MessageText = $"مشتری گرامی\n{customerName}\nدستگاه {deviceType} ({deviceBrand ?? ""}) در تاریخ {date} در {workshopName}\n{state} می باشد.\nلطفا برای تحویل مراجعه بفرمایید.\nمجموعه فنی و مهندسی ارور سرویس",
                    SendStatus = SmsSendStatus.Sent,
                    Cost = tariff,
                    ProviderMessageId = messageId,
                    DeliveryStatus = SmsDeliveryStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                _db.SmsLogs.Add(log);
                await _db.SaveChangesAsync();

                return new SendSmsResultDto { Success = true, MessageId = messageId };
            }
            else
            {
                var log = new SmsLog
                {
                    WorkshopId = workshopId,
                    CustomerReceiptId = receiptId,
                    ModuleType = SmsModuleType.OrderReadyForDelivery,
                    RecipientNumber = mobile,
                    MessageText = "",
                    SendStatus = SmsSendStatus.Failed,
                    ErrorMessage = body.Length > 500 ? body[..500] : body,
                    Cost = 0,
                    DeliveryStatus = SmsDeliveryStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                _db.SmsLogs.Add(log);
                await _db.SaveChangesAsync();

                _logger.LogError("sms.ir send-ready-for-delivery error for {Mobile}: {StatusCode} {Body}", mobile, response.StatusCode, body);
                return new SendSmsResultDto { Success = false, Message = "ارسال پیامک ناموفق بود" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ready-for-delivery SMS to {Mobile}", mobile);

            var log = new SmsLog
            {
                WorkshopId = workshopId,
                CustomerReceiptId = receiptId,
                ModuleType = SmsModuleType.OrderReadyForDelivery,
                RecipientNumber = mobile,
                MessageText = "",
                SendStatus = SmsSendStatus.Failed,
                ErrorMessage = ex.Message,
                Cost = 0,
                DeliveryStatus = SmsDeliveryStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _db.SmsLogs.Add(log);
            await _db.SaveChangesAsync();

            return new SendSmsResultDto { Success = false, Message = "خطا در ارسال پیامک" };
        }
    }

    public async Task<SendSmsResultDto> SendOrderUnrepairableSmsAsync(int workshopId, string mobile, string customerName, string deviceType, string? deviceBrand, string date, string workshopName, string state, int? receiptId)
    {
        var settings = await _db.SmsSettings.FirstOrDefaultAsync();
        if (settings == null)
            return new SendSmsResultDto { Success = false, Message = "تنظیمات پیامک پیکربندی نشده است" };
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return new SendSmsResultDto { Success = false, Message = "کلید API پیامک تنظیم نشده است" };

        var tariff = settings.TariffPerSms;
        if (tariff <= 0)
            return new SendSmsResultDto { Success = false, Message = "تعرفه پیامک تنظیم نشده است" };

        var credit = await _db.WorkshopSmsCredits.FirstOrDefaultAsync(c => c.WorkshopId == workshopId);
        if (credit == null)
            return new SendSmsResultDto { Success = false, Message = "اعتبار پیامک برای این کارگاه یافت نشد" };
        if (credit.Balance < tariff)
            return new SendSmsResultDto { Success = false, Message = "اعتبار پیامک کارگاه کافی نیست" };

        var payload = new SmsIrVerifyRequest
        {
            Mobile = mobile,
            TemplateId = 202660,
            Parameters = new[]
            {
                new SmsIrParameter { Name = "CUSTOMER", Value = customerName },
                new SmsIrParameter { Name = "TYPE", Value = deviceType },
                new SmsIrParameter { Name = "BRAND", Value = deviceBrand ?? "" },
                new SmsIrParameter { Name = "DATE", Value = date },
                new SmsIrParameter { Name = "WORKSHOP", Value = workshopName },
                new SmsIrParameter { Name = "STATE", Value = state }
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        try
        {
            using var http = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sms.ir/v1/send/verify");
            request.Headers.Add("x-api-key", settings.ApiKey);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                string? messageId = null;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var el = doc.RootElement.GetProperty("data").GetProperty("messageId");
                    messageId = el.ValueKind == JsonValueKind.Number ? el.GetInt64().ToString() : el.GetString();
                }
                catch { }

                credit.Balance -= tariff;
                credit.UpdatedAt = DateTime.UtcNow;

                var log = new SmsLog
                {
                    WorkshopId = workshopId,
                    CustomerReceiptId = receiptId,
                    ModuleType = SmsModuleType.OrderUnrepairable,
                    RecipientNumber = mobile,
                    MessageText = $"مشتری گرامی\n{customerName}\nدستگاه {deviceType} ({deviceBrand ?? ""}) در تاریخ {date} در {workshopName}\n{state} می باشد.\nلطفا برای تحویل مراجعه بفرمایید.\nمجموعه فنی و مهندسی ارور سرویس",
                    SendStatus = SmsSendStatus.Sent,
                    Cost = tariff,
                    ProviderMessageId = messageId,
                    DeliveryStatus = SmsDeliveryStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                _db.SmsLogs.Add(log);
                await _db.SaveChangesAsync();

                return new SendSmsResultDto { Success = true, MessageId = messageId };
            }
            else
            {
                var log = new SmsLog
                {
                    WorkshopId = workshopId,
                    CustomerReceiptId = receiptId,
                    ModuleType = SmsModuleType.OrderUnrepairable,
                    RecipientNumber = mobile,
                    MessageText = "",
                    SendStatus = SmsSendStatus.Failed,
                    ErrorMessage = body.Length > 500 ? body[..500] : body,
                    Cost = 0,
                    DeliveryStatus = SmsDeliveryStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                _db.SmsLogs.Add(log);
                await _db.SaveChangesAsync();

                _logger.LogError("sms.ir send-unrepairable error for {Mobile}: {StatusCode} {Body}", mobile, response.StatusCode, body);
                return new SendSmsResultDto { Success = false, Message = "ارسال پیامک ناموفق بود" };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send unrepairable SMS to {Mobile}", mobile);

            var log = new SmsLog
            {
                WorkshopId = workshopId,
                CustomerReceiptId = receiptId,
                ModuleType = SmsModuleType.OrderUnrepairable,
                RecipientNumber = mobile,
                MessageText = "",
                SendStatus = SmsSendStatus.Failed,
                ErrorMessage = ex.Message,
                Cost = 0,
                DeliveryStatus = SmsDeliveryStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _db.SmsLogs.Add(log);
            await _db.SaveChangesAsync();

            return new SendSmsResultDto { Success = false, Message = "خطا در ارسال پیامک" };
        }
    }

    public async Task<DeliveryCheckResultDto> CheckDeliveryStatusAsync(string messageId, string apiKey)
    {
        try
        {
            using var http = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.sms.ir/v1/send/{messageId}");
            request.Headers.Add("x-api-key", apiKey);

            var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new DeliveryCheckResultDto { Success = false, Message = body.Length > 500 ? body[..500] : body };

            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.GetProperty("status").GetInt32();
            if (status != 1)
                return new DeliveryCheckResultDto { Success = false, Message = $"sms.ir status: {status}" };

            var data = doc.RootElement.GetProperty("data");
            byte? deliveryState = null;
            if (data.TryGetProperty("deliveryState", out var ds) && ds.ValueKind == JsonValueKind.Number)
                deliveryState = ds.GetByte();

            DateTime? deliveryDateTime = null;
            if (data.TryGetProperty("deliveryDateTime", out var ddt) && ddt.ValueKind == JsonValueKind.Number)
            {
                var unix = ddt.GetInt64();
                if (unix > 0)
                    deliveryDateTime = DateTimeOffset.FromUnixTimeSeconds(unix).DateTime;
            }

            return new DeliveryCheckResultDto
            {
                Success = true,
                DeliveryState = deliveryState,
                DeliveryDateTime = deliveryDateTime,
                ProviderRawStatus = deliveryState?.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check delivery status for messageId={MessageId}", messageId);
            return new DeliveryCheckResultDto { Success = false, Message = ex.Message };
        }
    }

    private static OtpResultDto Fail(string message) => new() { Success = false, Message = message };
}

file sealed class SmsIrVerifyRequest
{
    [JsonPropertyName("mobile")]
    public string Mobile { get; set; } = string.Empty;

    [JsonPropertyName("templateId")]
    public int TemplateId { get; set; }

    [JsonPropertyName("parameters")]
    public SmsIrParameter[] Parameters { get; set; } = Array.Empty<SmsIrParameter>();
}

file sealed class SmsIrParameter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
