using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace ErrorService.Server.Controllers;

/// <summary>
/// تبدیل‌گر تصویر برای کاهش وزن تصاویر در لیست‌ها (srcset):
///   GET /img/{width}/uploads/...
/// تبدیل در حافظه انجام و نتیجه کش می‌شود؛ نیازی به نوشتن thumbnail روی دیسک نیست.
/// </summary>
[ApiController]
[Route("img")]
public class ImageTransformController : ControllerBase
{
    // باید دقیقاً همان عرض‌هایی باشد که Client/Helpers/ImageUrlHelper.cs می‌سازد (200/400/800).
    private static readonly int[] AllowedWidths = { 120, 200, 320, 400, 480, 640, 800, 960 };
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly SemaphoreSlim Gate = new(4, 4);
    private static readonly Dictionary<string, CacheEntry> Cache = new();
    private static readonly object CacheLock = new();
    private const int MaxCacheEntries = 128;
    private const long MaxSourceBytes = 15L * 1024 * 1024;

    private readonly string _webRootPath;

    public ImageTransformController(IWebHostEnvironment environment)
    {
        _webRootPath = environment.WebRootPath
            ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
    }

    private sealed record CacheEntry(byte[] Bytes, string ContentType, string ETag);

    [HttpGet("{width:int}/{*src}")]
    public async Task<IActionResult> Get(int width, string src, CancellationToken cancellationToken)
    {
        if (!AllowedWidths.Contains(width))
            return BadRequest();

        var normalized = NormalizeSrc(src);
        if (normalized == null)
            return NotFound();

        var webRoot = Path.GetFullPath(_webRootPath);
        var fullPath = Path.GetFullPath(Path.Combine(webRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
            return NotFound();

        var info = new FileInfo(fullPath);
        var cacheKey = $"{width}|{normalized}|{info.LastWriteTimeUtc.Ticks}|{info.Length}";
        var etag = "\"" + Convert.ToHexString(
            System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(cacheKey))) + "\"";

        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers.ETag = etag;

        if (Request.Headers.IfNoneMatch.Any(v => v == etag))
            return new StatusCodeResult(StatusCodes.Status304NotModified);

        if (TryGetCache(cacheKey, out var cached))
            return File(cached.Bytes, cached.ContentType);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCache(cacheKey, out cached))
                return File(cached.Bytes, cached.ContentType);

            // فایل‌های خیلی بزرگ را تبدیل نمی‌کنیم تا فشار حافظه/CPU ایجاد نشود.
            if (info.Length > MaxSourceBytes)
                return PhysicalFile(fullPath, ContentTypeFor(Path.GetExtension(normalized)));

            var entry = await BuildAsync(fullPath, Path.GetExtension(normalized), width, etag, cancellationToken);
            PutCache(cacheKey, entry);
            return File(entry.Bytes, entry.ContentType);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// فقط مسیرهای uploads/* بدون «..» و با پسوند مجاز می‌پذیریم.
    /// </summary>
    private static string? NormalizeSrc(string? src)
    {
        if (string.IsNullOrWhiteSpace(src))
            return null;

        var value = Uri.UnescapeDataString(src).Replace('\\', '/').Trim();
        while (value.StartsWith('/'))
            value = value[1..];

        if (value.Length == 0)
            return null;
        if (value.Contains("..", StringComparison.Ordinal) || value.Contains("//", StringComparison.Ordinal))
            return null;
        if (!value.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            return null;
        if (!AllowedExtensions.Contains(Path.GetExtension(value)))
            return null;

        return value;
    }

    private static async Task<CacheEntry> BuildAsync(string fullPath, string extension, int width, string etag, CancellationToken ct)
    {
        try
        {
            await using var input = System.IO.File.OpenRead(fullPath);
            using var image = await Image.LoadAsync(input, ct);

            // بزرگ‌نمایی نمی‌کنیم؛ فقط از اندازه اصلی کوچک‌تر می‌شویم.
            if (image.Width > width)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(width, 0),
                    Mode = ResizeMode.Max,
                    Compand = true
                }));
            }

            await using var output = new MemoryStream();
            await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = 80 }, ct);
            return new CacheEntry(output.ToArray(), "image/webp", etag);
        }
        catch
        {
            // اگر تبدیل شکست خورد، همان فایل اصلی را برمی‌گردانیم تا تصویر از دست نرود.
            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath, ct);
            return new CacheEntry(bytes, ContentTypeFor(extension), etag);
        }
    }

    private static bool TryGetCache(string key, out CacheEntry entry)
    {
        lock (CacheLock)
        {
            if (Cache.TryGetValue(key, out var found))
            {
                entry = found;
                return true;
            }
        }
        entry = null!;
        return false;
    }

    private static void PutCache(string key, CacheEntry entry)
    {
        lock (CacheLock)
        {
            if (Cache.Count >= MaxCacheEntries)
                Cache.Clear();
            Cache[key] = entry;
        }
    }

    private static string ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
