using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;

namespace ErrorService.Client.Services;

public sealed class ClientCachingHandler : DelegatingHandler
{
    private static readonly ConcurrentDictionary<string, (string json, DateTime cachedAt)> _cache = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private static readonly HashSet<string> CacheablePrefixes = new()
    {
        "/api/sitesettings",
        "/api/popular-brands",
        "/api/sliders",
        "/api/stories",
        "/api/categories"
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Get && request.RequestUri != null)
        {
            var path = request.RequestUri.AbsolutePath;

            if (CacheablePrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                var key = request.RequestUri.ToString();

                if (_cache.TryGetValue(key, out var cached) && (DateTime.UtcNow - cached.cachedAt) < Ttl)
                {
                    var cachedResponse = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(cached.json, System.Text.Encoding.UTF8, "application/json"),
                        RequestMessage = request
                    };
                    return cachedResponse;
                }

                var response = await base.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    _cache[key] = (json, DateTime.UtcNow);
                }

                return response;
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
