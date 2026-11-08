using System.Net;
using System.Net.Http.Headers;

namespace ErrorService.Client.Services;

public class AuthHttpHandler : DelegatingHandler
{
    private readonly LocalStorageService _storage;
    private readonly SessionExpiryNotifier _sessionExpiry;

    public AuthHttpHandler(LocalStorageService storage, SessionExpiryNotifier sessionExpiry)
    {
        _storage = storage;
        _sessionExpiry = sessionExpiry;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? attachedToken = null;

        if (request.Headers.Authorization == null)
        {
            var token = await _storage.GetAsync("auth_token");
            if (string.IsNullOrWhiteSpace(token))
                token = await _storage.GetAsync("workshop_token");

            if (!string.IsNullOrWhiteSpace(token))
            {
                // توکن منقضی را حتی نفرست: هم درخواست بی‌نتیجه است، هم کاربر
                // باید فوراً به صفحه ورود هدایت شود (لاگین قبلی باطل شده است).
                if (JwtParser.IsExpired(token))
                {
                    await ClearStoredTokensAsync();
                    _sessionExpiry.NotifyExpired();
                }
                else
                {
                    attachedToken = token;
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }
        }
        else if (string.Equals(request.Headers.Authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            attachedToken = request.Headers.Authorization.Parameter;
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // فقط وقتی جلسه واقعاً منقضی شده پاک‌سازی می‌کنیم. 401 به معنای
            // نداشتن مجوز نباید کاربر را از حساب خارج کند.
            if (!string.IsNullOrWhiteSpace(attachedToken) && JwtParser.IsExpired(attachedToken))
            {
                await ClearStoredTokensAsync();
                _sessionExpiry.NotifyExpired();
            }
            else if (request.RequestUri?.AbsolutePath?.Contains("/api/auth/me") == true)
            {
                // /api/auth/me منبع حقیقت اعتبار توکن است.
                await ClearStoredTokensAsync();
            }
        }

        return response;
    }

    private async Task ClearStoredTokensAsync()
    {
        await _storage.RemoveAsync("auth_token");
        await _storage.RemoveAsync("workshop_token");
    }
}
