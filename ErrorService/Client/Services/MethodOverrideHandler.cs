using System.Net;
using System.Net.Http.Headers;

namespace ErrorService.Client.Services;

public sealed class MethodOverrideHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var resp = await base.SendAsync(request, cancellationToken);
        if (resp.StatusCode != HttpStatusCode.MethodNotAllowed)
            return resp;

        var method = request.Method;
        if (method != HttpMethod.Put && method != HttpMethod.Delete)
            return resp;

        using var oldResp = resp;

        var fallback = new HttpRequestMessage(HttpMethod.Post, request.RequestUri);
        fallback.Headers.Add("X-HTTP-Method-Override", method.Method);

        if (request.Content != null)
        {
            fallback.Content = request.Content;
            request.Content = null;
        }

        return await base.SendAsync(fallback, cancellationToken);
    }
}
