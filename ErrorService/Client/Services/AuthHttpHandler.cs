using System.Net;

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
