using System.Diagnostics;

namespace Purch.Api.Middleware;

/// <summary>
/// One structured line per request: method, path, status and duration. The query string is left out on purpose
/// (it can carry tokens or customer data). Health probes log at Debug so they don't drown real traffic, and a
/// request slower than <see cref="SlowRequestThreshold"/> is a warning so slow endpoints surface without a
/// metrics stack. Sits outside the exception handler, so a request that ended in a 500 is logged as a 500.
/// </summary>
public sealed partial class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    internal static readonly TimeSpan SlowRequestThreshold = TimeSpan.FromSeconds(2);

    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            var path = context.Request.Path.Value ?? "/";
            var status = context.Response.StatusCode;

            if (path.StartsWith("/health", StringComparison.Ordinal))
            {
                LogHealthProbe(logger, context.Request.Method, path, status, elapsed.TotalMilliseconds);
            }
            else if (elapsed >= SlowRequestThreshold)
            {
                LogSlowRequest(logger, context.Request.Method, path, status, elapsed.TotalMilliseconds);
            }
            else
            {
                LogRequest(logger, context.Request.Method, path, status, elapsed.TotalMilliseconds);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms")]
    private static partial void LogRequest(ILogger logger, string method, string path, int statusCode, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Slow request: {Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms")]
    private static partial void LogSlowRequest(ILogger logger, string method, string path, int statusCode, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms")]
    private static partial void LogHealthProbe(ILogger logger, string method, string path, int statusCode, double elapsedMs);
}
