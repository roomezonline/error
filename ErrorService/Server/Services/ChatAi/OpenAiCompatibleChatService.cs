using ErrorService.Server.Data;
using Microsoft.EntityFrameworkCore;
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

    public async Task<string?> GetReplyAsync(string userMessage, string operatorName)
    {
        try
        {
            var settings = await _db.SiteSettings.FirstOrDefaultAsync();
            if (settings?.ChatEnableAiAssistant != true) return null;
            if (string.IsNullOrWhiteSpace(settings.ChatAiApiUrl) || string.IsNullOrWhiteSpace(settings.ChatAiModel))
                return null;

            var baseUrl = settings.ChatAiApiUrl.TrimEnd('/');
            var systemPrompt = !string.IsNullOrWhiteSpace(settings.ChatAiSystemPrompt)
                ? settings.ChatAiSystemPrompt
                : "تو دستیار پشتیبانی یک فروشگاه ایرانی هستی. کوتاه، مؤدب و مفید پاسخ بده.";

            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            if (!string.IsNullOrWhiteSpace(settings.ChatAiApiKey))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ChatAiApiKey.Trim());

            var payload = new
            {
                model = settings.ChatAiModel,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                temperature = 0.6,
                max_tokens = 400
            };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAiCompatible: HTTP {Status} from {Url}", (int)response.StatusCode, baseUrl);
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return null;

            var content = choices[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAiCompatibleChatService: call failed");
            return null;
        }
    }
}