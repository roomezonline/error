namespace ErrorService.Server.Infrastructure;

public sealed class TrailingSlashMiddleware
{
    private readonly RequestDelegate _next;

    public TrailingSlashMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip API calls, static files (have dots), and root path
        if (path.StartsWith("/api/") || path.Contains('.') || path == "/" || path.StartsWith("/_framework") || path.StartsWith("/_content"))
        {
            await _next(context);
            return;
        }

        // If path has trailing slash, redirect permanently to non-trailing-slash version
        if (path.Length > 1 && path.EndsWith('/'))
        {
            var redirectUrl = path.TrimEnd('/') + context.Request.QueryString;
            context.Response.Redirect(redirectUrl, permanent: true);
            return;
        }

        await _next(context);
    }
}
