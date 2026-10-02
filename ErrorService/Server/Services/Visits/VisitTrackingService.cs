using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ErrorService.Server.Services.Visits;

public sealed record VisitCapture(
    string Ip,
    string Path,
    string Referrer,
    string UserAgent,
    string VisitorId,
    DateTime VisitedAtUtc,
    string IdentityKey,
    string UserName,
    bool HasUserIdentity);

/// <summary>
/// صف غیرمسدودکننده ثبت بازدید. درخواست کاربر فقط یک TryWrite انجام می‌دهد
/// و هیچ I/O دیتابیسی در مسیر درخواست عمومی انجام نمی‌شود.
/// </summary>
public sealed class VisitTrackingService
{
    private readonly Channel<VisitCapture> _channel = Channel.CreateBounded<VisitCapture>(
        new BoundedChannelOptions(20000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    // جلوگیری از ثبت تکراری همان مسیر توسط همان بازدیدکننده در بازه کوتاه (رفرش مکرر)
    // نکته: «تکرار» در سطح صفحه‌بازدید مسدود می‌شود؛ شمارش «بازدید» در سطح جلسه انجام می‌گیرد.
    private readonly ConcurrentDictionary<string, DateTime> _recent = new();

    private static readonly TimeSpan DedupeWindow = TimeSpan.FromSeconds(20);
    private long _lastPruneTicks;

    public System.Threading.Channels.ChannelReader<VisitCapture> Reader => _channel.Reader;

    public bool TryEnqueue(VisitCapture capture)
    {
        var key = capture.VisitorId + "|" + capture.Path;
        var now = capture.VisitedAtUtc;

        if (_recent.TryGetValue(key, out var last) && now - last < DedupeWindow)
        {
            return false;
        }

        _recent[key] = now;
        PruneIfNeeded(now);

        return _channel.Writer.TryWrite(capture);
    }

    private void PruneIfNeeded(DateTime now)
    {
        var last = Interlocked.Read(ref _lastPruneTicks);
        if (now.Ticks - last < TimeSpan.FromMinutes(5).Ticks) return;
        if (Interlocked.CompareExchange(ref _lastPruneTicks, now.Ticks, last) != last) return;

        var cutoff = now - DedupeWindow;
        foreach (var kv in _recent)
        {
            if (kv.Value < cutoff)
            {
                _recent.TryRemove(kv.Key, out _);
            }
        }
    }
}

public static class VisitBotDetector
{
    private static readonly string[] Patterns =
    {
        "bot", "crawl", "spider", "slurp", "bingpreview", "facebookexternalhit",
        "whatsapp", "telegram", "preview", "headless", "lighthouse", "monitoring",
        "curl/", "wget", "python-requests", "axios", "node-fetch", "go-http-client",
        "libwww", "scrapy", "yandex", "ahrefs", "semrush", "petalbot", "bytespider",
        "chrome-lighthouse", "gptbot", "ccbot", "applebot", "embedly", "pinterest",
        "redditbot", "vkshare", "w3c_validator", "validator", "screaming frog"
    };

    public static bool IsBot(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return true;
        var ua = userAgent.ToLowerInvariant();
        return Patterns.Any(p => ua.Contains(p, StringComparison.Ordinal));
    }
}
