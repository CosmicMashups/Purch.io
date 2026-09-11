using Purch.Application.Inventory;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var inventoryManager = new[] { nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Warehouse) };

        // --- C1 — overview cards + low-stock alert list ---
        _ = app.MapGet("/inventory/dashboard", async (
            IInventoryDashboardService dashboardService,
            CancellationToken cancellationToken) =>
            Results.Ok(await dashboardService.GetDashboardAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

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

        // --- C4 — multi-branch stock transfer ---
        _ = app.MapGet("/branch-transfers", async (
            IBranchTransferService branchTransferService,
            CancellationToken cancellationToken) =>
            Results.Ok(await branchTransferService.ListAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/branch-transfers", async (
            CreateBranchTransferRequest request,
            IBranchTransferService branchTransferService,
            CancellationToken cancellationToken) =>
            Results.Ok(await branchTransferService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/branch-transfers/{branchTransferId:guid}/mark-in-transit", async (
            Guid branchTransferId,
            IBranchTransferService branchTransferService,
            CancellationToken cancellationToken) =>
            Results.Ok(await branchTransferService.MarkInTransitAsync(branchTransferId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/branch-transfers/{branchTransferId:guid}/mark-received", async (
            Guid branchTransferId,
            IBranchTransferService branchTransferService,
            CancellationToken cancellationToken) =>
            Results.Ok(await branchTransferService.MarkReceivedAsync(branchTransferId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        return app;
    }
}
