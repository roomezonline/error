using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using System.Diagnostics;

namespace ErrorService.Server.Infrastructure;

/// <summary>
/// Middleware حرفه‌ای برای ردیابی خطاها و عملکرد سیستم - چشمانی قوی برای ادمین
/// </summary>
public class SystemEventMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SystemEventMiddleware> _logger;

    public SystemEventMiddleware(RequestDelegate next, ILogger<SystemEventMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, SystemEventService eventService)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path.Value;
        var method = context.Request.Method;

        try
        {
            await _next(context);
            stopwatch.Stop();

            // ردیابی عملکردهای واقعاً بحرانی (بیش از 10 ثانیه)
            // با توجه به استفاده از فیلترشکن، آستانه را بالا بردیم تا لاگ‌های غیرضروری ثبت نشوند
            if (stopwatch.ElapsedMilliseconds > 10000 && !requestPath.Contains("_framework"))
            {
                await eventService.LogEventAsync(new SystemEventLog
                {
                    Severity = EventSeverity.Warning,
                    Category = EventCategory.Performance,
                    PersianTitle = "کندی شدید در پاسخ‌دهی (بحرانی)",
                    PersianDescription = $"درخواست به مسیر {requestPath} بیش از ۱۰ ثانیه طول کشید. این میزان کندی حتی با فیلترشکن هم غیرعادی است.",
                    TechnicalTitle = "Critical Slow Response",
                    TechnicalDetails = $"Execution took {stopwatch.ElapsedMilliseconds}ms",
                    RequestPath = requestPath,
                    HttpMethod = method,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    ResponseStatusCode = context.Response.StatusCode,
                    ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.Request.Headers["User-Agent"]
                });
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // ثبت خطای بحرانی در دیتابیس با جزئیات کامل
            await eventService.LogEventAsync(new SystemEventLog
            {
                Severity = EventSeverity.Critical,
                Category = EventCategory.System,
                PersianTitle = "خطای غیرمنتظره در سرور",
                PersianDescription = "یک خطای داخلی در حین پردازش درخواست رخ داد که نیاز به بررسی فوری دارد.",
                TechnicalTitle = ex.Message,
                TechnicalDetails = ex.ToString(),
                StackTrace = ex.StackTrace,
                ErrorCode = ex.GetType().Name,
                RequestPath = requestPath,
                HttpMethod = method,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                ResponseStatusCode = 500,
                ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers["User-Agent"],
                UserId = GetUserId(context),
                UserName = context.User?.Identity?.Name
            });

            // بازنشر خطا برای Handle شدن توسط ExceptionHandler استاندارد
            throw;
        }
    }

    private int? GetUserId(HttpContext context)
    {
        var claim = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return int.TryParse(claim?.Value, out var id) ? id : null;
    }
}

public static class SystemEventMiddlewareExtensions
{
    public static IApplicationBuilder UseSystemEventTracker(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SystemEventMiddleware>();
    }
}
