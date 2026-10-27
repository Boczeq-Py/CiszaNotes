using System.Net;

namespace PrivateNotes;

public sealed class LabRequestMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip))
        {
            context.Response.StatusCode = 403;
            return;
        }
        context.Response.Headers["X-Lab-Variant"] = "Vulnerable";
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.Headers.CacheControl = "no-cache, no-store";
        // LAB VULNERABILITY: no CSP, so unsafe HTML rendering can execute event handlers.
        await next(context);
    }
}
