using ErrorService.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Services;

/// <summary>
/// حذف دوره‌ای رویدادهای صوتی کهنه تا جدول DeviceVoiceEvents کوچک بماند.
/// </summary>
public sealed class DeviceVoiceEventCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeviceVoiceEventCleanupService> _logger;

    public DeviceVoiceEventCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeviceVoiceEventCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
                var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
                var removed = await db.DeviceVoiceEvents
                    .Where(x => x.CreatedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                if (removed > 0)
                    _logger.LogInformation("Removed {Count} expired device voice events", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Device voice event cleanup failed");
            }
        }
    }
}
