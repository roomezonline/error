using Microsoft.AspNetCore.Hosting;
using System.Text.RegularExpressions;

namespace ErrorService.Server.Infrastructure;

public static class FileCleanupHelper
{
    private static readonly Regex LocalUploadPattern = new(@"^/uploads/.+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void DeleteOldFileIfChanged(string? oldUrl, string? newUrl, IWebHostEnvironment env)
    {
        if (string.IsNullOrWhiteSpace(oldUrl))
            return;

        if (string.Equals(oldUrl, newUrl, StringComparison.OrdinalIgnoreCase))
            return;

        if (!LocalUploadPattern.IsMatch(oldUrl))
            return;

        try
        {
            var wwwroot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
            var relativePath = oldUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(wwwroot, relativePath);

            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
        catch
        {
        }
    }
}
