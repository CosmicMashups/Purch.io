using Purch.Application.Reporting;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var reportGenerator = new[] { nameof(Role.Admin), nameof(Role.Manager) };

        // --- FR26 — BIR X-reading (mid-shift, re-runnable) / Z-reading (end-of-day, advances the reset counter) ---
        _ = app.MapPost("/reports/x-reading", async (
            IBirReadingService birReadingService,
            CancellationToken cancellationToken) =>
            Results.Ok(await birReadingService.GenerateXReadingAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        _ = app.MapPost("/reports/z-reading", async (
            IBirReadingService birReadingService,
            CancellationToken cancellationToken) =>
            Results.Ok(await birReadingService.GenerateZReadingAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reportGenerator));

        return app;
    }
}
