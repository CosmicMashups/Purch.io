using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Purch.Application.Common.Exceptions;

namespace Purch.Api.ErrorHandling;

/// <summary>
/// The one place every unhandled exception in the whole Api ends up. Known, intentional
/// failures (AppException subtypes) get a specific status code and a caller-safe message.
/// Everything else (a real bug, the database being down) gets logged in full detail here
/// and the caller gets a generic, non-leaking message — never a raw stack trace or an
/// internal exception message, which could expose implementation details or PII.
/// </summary>
public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, problemDetails) = MapToProblemDetails(exception, httpContext);

        LogException(exception, statusCode, httpContext);

        httpContext.Response.StatusCode = statusCode;

        // Serialize using the *runtime* type (ValidationProblemDetails vs plain
        // ProblemDetails), not the switch expression's common compile-time type —
        // otherwise System.Text.Json only sees ProblemDetails's properties and the
        // Errors dictionary silently disappears from the response.
        await httpContext.Response.WriteAsJsonAsync(problemDetails, problemDetails.GetType(), cancellationToken);
        return true;
    }

    private static (int StatusCode, ProblemDetails Details) MapToProblemDetails(Exception exception, HttpContext httpContext)
    {
        return exception switch
        {
            ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                new ValidationProblemDetails(validationException.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
                {
                    Title = "Validation failed.",
                    Status = StatusCodes.Status400BadRequest,
                    Instance = httpContext.Request.Path,
                }),

            NotFoundException notFoundException => (
                StatusCodes.Status404NotFound,
                BuildProblemDetails(StatusCodes.Status404NotFound, "Resource not found.", notFoundException.Message, httpContext)),

            ConflictException conflictException => (
                StatusCodes.Status409Conflict,
                BuildProblemDetails(StatusCodes.Status409Conflict, "Conflict.", conflictException.Message, httpContext)),

            ForbiddenException forbiddenException => (
                StatusCodes.Status403Forbidden,
                BuildProblemDetails(StatusCodes.Status403Forbidden, "Forbidden.", forbiddenException.Message, httpContext)),

            // A missing/malformed required route, query, or body parameter (e.g. a
            // required Guid query param the caller forgot to send) — a caller
            // mistake, not a server bug, so it belongs in the 400 family rather
            // than falling through to the generic 500 below.
            BadHttpRequestException badRequestException => (
                badRequestException.StatusCode,
                BuildProblemDetails(badRequestException.StatusCode, "Bad request.", badRequestException.Message, httpContext)),

            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => (
                StatusCodes.Status409Conflict,
                BuildProblemDetails(
                    StatusCodes.Status409Conflict,
                    "Conflict.",
                    "A record with these details already exists.",
                    httpContext)),

            NpgsqlException or TimeoutException => (
                StatusCodes.Status503ServiceUnavailable,
                BuildProblemDetails(
                    StatusCodes.Status503ServiceUnavailable,
                    "Service unavailable.",
                    "The service is temporarily unable to reach the database. Please try again shortly.",
                    httpContext)),

            _ => (
                StatusCodes.Status500InternalServerError,
                BuildProblemDetails(
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred.",
                    "Something went wrong on our end. If this keeps happening, please contact support.",
                    httpContext)),
        };
    }

    private static ProblemDetails BuildProblemDetails(int statusCode, string title, string detail, HttpContext httpContext)
    {
        return new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };
    }

    private void LogException(Exception exception, int statusCode, HttpContext httpContext)
    {
        // 5xx = something we need to investigate, logged at Error with the full exception
        // (stack trace, inner exceptions). 4xx from AppException subtypes are expected,
        // caller-caused outcomes, logged at Information so they don't drown out real bugs.
        if (statusCode >= 500)
        {
            LogUnexpectedException(logger, httpContext.Request.Method, httpContext.Request.Path, statusCode, exception);
        }
        else
        {
            LogExpectedFailure(logger, exception.GetType().Name, httpContext.Request.Method, httpContext.Request.Path, statusCode, exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on {Method} {Path} -> {StatusCode}")]
    private static partial void LogUnexpectedException(ILogger logger, string method, PathString path, int statusCode, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ExceptionType} on {Method} {Path} -> {StatusCode}: {Message}")]
    private static partial void LogExpectedFailure(ILogger logger, string exceptionType, string method, PathString path, int statusCode, string message);
}
