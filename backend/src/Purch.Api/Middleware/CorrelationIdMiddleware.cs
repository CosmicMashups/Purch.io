namespace Purch.Api.Middleware;

/// <summary>
/// Gives every request one id that shows up everywhere it can be looked at: the <c>X-Correlation-Id</c> response
/// header, the <c>traceId</c> in error ProblemDetails, and every log line written while handling it (as a logging
/// scope). A caller may supply its own id (a terminal tags a sale's retries with one) so a report from the app can
/// be matched to server logs; anything that doesn't look like a plain token is replaced, so a client can't
/// inject newlines or huge strings into the logs.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsAcceptable(supplied) ? supplied! : context.TraceIdentifier;

        // ProblemDetails reads TraceIdentifier, so this makes error bodies carry the same id.
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    internal static bool IsAcceptable(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }
}
