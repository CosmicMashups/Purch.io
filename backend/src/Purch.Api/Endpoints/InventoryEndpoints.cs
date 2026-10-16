using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Purch.Application.Common.Exceptions;
using Purch.Application.Inventory;
using Purch.Application.Inventory.Import;
using Purch.Domain.Enums;

namespace Purch.Api.Endpoints;

public static class InventoryEndpoints
{
    /// <summary>The 5 MB file cap plus room for the multipart boundaries and the kind field.</summary>
    private const long ImportRequestLimit = CsvImportParser.MaxBytes + (64 * 1024);

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var inventoryManager = new[] { nameof(Role.Admin), nameof(Role.Manager), nameof(Role.Warehouse) };

        // --- CSV export (tenant-wide; cost column dropped for actors who cannot see costs) ---
        _ = app.MapGet("/inventory/export/items.csv", async (IInventoryExportService exports, CancellationToken cancellationToken) =>
            Results.Text(await exports.ExportItemsAsync(cancellationToken), "text/csv"))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapGet("/inventory/export/ingredients.csv", async (IInventoryExportService exports, CancellationToken cancellationToken) =>
            Results.Text(await exports.ExportIngredientsAsync(cancellationToken), "text/csv"))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapGet("/inventory/export/stock.csv", async (IInventoryExportService exports, CancellationToken cancellationToken) =>
            Results.Text(await exports.ExportStockAsync(cancellationToken), "text/csv"))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        // --- C1 — overview cards + low-stock alert list ---
        _ = app.MapGet("/inventory/dashboard", async (
            IInventoryDashboardService dashboardService,
            CancellationToken cancellationToken) =>
            Results.Ok(await dashboardService.GetDashboardAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        // --- C2/C3 — the stock movement log and the form that records into it ---
        _ = app.MapGet("/inventory/movements", async (
            Guid? itemId,
            Guid? inventoryItemId,
            Guid? branchId,
            MovementType? type,
            DateTimeOffset? before,
            Guid? beforeId,
            int? limit,
            IInventoryMovementService movementService,
            CancellationToken cancellationToken) =>
            Results.Ok(await movementService.ListAsync(itemId, branchId, type, before, limit, beforeId, inventoryItemId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/inventory/movements", async (
            RecordMovementRequest request,
            IInventoryMovementService movementService,
            CancellationToken cancellationToken) =>
            Results.Ok(await movementService.RecordAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        // --- U3 — CSV import: upload, review (re-planned on the server), resolutions, commit (Admin/Manager approver PIN
        // checked in the service). Cost columns are honoured only because only these roles reach the routes. ---
        _ = app.MapPost("/inventory/imports", async (HttpRequest request, IImportService importService, CancellationToken cancellationToken) =>
        {
            // Refuse an oversized body before anything is buffered; the parser enforces the 5 MB file cap again.
            if (request.ContentLength > ImportRequestLimit)
            {
                return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Payload too large.", detail: "The file is larger than 5 MB.");
            }

            var sizeFeature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (sizeFeature is { IsReadOnly: false })
            {
                sizeFeature.MaxRequestBodySize = ImportRequestLimit;
            }

            if (!request.HasFormContentType)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad request.", detail: "Send the file as multipart/form-data.");
            }

            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad request.", detail: "No file was uploaded or the file is empty.");
            }

            ImportKind? kind = form["kind"].ToString().Trim().ToLowerInvariant() switch
            {
                "catalog" => ImportKind.Catalog,
                "stock" => ImportKind.Stock,
                _ => null,
            };
            if (kind is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad request.", detail: "Kind must be catalog or stock.");
            }

            using var buffer = new MemoryStream();
            await using (var stream = file.OpenReadStream())
            {
                await stream.CopyToAsync(buffer, cancellationToken);
            }

            var key = request.Headers["Idempotency-Key"].ToString();
            try
            {
                return Results.Ok(await importService.UploadAsync(new MemoryStream(buffer.ToArray()), kind.Value, key, cancellationToken));
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // A parallel upload with the same Idempotency-Key won the insert: return that batch.
                return Results.Ok(await importService.UploadAsync(new MemoryStream(buffer.ToArray()), kind.Value, key, cancellationToken));
            }
        })
            .WithMetadata(new RequestSizeLimitAttribute(ImportRequestLimit))
            .DisableAntiforgery()
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapGet("/inventory/imports/{id:guid}", async (Guid id, IImportService importService, CancellationToken cancellationToken) =>
            Results.Ok(await importService.GetAsync(id, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPut("/inventory/imports/{id:guid}/resolutions", async (
            Guid id,
            Dictionary<string, ImportResolutionDto> resolutions,
            IImportService importService,
            CancellationToken cancellationToken) =>
            Results.Ok(await importService.SaveResolutionsAsync(id, resolutions, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        // A stale preview is a 409 carrying the fresh plan and the changed rows; nothing was applied.
        _ = app.MapPost("/inventory/imports/{id:guid}/commit", async (
            Guid id,
            CommitImportRequest request,
            IImportService importService,
            CancellationToken cancellationToken) =>
        {
            ImportCommitResultDto result;
            try
            {
                result = await importService.CommitAsync(id, request, cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A parallel commit of the same batch won: replay its result, otherwise the generic 409.
                result = await importService.TryReplayAsync(id, cancellationToken) ?? throw new ConflictException("This import was changed by another request. Please try again.");
            }

            return result.Committed ? Results.Ok(result) : Results.Conflict(result);
        })
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        // Review reveals expected quantities (even on a blind take), so only Admin/Manager may move a take to it.
        var stockTakeReviewers = new[] { nameof(Role.Admin), nameof(Role.Manager) };

        // --- U3 — stock-take sessions. Apply additionally needs an Admin/Manager approver PIN (checked in the service). ---
        _ = app.MapPost("/stock-takes", async (
            CreateStockTakeRequest request,
            IStockTakeService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapGet("/stock-takes", async (
            IStockTakeService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapGet("/stock-takes/{id:guid}", async (
            Guid id,
            IStockTakeService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(id, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPut("/stock-takes/{id:guid}/lines/{lineId:guid}", async (
            Guid id,
            Guid lineId,
            CountStockTakeLineRequest request,
            IStockTakeService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.CountLineAsync(id, lineId, request.Counted, request.Skip, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/stock-takes/{id:guid}/start", async (
            Guid id,
            IStockTakeService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.StartAsync(id, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/stock-takes/{id:guid}/review", async (
            Guid id,
            IStockTakeService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ToReviewAsync(id, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(stockTakeReviewers));

        _ = app.MapPost("/stock-takes/{id:guid}/apply", async (
            Guid id,
            ApplyStockTakeRequest request,
            IStockTakeService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ApplyAsync(id, request.ApproverPin, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/stock-takes/{id:guid}/cancel", async (
            Guid id,
            IStockTakeService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.CancelAsync(id, cancellationToken)))
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

        _ = app.MapPut("/purchase-orders/{purchaseOrderId:guid}", async (
            Guid purchaseOrderId,
            UpdatePurchaseOrderRequest request,
            IPurchaseOrderService purchaseOrderService,
            CancellationToken cancellationToken) =>
            Results.Ok(await purchaseOrderService.UpdateAsync(purchaseOrderId, request, cancellationToken)))
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

        // --- Restock: purchase requests, converted into purchase orders ---
        var approvers = new[] { nameof(Role.Admin), nameof(Role.Manager) };

        _ = app.MapGet("/inventory/restock-suggestions", async (
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetRestockSuggestionsAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapGet("/purchase-requests", async (
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/purchase-requests", async (
            CreatePurchaseRequestRequest request,
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.CreateAsync(request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/purchase-requests/{id:guid}/approve", async (
            Guid id,
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ApproveAsync(id, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(approvers));

        _ = app.MapPost("/purchase-requests/{id:guid}/cancel", async (
            Guid id,
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.CancelAsync(id, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPost("/purchase-requests/{id:guid}/convert", async (
            Guid id,
            ConvertPurchaseRequestRequest request,
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ConvertAsync(id, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapGet("/items/{itemId:guid}/suppliers", async (
            Guid itemId,
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetSuppliersAsync(itemId, null, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPut("/items/{itemId:guid}/suppliers", async (
            Guid itemId,
            SetItemSuppliersRequest request,
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.SetSuppliersAsync(itemId, null, request, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapGet("/inventory-items/{inventoryItemId:guid}/suppliers", async (
            Guid inventoryItemId,
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetSuppliersAsync(null, inventoryItemId, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(inventoryManager));

        _ = app.MapPut("/inventory-items/{inventoryItemId:guid}/suppliers", async (
            Guid inventoryItemId,
            SetItemSuppliersRequest request,
            IPurchaseRequestService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.SetSuppliersAsync(null, inventoryItemId, request, cancellationToken)))
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
