using Purch.Application.Inventory;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var inventoryManager = new[] { nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Warehouse) };

        // --- C2/C3 — the stock movement log and the form that records into it ---
        _ = app.MapGet("/inventory/movements", async (
            Guid? itemId,
            Guid? branchId,
            MovementType? type,
            IInventoryMovementService movementService,
            CancellationToken cancellationToken) =>
            Results.Ok(await movementService.ListAsync(itemId, branchId, type, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/inventory/movements", async (
            RecordMovementRequest request,
            IInventoryMovementService movementService,
            CancellationToken cancellationToken) =>
            Results.Ok(await movementService.RecordAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        return app;
    }
}
