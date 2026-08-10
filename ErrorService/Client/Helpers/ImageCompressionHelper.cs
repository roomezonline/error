using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace ErrorService.Client.Helpers
{
    public static class ImageCompressionHelper
    {
        public static int DefaultMaxBytes { get; set; } = 200 * 1024;

        public static bool IsImage(string contentType) =>
            !string.IsNullOrEmpty(contentType) && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

        public static async Task<byte[]> CompressIfNeededAsync(
            IBrowserFile file,
            IJSRuntime js,
            int? maxBytes = null)
        {
            var targetMaxBytes = maxBytes ?? DefaultMaxBytes;

            if (!IsImage(file.ContentType))
            {
                using var stream = file.OpenReadStream(file.Size);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                return ms.ToArray();
            }

            if (file.Size <= targetMaxBytes)
            {
                using var stream = file.OpenReadStream(file.Size);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                return ms.ToArray();
            }

            try
            {
                using var readStream = file.OpenReadStream(file.Size);
                using var ms = new MemoryStream();
                await readStream.CopyToAsync(ms);
                var buffer = ms.ToArray();
                return await js.InvokeAsync<byte[]>("resizeImageBytes", buffer, file.ContentType, targetMaxBytes);
            }
            catch
            {
                using var stream = file.OpenReadStream(file.Size);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                return ms.ToArray();
            }
        }
    }
}
