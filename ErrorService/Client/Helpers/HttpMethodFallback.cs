using System.Net.Http.Json;

namespace ErrorService.Client.Helpers;

public static class HttpMethodFallback
{
    public static async Task<HttpResponseMessage> DeleteWithFallbackAsync(this HttpClient http, string url)
    {
        var resp = await http.DeleteAsync(url);
        if (resp.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("X-HTTP-Method-Override", "DELETE");
            resp = await http.SendAsync(req);
        }
        return resp;
    }

    public static async Task<HttpResponseMessage> PutWithFallbackAsync<T>(this HttpClient http, string url, T body)
    {
        var resp = await http.PutAsJsonAsync(url, body);
        if (resp.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("X-HTTP-Method-Override", "PUT");
            req.Content = JsonContent.Create(body);
            resp = await http.SendAsync(req);
        }
        return resp;
    }
}
