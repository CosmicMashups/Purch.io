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
            DateTimeOffset? before,
            Guid? beforeId,
            int? limit,
            IInventoryMovementService movementService,
            CancellationToken cancellationToken) =>
            Results.Ok(await movementService.ListAsync(itemId, branchId, type, before, limit, beforeId, cancellationToken)))
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

        _ = app.MapPost("/branch-transfers/{branchTransferId:guid}/cancel", async (
            Guid branchTransferId,
            IBranchTransferService branchTransferService,
            CancellationToken cancellationToken) =>
            Results.Ok(await branchTransferService.CancelAsync(branchTransferId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        // --- C5 — suppliers ---
        _ = app.MapGet("/suppliers", async (
            ISupplierService supplierService,
            CancellationToken cancellationToken) =>
            Results.Ok(await supplierService.ListAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/suppliers", async (
            CreateSupplierRequest request,
            ISupplierService supplierService,
            CancellationToken cancellationToken) =>
            Results.Ok(await supplierService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPut("/suppliers/{supplierId:guid}", async (
            Guid supplierId,
            UpdateSupplierRequest request,
            ISupplierService supplierService,
            CancellationToken cancellationToken) =>
            Results.Ok(await supplierService.UpdateAsync(supplierId, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        // --- C5 — purchase orders ---
        _ = app.MapGet("/purchase-orders", async (
            IPurchaseOrderService purchaseOrderService,
            CancellationToken cancellationToken) =>
            Results.Ok(await purchaseOrderService.ListAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/purchase-orders", async (
            CreatePurchaseOrderRequest request,
            IPurchaseOrderService purchaseOrderService,
            CancellationToken cancellationToken) =>
            Results.Ok(await purchaseOrderService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/purchase-orders/{purchaseOrderId:guid}/mark-sent", async (
            Guid purchaseOrderId,
            IPurchaseOrderService purchaseOrderService,
            CancellationToken cancellationToken) =>
            Results.Ok(await purchaseOrderService.MarkSentAsync(purchaseOrderId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/purchase-orders/{purchaseOrderId:guid}/cancel", async (
            Guid purchaseOrderId,
            IPurchaseOrderService purchaseOrderService,
            CancellationToken cancellationToken) =>
            Results.Ok(await purchaseOrderService.CancelAsync(purchaseOrderId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        // --- Incoming Receiving Reports (IRR) ---
        _ = app.MapGet("/incoming-receiving", async (
            IIncomingReceivingService incomingReceivingService,
            CancellationToken cancellationToken) =>
            Results.Ok(await incomingReceivingService.ListAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapGet("/incoming-receiving/{reportId:guid}", async (
            Guid reportId,
            IIncomingReceivingService incomingReceivingService,
            CancellationToken cancellationToken) =>
            Results.Ok(await incomingReceivingService.GetAsync(reportId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/incoming-receiving", async (
            CreateIncomingReceivingRequest request,
            IIncomingReceivingService incomingReceivingService,
            CancellationToken cancellationToken) =>
            Results.Ok(await incomingReceivingService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/incoming-receiving/{reportId:guid}/link-purchase-order", async (
            Guid reportId,
            LinkIncomingReceivingRequest request,
            IIncomingReceivingService incomingReceivingService,
            CancellationToken cancellationToken) =>
            Results.Ok(await incomingReceivingService.LinkPurchaseOrderAsync(reportId, request.PurchaseOrderId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(nameof(Role.Admin), nameof(Role.Manager)));

        // --- Separately tracked inventory items (raw materials/ingredients), used when the
        // tenant opts into UseSeparateInventoryTracking instead of Item.StockOnHand directly ---
        _ = app.MapGet("/inventory-items", async (
            IInventoryItemService inventoryItemService,
            CancellationToken cancellationToken) =>
            Results.Ok(await inventoryItemService.ListAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/inventory-items", async (
            CreateInventoryItemRequest request,
            IInventoryItemService inventoryItemService,
            CancellationToken cancellationToken) =>
            Results.Ok(await inventoryItemService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPut("/inventory-items/order", async (
            ReorderInventoryItemsRequest request,
            IInventoryItemService inventoryItemService,
            CancellationToken cancellationToken) =>
            {
                await inventoryItemService.ReorderAsync(request, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPut("/inventory-items/{id:guid}", async (
            Guid id,
            UpdateInventoryItemRequest request,
            IInventoryItemService inventoryItemService,
            CancellationToken cancellationToken) =>
            Results.Ok(await inventoryItemService.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/inventory-items/{id:guid}/physical-count", async (
            Guid id,
            UpdatePhysicalCountRequest request,
            IInventoryItemService inventoryItemService,
            CancellationToken cancellationToken) =>
            Results.Ok(await inventoryItemService.UpdatePhysicalCountAsync(id, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/inventory-items/{id:guid}/receive", async (
            Guid id,
            ReceiveInventoryStockRequest request,
            IInventoryItemService inventoryItemService,
            CancellationToken cancellationToken) =>
            Results.Ok(await inventoryItemService.ReceiveStockAsync(id, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        // --- Ingredient categories: everyone in inventory can read them; only Admin and Manager change them ---
        var categoryEditor = new[] { nameof(Role.Admin), nameof(Role.Manager) };

        _ = app.MapGet("/inventory-categories", async (
            IInventoryCategoryService categoryService,
            CancellationToken cancellationToken) =>
            Results.Ok(await categoryService.ListAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/inventory-categories", async (
            CreateInventoryCategoryRequest request,
            IInventoryCategoryService categoryService,
            CancellationToken cancellationToken) =>
            Results.Ok(await categoryService.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(categoryEditor));

        _ = app.MapPut("/inventory-categories/{id:guid}", async (
            Guid id,
            UpdateInventoryCategoryRequest request,
            IInventoryCategoryService categoryService,
            CancellationToken cancellationToken) =>
            Results.Ok(await categoryService.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(categoryEditor));

        _ = app.MapDelete("/inventory-categories/{id:guid}", async (
            Guid id,
            IInventoryCategoryService categoryService,
            CancellationToken cancellationToken) =>
        {
            await categoryService.DeleteAsync(id, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole(categoryEditor));

        return app;
    }
}
