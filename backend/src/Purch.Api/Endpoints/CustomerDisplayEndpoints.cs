using System.Text.Json;
using Purch.Application.Devices;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

/// <summary>A customer display paired to a Register follows that Register's cart. The Register pushes what to show and the
/// display polls for it (a serverless API holds nothing between requests). The display reads with a conditional GET, so a
/// poll that finds nothing new costs a 304.</summary>
public static class CustomerDisplayEndpoints
{
    public static IEndpointRouteBuilder MapCustomerDisplayEndpoints(this IEndpointRouteBuilder app)
    {
        var tillRoles = new[] { nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Cashier) };

        _ = app.MapPut("/customer-display/state", async (
            JsonElement state,
            ICustomerDisplayService displayService,
            CancellationToken cancellationToken) =>
        {
            await displayService.PublishAsync(state, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole(tillRoles));

        _ = app.MapGet("/customer-display/state", async (
            HttpContext http,
            ICustomerDisplayService displayService,
            CancellationToken cancellationToken) =>
        {
            long? known = null;
            var header = http.Request.Headers.IfNoneMatch.ToString().Trim('"', 'v', ' ');
            if (long.TryParse(header, out var parsed))
            {
                known = parsed;
            }

            var feed = await displayService.GetAsync(known, cancellationToken);
            http.Response.Headers.ETag = $"\"v{feed.Version}\"";
            http.Response.Headers.CacheControl = "no-store";
            return feed.NotModified
                ? Results.StatusCode(StatusCodes.Status304NotModified)
                : Results.Ok(new { version = feed.Version, updatedAt = feed.UpdatedAt, state = feed.State });
        }).RequireAuthorization(policy => policy.RequireRole(nameof(Role.CustomerDisplay)));

        return app;
    }
}
