using ErrorService.Shared;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace ErrorService.Client.Services;

public class ClientErrorLogger : IHandleAfterRender
{
    private readonly HttpClient _http;
    private readonly NavigationManager _nav;

    public ClientErrorLogger(HttpClient http, NavigationManager nav)
    {
        _http = http;
        _nav = nav;
    }

    public async Task LogErrorAsync(Exception ex, string? componentName = null)
    {
        try
        {
            var log = new
            {
                Severity = EventSeverity.Error,
                Category = EventCategory.Frontend,
                PersianTitle = "خطای کلاینت (فرانت‌اند)",
                PersianDescription = $"یک خطا در بخش کاربری سایت رخ داد. کامپوننت: {componentName ?? "نامشخص"}",
                TechnicalTitle = ex.Message,
                TechnicalDetails = ex.ToString(),
                StackTrace = ex.StackTrace,
                ErrorCode = ex.GetType().Name,
                RequestPath = _nav.Uri,
                HttpMethod = "CLIENT",
                Component = componentName
            };

            await _http.PostAsJsonAsync("api/systemevents/client-log", log);
        }
        catch
        {
            // Fail silently to avoid infinite error loops
            Console.WriteLine("Failed to log client error to server.");
        }
    }

    public Task OnAfterRenderAsync() => Task.CompletedTask;
}
