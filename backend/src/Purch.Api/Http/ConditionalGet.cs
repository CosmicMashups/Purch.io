using Microsoft.Extensions.Primitives;

namespace Purch.Api.Http;

/// <summary>ETag / If-None-Match for list endpoints. The validator is computed first; when the caller already
/// holds that version we answer 304 without loading or serialising the list.
/// <c>no-cache</c> means "revalidate every time", not "never store", and <c>Vary: Authorization</c> keeps one
/// user's cached copy from being served to another.</summary>
public static class ConditionalGet
{
    public static async Task<IResult> RespondAsync<T>(
        HttpContext httpContext,
        Func<Task<string>> getVersion,
        Func<Task<T>> load)
    {
        var etag = $"W/\"{await getVersion()}\"";

        httpContext.Response.Headers.ETag = etag;
        httpContext.Response.Headers.CacheControl = "private, no-cache";
        httpContext.Response.Headers.Vary = "Authorization";

        return Matches(httpContext.Request.Headers.IfNoneMatch, etag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Ok(await load());
    }

    private static bool Matches(StringValues ifNoneMatch, string etag)
    {
        foreach (var header in ifNoneMatch)
        {
            foreach (var candidate in (header ?? string.Empty).Split(','))
            {
                var value = candidate.Trim();
                if (value == "*" || value == etag)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
