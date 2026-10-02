using ErrorService.Server.Data;
using ErrorService.Shared;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ErrorService.Server.Services;

/// <summary>
/// ÃƒËœÃ‚Â§ÃƒËœÃ‚Â·Ãƒâ„¢Ã¢â‚¬Å¾ÃƒËœÃ‚Â§ÃƒËœÃ‚Â¹ÃƒËœÃ‚Â§ÃƒËœÃ‚Âª Ãƒâ„¢Ã‚ÂÃƒËœÃ‚Â¶ÃƒËœÃ‚Â§Ãƒâ€ºÃ…â€™ Ãƒâ„¢Ã¢â‚¬Â¡ÃƒËœÃ‚Â§ÃƒËœÃ‚Â³ÃƒËœÃ‚Âª: ÃƒËœÃ‚Â¯Ãƒâ€ºÃ…â€™ÃƒËœÃ‚Â³ÃƒÅ¡Ã‚Â©ÃƒËœÃ…â€™ Ãƒâ„¢Ã‚ÂÃƒËœÃ‚Â§Ãƒâ€ºÃ…â€™Ãƒâ„¢Ã¢â‚¬Å¾ÃƒÂ¢Ã¢â€šÂ¬Ã…â€™Ãƒâ„¢Ã¢â‚¬Â¡ÃƒËœÃ‚Â§Ãƒâ€ºÃ…â€™ wwwroot Ãƒâ„¢Ã‹â€  ÃƒËœÃ‚Â­ÃƒËœÃ‚Â¬Ãƒâ„¢Ã¢â‚¬Â¦ ÃƒËœÃ‚Â¨ÃƒËœÃ‚Â§Ãƒâ„¢Ã¢â‚¬Â ÃƒÅ¡Ã‚Â© ÃƒËœÃ‚Â§ÃƒËœÃ‚Â·Ãƒâ„¢Ã¢â‚¬Å¾ÃƒËœÃ‚Â§ÃƒËœÃ‚Â¹ÃƒËœÃ‚Â§ÃƒËœÃ‚ÂªÃƒâ€ºÃ…â€™.
/// ÃƒËœÃ‚Â§ÃƒËœÃ‚Â³ÃƒÅ¡Ã‚Â©Ãƒâ„¢Ã¢â‚¬Â  Ãƒâ„¢Ã‚Â¾Ãƒâ„¢Ã‹â€ ÃƒËœÃ‚Â´Ãƒâ„¢Ã¢â‚¬Â¡ÃƒÂ¢Ã¢â€šÂ¬Ã…â€™Ãƒâ„¢Ã¢â‚¬Â¡ÃƒËœÃ‚Â§ ÃƒËœÃ‚Â¯ÃƒËœÃ‚Â± Ãƒâ„¢Ã‚Â¾ÃƒËœÃ‚Â³ÃƒÂ¢Ã¢â€šÂ¬Ã…â€™ÃƒËœÃ‚Â²Ãƒâ„¢Ã¢â‚¬Â¦Ãƒâ€ºÃ…â€™Ãƒâ„¢Ã¢â‚¬Â Ãƒâ„¢Ã¢â‚¬Â¡ ÃƒËœÃ‚Â§Ãƒâ„¢Ã¢â‚¬Â ÃƒËœÃ‚Â¬ÃƒËœÃ‚Â§Ãƒâ„¢Ã¢â‚¬Â¦ Ãƒâ„¢Ã‹â€  Ãƒâ„¢Ã¢â‚¬Â ÃƒËœÃ‚ÂªÃƒâ€ºÃ…â€™ÃƒËœÃ‚Â¬Ãƒâ„¢Ã¢â‚¬Â¡ ÃƒÅ¡Ã‚Â©ÃƒËœÃ‚Â´ Ãƒâ„¢Ã¢â‚¬Â¦Ãƒâ€ºÃ…â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…â€™ÃƒËœÃ‚Â´Ãƒâ„¢Ã‹â€ ÃƒËœÃ‚Â¯ ÃƒËœÃ‚ÂªÃƒËœÃ‚Â§ ÃƒËœÃ‚Â¯ÃƒËœÃ‚Â±ÃƒËœÃ‚Â®Ãƒâ„¢Ã‹â€ ÃƒËœÃ‚Â§ÃƒËœÃ‚Â³ÃƒËœÃ‚Âª Ãƒâ„¢Ã¢â‚¬Â¡ÃƒËœÃ‚Â±ÃƒÅ¡Ã‚Â¯ÃƒËœÃ‚Â² Ãƒâ„¢Ã¢â‚¬Â¦ÃƒËœÃ‚Â¹ÃƒËœÃ‚Â·Ãƒâ„¢Ã¢â‚¬Å¾ Ãƒâ„¢Ã¢â‚¬Â Ãƒâ„¢Ã¢â‚¬Â¦ÃƒËœÃ‚Â§Ãƒâ„¢Ã¢â‚¬Â ÃƒËœÃ‚Â¯.
/// </summary>
public sealed class HostInfoService : BackgroundService
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DbCacheInterval = TimeSpan.FromSeconds(60);

    private readonly IWebHostEnvironment _env;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<HostInfoService> _logger;
    private readonly SemaphoreSlim _computeLock = new(1, 1);

    private HostInfoDto? _cache;
    private HostDatabaseDto? _dbCache;
    private DateTime _dbCacheAt = DateTime.MinValue;
    private DateTime _scannedAt = DateTime.MinValue;
    private bool _scanning;
    private static readonly DateTime _startedAt = DateTime.UtcNow;

    public HostInfoService(IWebHostEnvironment env, IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<HostInfoService> logger)
    {
        _env = env;
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    public HostInfoDto GetSnapshot()
    {
        var dto = _cache ?? new HostInfoDto();
        return new HostInfoDto
        {
            GeneratedAt = dto.GeneratedAt,
            Refreshing = _scanning || _cache == null,
            Disk = dto.Disk,
            Files = dto.Files,
            Database = dto.Database,
            Process = BuildProcessInfo()
        };
    }

    /// <summary>ÃƒËœÃ‚Â¯ÃƒËœÃ‚Â±ÃƒËœÃ‚Â®Ãƒâ„¢Ã‹â€ ÃƒËœÃ‚Â§ÃƒËœÃ‚Â³ÃƒËœÃ‚Âª ÃƒËœÃ‚Â¨ÃƒËœÃ‚Â±Ãƒâ„¢Ã‹â€ ÃƒËœÃ‚Â²ÃƒËœÃ‚Â±ÃƒËœÃ‚Â³ÃƒËœÃ‚Â§Ãƒâ„¢Ã¢â‚¬Â Ãƒâ€ºÃ…â€™ Ãƒâ„¢Ã‚ÂÃƒâ„¢Ã‹â€ ÃƒËœÃ‚Â±Ãƒâ€ºÃ…â€™ (ÃƒËœÃ‚Â¯ÃƒÅ¡Ã‚Â©Ãƒâ„¢Ã¢â‚¬Â¦Ãƒâ„¢Ã¢â‚¬Â¡ Refresh ÃƒËœÃ‚Â¯ÃƒËœÃ‚Â± UI).</summary>
    public async Task RefreshAsync()
    {
        await ComputeAsync(force: true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HostInfoService is running");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ComputeAsync(force: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HostInfoService scan failed");
            }

            try { await Task.Delay(ScanInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ComputeAsync(bool force)
    {
        if (!force && _cache != null && DateTime.UtcNow - _scannedAt < ScanInterval) return;
        if (!await _computeLock.WaitAsync(0)) return;

        try
        {
            _scanning = true;
            var files = await Task.Run(ScanFiles);
            var db = await GetDatabaseInfoAsync(force);
            var disk = BuildDiskInfo(files, db);

            _cache = new HostInfoDto
            {
                GeneratedAt = DateTime.UtcNow,
                Disk = disk,
                Files = files,
                Database = db,
                Process = BuildProcessInfo()
            };
            _scannedAt = DateTime.UtcNow;
        }
        finally
        {
            _scanning = false;
            _computeLock.Release();
        }
    }

    private HostFilesDto ScanFiles()
    {
        var result = ScanWwwRoot();
        var appTotal = ScanFolderTotal(_env.ContentRootPath);
        result.AppTotalMb = Math.Round(Math.Max(appTotal / 1048576.0, result.TotalMb), 2);
        return result;
    }

    /// <summary>
    /// ÃƒËœÃ‚Â³Ãƒâ„¢Ã¢â‚¬Â¡Ãƒâ„¢Ã¢â‚¬Â¦Ãƒâ€ºÃ…â€™Ãƒâ„¢Ã¢â‚¬Â¡ Ãƒâ„¢Ã‚ÂÃƒËœÃ‚Â¶ÃƒËœÃ‚Â§Ãƒâ€ºÃ…â€™ Ãƒâ„¢Ã¢â‚¬Â¡ÃƒËœÃ‚Â§ÃƒËœÃ‚Â³ÃƒËœÃ‚Âª ÃƒËœÃ‚Â§ÃƒËœÃ‚Â² ÃƒËœÃ‚ÂªÃƒâ„¢Ã¢â‚¬Â ÃƒËœÃ‚Â¸Ãƒâ€ºÃ…â€™Ãƒâ„¢Ã¢â‚¬Â¦ÃƒËœÃ‚Â§ÃƒËœÃ‚Âª HostInfo:QuotaGb ÃƒËœÃ‚Â®Ãƒâ„¢Ã‹â€ ÃƒËœÃ‚Â§Ãƒâ„¢Ã¢â‚¬Â ÃƒËœÃ‚Â¯Ãƒâ„¢Ã¢â‚¬Â¡ Ãƒâ„¢Ã¢â‚¬Â¦Ãƒâ€ºÃ…â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…â€™ÃƒËœÃ‚Â´Ãƒâ„¢Ã‹â€ ÃƒËœÃ‚Â¯.
    /// ÃƒËœÃ‚Â§ÃƒÅ¡Ã‚Â¯ÃƒËœÃ‚Â± ÃƒËœÃ‚ÂªÃƒâ„¢Ã¢â‚¬Â ÃƒËœÃ‚Â¸Ãƒâ€ºÃ…â€™Ãƒâ„¢Ã¢â‚¬Â¦ Ãƒâ„¢Ã¢â‚¬Â ÃƒËœÃ‚Â´ÃƒËœÃ‚Â¯Ãƒâ„¢Ã¢â‚¬Â¡ ÃƒËœÃ‚Â¨ÃƒËœÃ‚Â§ÃƒËœÃ‚Â´ÃƒËœÃ‚Â¯ (ÃƒËœÃ‚ÂµÃƒâ„¢Ã‚ÂÃƒËœÃ‚Â±/ÃƒËœÃ‚Â®ÃƒËœÃ‚Â§Ãƒâ„¢Ã¢â‚¬Å¾Ãƒâ€ºÃ…â€™) Ãƒâ„¢Ã¢â‚¬Â¦ÃƒËœÃ‚Â­ÃƒËœÃ‚Â§ÃƒËœÃ‚Â³ÃƒËœÃ‚Â¨ÃƒËœÃ‚Â§ÃƒËœÃ‚Âª Ãƒâ„¢Ã¢â‚¬Â¦ÃƒËœÃ‚Â«Ãƒâ„¢Ã¢â‚¬Å¾ Ãƒâ„¢Ã¢â‚¬Å¡ÃƒËœÃ‚Â¨Ãƒâ„¢Ã¢â‚¬Å¾ ÃƒËœÃ‚Â±Ãƒâ„¢Ã‹â€ Ãƒâ€ºÃ…â€™ ÃƒÅ¡Ã‚Â©Ãƒâ„¢Ã¢â‚¬Å¾ ÃƒËœÃ‚Â¯ÃƒËœÃ‚Â±ÃƒËœÃ‚Â§Ãƒâ€ºÃ…â€™Ãƒâ„¢Ã‹â€  ÃƒËœÃ‚Â³ÃƒËœÃ‚Â±Ãƒâ„¢Ã‹â€ ÃƒËœÃ‚Â± ÃƒËœÃ‚Â§Ãƒâ„¢Ã¢â‚¬Â ÃƒËœÃ‚Â¬ÃƒËœÃ‚Â§Ãƒâ„¢Ã¢â‚¬Â¦ Ãƒâ„¢Ã¢â‚¬Â¦Ãƒâ€ºÃ…â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…â€™ÃƒËœÃ‚Â´Ãƒâ„¢Ã‹â€ ÃƒËœÃ‚Â¯.
    /// </summary>
    private double ReadQuotaGb()
    {
        var raw = _config["HostInfo:QuotaGb"];
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var quota)
            ? Math.Round(Math.Max(quota, 0), 2)
            : 0;
    }

    private HostDiskInfoDto BuildDiskInfo(HostFilesDto files, HostDatabaseDto db)
    {
        double physicalTotal = 0, physicalFree = 0;
        var physicalVolume = "";
        var physicalAvailable = false;

        try
        {
            var root = Path.GetPathRoot(_env.ContentRootPath);
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new System.IO.DriveInfo(root);
                physicalTotal = drive.TotalSize / 1073741824.0;
                physicalFree = drive.AvailableFreeSpace / 1073741824.0;
                physicalVolume = root;
                physicalAvailable = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read disk info");
        }

        var physical = new HostDiskInfoDto
        {
            Available = physicalAvailable,
            Volume = physicalVolume,
            TotalGb = Math.Round(physicalTotal, 2),
            UsedGb = Math.Round(physicalTotal - physicalFree, 2),
            UsedMb = Math.Round((physicalTotal - physicalFree) * 1024, 2),
            FreeGb = Math.Round(physicalFree, 2),
            UsedPercent = physicalTotal > 0 ? Math.Round((physicalTotal - physicalFree) / physicalTotal * 100, 1) : 0
        };

        var quotaGb = ReadQuotaGb();
        if (quotaGb <= 0) return physical;

        var usedMb = files.AppTotalMb + db.TotalMb;
        var usedGb = Math.Min(Math.Round(usedMb / 1024.0, 2), quotaGb);

        return new HostDiskInfoDto
        {
            Available = true,
            QuotaBased = true,
            QuotaGb = quotaGb,
            Volume = physicalVolume,
            TotalGb = quotaGb,
            UsedGb = usedGb,
            UsedMb = Math.Round(usedMb, 2),
            FreeGb = Math.Round(quotaGb - usedGb, 2),
            UsedPercent = Math.Round(usedGb / quotaGb * 100, 1),
            PhysicalVolume = physicalVolume,
            PhysicalTotalGb = physical.TotalGb,
            PhysicalFreeGb = physical.FreeGb
        };
    }

    private double ScanFolderTotal(string root)
    {
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return 0;

        // Count every file under the deployed application root. The host analyzer\n        // must reflect real occupied storage, including runtime files, logs,\n        // uploads, backups and build/publish artifacts when they exist.
        var total = 0d;
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    total += SafeFileLength(file);
                }

                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    stack.Push(dir);
                }
            }
            catch
            {
                /* Ãƒâ„¢Ã‚Â¾Ãƒâ„¢Ã‹â€ ÃƒËœÃ‚Â´Ãƒâ„¢Ã¢â‚¬Â¡ ÃƒËœÃ‚Â¨ÃƒËœÃ‚Â¯Ãƒâ„¢Ã‹â€ Ãƒâ„¢Ã¢â‚¬Â  ÃƒËœÃ‚Â¯ÃƒËœÃ‚Â³ÃƒËœÃ‚ÂªÃƒËœÃ‚Â±ÃƒËœÃ‚Â³Ãƒâ€ºÃ…â€™ ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Ãƒâ„¢Ã¢â‚¬Â ÃƒËœÃ‚Â§ÃƒËœÃ‚Â¯Ãƒâ€ºÃ…â€™ÃƒËœÃ‚Â¯Ãƒâ„¢Ã¢â‚¬Â¡ ÃƒÅ¡Ã‚Â¯ÃƒËœÃ‚Â±Ãƒâ„¢Ã‚ÂÃƒËœÃ‚ÂªÃƒâ„¢Ã¢â‚¬Â¡ Ãƒâ„¢Ã¢â‚¬Â¦Ãƒâ€ºÃ…â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…â€™ÃƒËœÃ‚Â´Ãƒâ„¢Ã‹â€ ÃƒËœÃ‚Â¯ */
            }
        }

        return total;
    }

    private HostFilesDto ScanWwwRoot()
    {
        var result = new HostFilesDto();
        var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        if (!Directory.Exists(wwwroot)) return result;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = 0
        };

        var entries = new List<HostFolderDto>();

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(wwwroot))
            {
                var name = Path.GetFileName(dir);
                double size = 0;
                try
                {
                    size = Directory.EnumerateFiles(dir, "*", options)
                        .Sum(f => SafeFileLength(f));
                }
                catch { /* ÃƒËœÃ‚Â¯ÃƒËœÃ‚Â³ÃƒËœÃ‚ÂªÃƒËœÃ‚Â±ÃƒËœÃ‚Â³Ãƒâ€ºÃ…â€™ Ãƒâ„¢Ã¢â‚¬Â ÃƒËœÃ‚Â§Ãƒâ„¢Ã¢â‚¬Â¦ÃƒËœÃ‚Â¹ÃƒËœÃ‚ÂªÃƒËœÃ‚Â¨ÃƒËœÃ‚Â± ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â Ãƒâ„¢Ã¢â‚¬Â ÃƒËœÃ‚Â§ÃƒËœÃ‚Â¯Ãƒâ€ºÃ…â€™ÃƒËœÃ‚Â¯Ãƒâ„¢Ã¢â‚¬Â¡ ÃƒÅ¡Ã‚Â¯ÃƒËœÃ‚Â±Ãƒâ„¢Ã‚ÂÃƒËœÃ‚ÂªÃƒâ„¢Ã¢â‚¬Â¡ Ãƒâ„¢Ã¢â‚¬Â¦Ãƒâ€ºÃ…â€™ÃƒÂ¢Ã¢â€šÂ¬Ã…â€™ÃƒËœÃ‚Â´Ãƒâ„¢Ã‹â€ ÃƒËœÃ‚Â¯ */ }

                entries.Add(new HostFolderDto { Name = name, SizeMb = Math.Round(size / 1048576.0, 2) });
            }

            double rootFiles = 0;
            foreach (var file in Directory.EnumerateFiles(wwwroot, "*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false }))
            {
                rootFiles += SafeFileLength(file);
            }
            result.RootFilesMb = Math.Round(rootFiles / 1048576.0, 2);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "wwwroot scan failed");
        }

        entries = entries.OrderByDescending(x => x.SizeMb).ToList();
        var totalMb = entries.Sum(x => x.SizeMb) + result.RootFilesMb;
        result.TotalMb = Math.Round(totalMb, 2);

        foreach (var e in entries)
        {
            e.Percent = totalMb > 0 ? Math.Round(e.SizeMb / totalMb * 100, 1) : 0;
        }
        result.Folders = entries.Take(14).ToList();

        return result;
    }

    private static double SafeFileLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }

    private async Task<HostDatabaseDto> GetDatabaseInfoAsync(bool force)
    {
        if (!force && _dbCache != null && DateTime.UtcNow - _dbCacheAt < DbCacheInterval)
        {
            return _dbCache;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErrorServiceDbContext>();
        var result = new HostDatabaseDto();

        // sys.master_files requires server-level permissions on many shared hosts.
        // sys.database_files is scoped to the current database and is available to
        // the application database user, so it is the authoritative first choice.
        try
        {
            var files = await db.Database.SqlQuery<DbFileRow>($@"
                SELECT file_id AS FileId,
                       type_desc AS TypeDesc,
                       CAST(size * 8.0 / 1024 AS float) AS SizeMb
                FROM sys.database_files").ToListAsync();

            if (files.Count == 0)
            {
                files = await db.Database.SqlQuery<DbFileRow>($@"
                    SELECT file_id AS FileId,
                           type_desc AS TypeDesc,
                           CAST(size * 8.0 / 1024 AS float) AS SizeMb
                    FROM sys.master_files
                    WHERE database_id = DB_ID()").ToListAsync();
            }

            result.Available = files.Count > 0;
            result.TotalMb = Math.Round(files.Sum(x => Math.Max(0, x.SizeMb)), 2);
            result.DataMb = Math.Round(files.Where(x => string.Equals(x.TypeDesc, "ROWS", StringComparison.OrdinalIgnoreCase)).Sum(x => Math.Max(0, x.SizeMb)), 2);
            result.LogMb = Math.Round(files.Where(x => string.Equals(x.TypeDesc, "LOG", StringComparison.OrdinalIgnoreCase)).Sum(x => Math.Max(0, x.SizeMb)), 2);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database file size query failed");
        }

        // Table statistics are useful detail, but must never invalidate the file totals.
        try
        {
            var tables = await db.Database.SqlQuery<DbTableRow>($@"
                SELECT TOP 12
                    t.name AS Name,
                    CAST(SUM(ps.row_count) AS bigint) AS Rows,
                    CAST(SUM(ps.used_page_count) * 8.0 / 1024 AS float) AS SizeMb
                FROM sys.dm_db_partition_stats ps
                INNER JOIN sys.tables t ON ps.object_id = t.object_id
                GROUP BY t.name
                ORDER BY SUM(ps.used_page_count) DESC").ToListAsync();

            result.TopTables = tables.Select(x => new HostDbTableDto
            {
                Name = x.Name,
                Rows = Math.Max(0, x.Rows),
                SizeMb = Math.Round(Math.Max(0, x.SizeMb), 2)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database table size query failed");
        }

        if (result.Available || result.TotalMb > 0 || result.TopTables.Count > 0)
        {
            _dbCache = result;
            _dbCacheAt = DateTime.UtcNow;
            return result;
        }

        return _dbCache ?? result;
    }
    private static HostProcessDto BuildProcessInfo()
    {
        using var proc = System.Diagnostics.Process.GetCurrentProcess();
        return new HostProcessDto
        {
            ManagedMemoryMb = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 1),
            WorkingSetMb = Math.Round(proc.WorkingSet64 / 1048576.0, 1),
            UptimeHours = Math.Round((DateTime.UtcNow - _startedAt).TotalHours, 1),
            ProcessorCount = Environment.ProcessorCount
        };
    }

    private sealed class DbFileRow
    {
        public int FileId { get; set; }
        public string TypeDesc { get; set; } = "";
        public double SizeMb { get; set; }
    }

    private sealed class DbTableRow
    {
        public string Name { get; set; } = "";
        public long Rows { get; set; }
        public double SizeMb { get; set; }
    }
}

