using Purch.Api.RateLimiting;
using Purch.Application.Shifts;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class ShiftEndpoints
{
    public static IEndpointRouteBuilder MapShiftEndpoints(this IEndpointRouteBuilder app)
    {
        var posOperator = new[] { nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Cashier) };

        // --- D7 — one cash-drawer session per device ---
        _ = app.MapGet("/shifts/current", async (
            IShiftService shiftService,
            CancellationToken cancellationToken) =>
            {
                var shift = await shiftService.GetCurrentShiftAsync(cancellationToken);
                // Results.Ok/Json(null) writes an empty body instead of the JSON
                // literal "null" — a client deserializing into a nullable DTO
                // would throw on that empty response instead of getting back
                // "no open shift", so write the literal explicitly.
                return shift is null
                    ? Results.Text("null", "application/json")
                    : Results.Ok(shift);
            })
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapPost("/shifts/open", async (
            OpenShiftRequest request,
            IShiftService shiftService,
            CancellationToken cancellationToken) =>
            Results.Ok(await shiftService.OpenShiftAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator));

        _ = app.MapPost("/shifts/close", async (
            CloseShiftRequest request,
            IShiftService shiftService,
            CancellationToken cancellationToken) =>
            Results.Ok(await shiftService.CloseShiftAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(posOperator))
            .RequireRateLimiting(RateLimiterPolicies.ShiftApproval);

        return app;
    }
}
