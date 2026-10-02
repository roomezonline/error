using System.Net;
using System.Net.Http.Headers;

namespace ErrorService.Client.Services;

public class AuthHttpHandler : DelegatingHandler
{
    private readonly LocalStorageService _storage;

    public AuthHttpHandler(LocalStorageService storage)
    {
        _storage = storage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization == null)
        {
            var token = await _storage.GetAsync("auth_token");
            if (string.IsNullOrWhiteSpace(token))
                token = await _storage.GetAsync("workshop_token");
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Only clear stored token when /api/auth/me itself returns 401 —
            // that is the source-of-truth for token validity.
            // A 401 from any other endpoint (e.g. cart, tickets) could be a
            // permission or server-side issue, and must not wipe login state.
            var authHeader = request.Headers.Authorization;
            var isAuthMeRequest = request.RequestUri?.AbsolutePath?.Contains("/api/auth/me") == true;

            if (authHeader != null
                && string.Equals(authHeader.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                && isAuthMeRequest)
            {
                await _storage.RemoveAsync("auth_token");
                await _storage.RemoveAsync("workshop_token");
            }
        }

        return response;
    }
}
