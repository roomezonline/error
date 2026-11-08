using ErrorService.Shared;

namespace ErrorService.Server.Services.ContentStudio;

/// <summary>
/// هماهنگ‌کننده کارهای سنگین استودیو (اجرای پایپ‌لاین / بازنویسی یک آیتم):
/// فقط یک کار هم‌زمان، و گزارش پیشرفت زنده برای پنل ادمین.
/// </summary>
public sealed class ContentStudioRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContentStudioRunner> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private volatile bool _running;
    private ContentStudioProgressDto _progress = new();
    private DateTimeOffset? _startedAt;

    public ContentStudioRunner(IServiceScopeFactory scopeFactory, ILogger<ContentStudioRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool IsRunning => _running;

    public ContentStudioProgressDto GetProgress() => new()
    {
        IsRunning = _running,
        Trigger = _progress.Trigger,
        Stage = _progress.Stage,
        CurrentSource = _progress.CurrentSource,
        Processed = _progress.Processed,
        Total = _progress.Total,
        StartedAt = _progress.StartedAt
    };

    public bool TryStart(string trigger, int? sourceId = null)
        => TryStartJob(trigger, "در حال آماده‌سازی", service => service.RunAsync(trigger, sourceId, UpdateProgress, CancellationToken.None));

    public bool TryStartRewrite(int itemId)
        => TryStartJob("rewrite", "بازنویسی مجدد آیتم", service => service.RewriteItemAsync(itemId, CancellationToken.None));

    private bool TryStartJob(string trigger, string initialStage, Func<ContentStudioService, Task> work)
    {
        if (!_gate.Wait(0)) return false;

        _running = true;
        _startedAt = DateTimeOffset.UtcNow;
        _progress = new ContentStudioProgressDto
        {
            IsRunning = true,
            Trigger = trigger,
            Stage = initialStage,
            StartedAt = _startedAt
        };

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ContentStudioService>();
                await work(service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ContentStudio runner crashed (trigger={Trigger})", trigger);
            }
            finally
            {
                _running = false;
                _progress = new ContentStudioProgressDto
                {
                    IsRunning = false,
                    Trigger = trigger,
                    Stage = "پایان",
                    Processed = _progress.Processed,
                    Total = _progress.Total,
                    StartedAt = _startedAt
                };
                _startedAt = null;
                _gate.Release();
            }
        });

        return true;
    }

    private void UpdateProgress(ContentStudioProgressDto progress)
    {
        progress.IsRunning = _running;
        progress.StartedAt = _startedAt;
        _progress = progress;
    }
}
