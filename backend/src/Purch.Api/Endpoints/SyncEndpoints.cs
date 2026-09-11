using Purch.Application.Sync;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class SyncEndpoints
{
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var anyStaff = new[] { nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Cashier), nameof(Role.Warehouse) };
        var reviewer = new[] { nameof(Role.Admin), nameof(Role.Manager) };

        // --- Phase 6 — batched offline-write sync, one round trip per queue flush ---
        _ = app.MapPost("/sync", async (
            SyncBatchRequest request,
            ISyncService syncService,
            CancellationToken cancellationToken) =>
            Results.Ok(await syncService.SyncBatchAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(anyStaff));

        // --- Manual review of conflict-flagged records ---
        _ = app.MapGet("/sync/flagged", async (
            ISyncService syncService,
            CancellationToken cancellationToken) =>
            Results.Ok(await syncService.ListFlaggedAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reviewer));

        _ = app.MapPost("/sync/flagged/{syncedRecordId:guid}/acknowledge", async (
            Guid syncedRecordId,
            ISyncService syncService,
            CancellationToken cancellationToken) =>
            Results.Ok(await syncService.AcknowledgeFlaggedAsync(syncedRecordId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(reviewer));

        return app;
    }
}
