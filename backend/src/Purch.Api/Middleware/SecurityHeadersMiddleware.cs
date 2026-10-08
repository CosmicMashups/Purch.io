namespace Purch.Api.Middleware;

/// <summary>
/// Baseline response hardening for an API that returns JSON, CSV and user-uploaded images: stop browsers from
/// sniffing a response into a different content type, keep the API out of frames and referrers, keep
/// authenticated responses out of shared caches, and tell browsers to stay on HTTPS (only when the request
/// actually arrived over HTTPS, so a plain-HTTP LAN install in Local mode is never told to refuse HTTP).
/// Headers are added as the response starts so they reflect the final scheme after forwarded-header handling.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers.XFrameOptions = "DENY";
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; sandbox";

            // Uploaded item images are meant to be cached; everything else is per-user data.
            if (!context.Request.Path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase)
                && !headers.ContainsKey(Microsoft.Net.Http.Headers.HeaderNames.CacheControl))
            {
                headers.CacheControl = "no-store";
            }

            if (context.Request.IsHttps)
            {
                headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
