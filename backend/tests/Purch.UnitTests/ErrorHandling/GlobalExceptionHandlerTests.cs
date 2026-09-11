using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Purch.Api.ErrorHandling;
using Purch.Application.Common.Exceptions;

namespace Purch.UnitTests.ErrorHandling;

public sealed class GlobalExceptionHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ValidationException_maps_to_400_with_field_level_errors()
    {
        var exception = new ValidationException("Pin", "PIN is required.");

        var (statusCode, body) = await HandleAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        var problem = JsonSerializer.Deserialize<ValidationProblemDetails>(body, JsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("Pin", problem!.Errors.Keys);
    }

    [Fact]
    public async Task NotFoundException_maps_to_404_with_a_specific_message()
    {
        var exception = new NotFoundException("Item", Guid.NewGuid());

        var (statusCode, body) = await HandleAsync(exception);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, JsonOptions);
        Assert.Contains("Item", problem!.Detail);
    }

    [Fact]
    public async Task ConflictException_maps_to_409()
    {
        var (statusCode, _) = await HandleAsync(new ConflictException("Duplicate SKU."));

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
    }

    [Fact]
    public async Task ForbiddenException_maps_to_403()
    {
        var (statusCode, _) = await HandleAsync(new ForbiddenException("Not allowed for this branch."));

        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
    }

    [Fact]
    public async Task An_unexpected_exception_maps_to_500_with_a_generic_message_only()
    {
        var exception = new InvalidOperationException("Some internal detail that must never reach a client.");

        var (statusCode, body) = await HandleAsync(exception);

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, JsonOptions);
        Assert.DoesNotContain("internal detail", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(int StatusCode, string Body)> HandleAsync(Exception exception)
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        Assert.True(handled);
        _ = responseBody.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseBody);
        return (httpContext.Response.StatusCode, await reader.ReadToEndAsync());
    }
}
