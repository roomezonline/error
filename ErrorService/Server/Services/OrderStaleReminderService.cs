using ErrorService.Server.Data;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

/// <summary>
/// یادآوری روزانهٔ سفارش‌های تکمیل‌نشده برای سوپر ادمین‌ها.
/// هر سفارشی که بیش از ۳ روز از آخرین تغییر وضعیت/مرحله‌اش گذشته باشد،
/// روزی یک بار (در مرکز اعلان‌ها و بله) یادآوری می‌شود. با هر تغییر وضعیت،
/// شمارش از نو شروع می‌شود و با رسیدن سفارش به «تکمیل شده» یا «حمل شده»
/// یادآوری متوقف می‌شود.
/// </summary>
public sealed class OrderStaleReminderService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(3);

    private static readonly OrderStatus[] OpenStatuses =
    {
        OrderStatus.PendingPayment,
        OrderStatus.ReceiptUploaded,
        OrderStatus.Approved
    };

    private readonly IServiceProvider _services;
    private readonly ILogger<OrderStaleReminderService> _logger;

    public OrderStaleReminderService(IServiceProvider services, ILogger<OrderStaleReminderService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Order stale reminder tick failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<NotificationEventService>();

        var now = DateTimeOffset.UtcNow;

        // فقط سفارش‌های باز (تکمیل‌نشده و حمل‌نشده) مشمول یادآوری هستند.
        var orders = await db.Orders
            .Include(o => o.Items)
            .Where(o => OpenStatuses.Contains(o.Status)
                && (o.Stage == null || o.Stage != OrderStage.Resolved))
            .ToListAsync(ct);

        if (orders.Count == 0)
            return;

        var orderIds = orders.Select(o => o.Id).ToList();
        var changeEvents = await db.OrderEvents.AsNoTracking()
            .Where(e => orderIds.Contains(e.OrderId)
                && (e.EventType == "status_changed" || e.EventType == "stage_changed"))
            .Select(e => new { e.OrderId, e.CreatedAt })
            .ToListAsync(ct);

        var lastChanges = changeEvents
            .GroupBy(e => e.OrderId)
            .ToDictionary(g => g.Key, g => g.Max(e => e.CreatedAt));

        var notified = 0;

        foreach (var order in orders)
        {
            var lastChangeAt = lastChanges.TryGetValue(order.Id, out var at) ? at : order.CreatedAt;
            var elapsed = now - lastChangeAt;
            if (elapsed < StaleAfter)
                continue;

            var staleDays = Math.Max(3, (int)elapsed.TotalDays);
            await notifier.NotifyOrderStaleAsync(order, lastChangeAt, staleDays);
            notified++;
        }

        if (notified > 0)
        {
            // EmitAsync فقط موجودیت‌ها را به Context اضافه می‌کند؛ ذخیره در اینجا
            // انجام می‌شود و کلید یکتایی اعلان از ارسال دوباره در همان روز جلوگیری می‌کند.
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Order stale reminder: queued {Count} reminder(s).", notified);
        }
    }
}
