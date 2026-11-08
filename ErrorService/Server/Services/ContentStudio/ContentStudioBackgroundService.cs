using ErrorService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services.ContentStudio;

/// <summary>
/// اجرای روزانه پایپ‌لاین در ساعت تنظیم‌شده (به وقت تهران) + پاک‌سازی دوره‌ای.
/// </summary>
public sealed class ContentStudioBackgroundService : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(12);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ContentStudioRunner _runner;
    private readonly ILogger<ContentStudioBackgroundService> _logger;
    private DateTimeOffset _lastCleanupAt = DateTimeOffset.MinValue;

    public ContentStudioBackgroundService(
        IServiceScopeFactory scopeFactory,
        ContentStudioRunner runner,
        ILogger<ContentStudioBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _runner = runner;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ContentStudio background tick failed");
            }

            await Task.Delay(Tick, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ContentStudioService>();

        await MaybeCleanupAsync(service, ct);

        var settings = await db.ContentStudioSettings.AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(ct);
        if (settings is null || !settings.Enabled) return;

        var now = ContentStudioClock.Now();
        if (now.Hour < Math.Clamp(settings.DailyRunHour, 0, 23)) return;

        var today = ContentStudioClock.TodayKey();
        if (settings.LastScheduledRunDate == today && settings.ScheduledRunAttempts >= 2) return;
        if (_runner.IsRunning) return;

        var tracked = await db.ContentStudioSettings.FirstAsync(x => x.Id == settings.Id, ct);
        if (tracked.LastScheduledRunDate != today)
        {
            tracked.LastScheduledRunDate = today;
            tracked.ScheduledRunAttempts = 0;
        }
        tracked.ScheduledRunAttempts++;
        tracked.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        if (_runner.TryStart("schedule"))
            _logger.LogInformation("ContentStudio scheduled run started (attempt {Attempt})", tracked.ScheduledRunAttempts);
    }

    private async Task MaybeCleanupAsync(ContentStudioService service, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastCleanupAt < CleanupInterval) return;
        _lastCleanupAt = now;

        try
        {
            await service.CleanupAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ContentStudio cleanup failed");
        }
    }
}
