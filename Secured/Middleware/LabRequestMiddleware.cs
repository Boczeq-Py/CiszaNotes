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
        context.Response.Headers["X-Lab-Variant"] = "Secured";
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.Headers.CacheControl = "no-cache, no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
        await next(context);
    }
}
