using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

/// <summary>Incoming Receiving Reports: the record of a delivery, who took it in, and what condition it was in.
/// This is the one place stock arrives against a purchase order.</summary>
public sealed class IncomingReceivingService(
    IIncomingReceivingRepository reportRepository,
    IPurchaseOrderRepository purchaseOrderRepository,
    IInventoryMovementRepository movementRepository,
    ISupplierRepository supplierRepository,
    IItemRepository itemRepository,
    IItemStockService itemStockService,
    IBranchRepository branchRepository,
    IUserRepository userRepository,
    IBranchScopeGuard branchScopeGuard,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IIncomingReceivingService
{
    public async Task<IReadOnlyList<IncomingReceivingDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var reports = await reportRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var dtos = new List<IncomingReceivingDto>();
        foreach (var report in reports)
        {
            dtos.Add(await ToDtoAsync(report, cancellationToken));
        }
        return dtos;
    }

    public async Task<IncomingReceivingDto> GetAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        var report = await reportRepository.GetByIdAsync(reportId, cancellationToken)
            ?? throw new NotFoundException("Incoming receiving report", reportId);
        return await ToDtoAsync(report, cancellationToken);
    }

    public async Task<IncomingReceivingDto> CreateAsync(CreateIncomingReceivingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Lines.Count == 0)
        {
            throw new ValidationException(nameof(request.Lines), "At least one item is required.");
        }

        await branchScopeGuard.EnsureAllowedAsync(request.BranchId, cancellationToken);
        _ = await supplierRepository.GetByIdAsync(request.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Supplier", request.SupplierId);
        _ = await branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);

        var items = new Dictionary<Guid, Item>();
        foreach (var line in request.Lines)
        {
            if (line.QuantityReceived <= 0)
            {
                throw new ValidationException(nameof(line.QuantityReceived), "Quantity received must be greater than zero.");
            }

            if (line.UnitPrice < 0)
            {
                throw new ValidationException(nameof(line.UnitPrice), "Unit price cannot be negative.");
            }

            if (!items.ContainsKey(line.ItemId))
            {
                items[line.ItemId] = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken)
                    ?? throw new NotFoundException("Item", line.ItemId);
            }
        }

        PurchaseOrder? purchaseOrder = null;
        if (request.PurchaseOrderId is { } purchaseOrderId)
        {
            purchaseOrder = await LoadLinkablePurchaseOrderAsync(purchaseOrderId, request.SupplierId, request.BranchId, cancellationToken);
        }

        var report = new IncomingReceivingReport
        {
            TenantId = CurrentTenantId,
            SupplierId = request.SupplierId,
            BranchId = request.BranchId,
            ReceivedByUserId = CurrentUserId,
            DeliveryDate = request.DeliveryDate,
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
        };
        reportRepository.Add(report);

        var lines = new List<IncomingReceivingReportLine>();
        foreach (var lineRequest in request.Lines)
        {
            var line = new IncomingReceivingReportLine
            {
                TenantId = CurrentTenantId,
                ReportId = report.Id,
                ItemId = lineRequest.ItemId,
                QuantityReceived = lineRequest.QuantityReceived,
                Uom = string.IsNullOrWhiteSpace(lineRequest.Uom) ? "pc" : lineRequest.Uom.Trim(),
                UnitPrice = lineRequest.UnitPrice,
                Condition = lineRequest.Condition,
                Remark = lineRequest.Remark,
            };
            lines.Add(line);
            reportRepository.AddLine(line);

            if (line.Remark != ReceivingRemark.Accepted)
            {
                continue;
            }

            var inventoryItemId = await itemStockService.AdjustAsync(items[line.ItemId], line.QuantityReceived, cancellationToken);
            movementRepository.Add(new InventoryMovement
            {
                TenantId = CurrentTenantId,
                ItemId = line.ItemId,
                InventoryItemId = inventoryItemId,
                BranchId = request.BranchId,
                Type = MovementType.StockIn,
                Quantity = line.QuantityReceived,
                StaffUserId = CurrentUserId,
                Note = $"Received on incoming receiving report {report.Id}",
            });
        }

        if (purchaseOrder is not null)
        {
            await ApplyToPurchaseOrderAsync(purchaseOrder, report, lines, cancellationToken);
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(report, cancellationToken);
    }

    public async Task<IncomingReceivingDto> LinkPurchaseOrderAsync(Guid reportId, Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var report = await reportRepository.GetByIdAsync(reportId, cancellationToken)
            ?? throw new NotFoundException("Incoming receiving report", reportId);

        await branchScopeGuard.EnsureAllowedAsync(report.BranchId, cancellationToken);

        if (report.PurchaseOrderId is not null)
        {
            throw new ValidationException(nameof(report.PurchaseOrderId), "This report is already linked to a purchase order.");
        }

        var purchaseOrder = await LoadLinkablePurchaseOrderAsync(purchaseOrderId, report.SupplierId, report.BranchId, cancellationToken);
        var lines = await reportRepository.ListLinesAsync(report.Id, cancellationToken);

        await ApplyToPurchaseOrderAsync(purchaseOrder, report, lines, cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(report, cancellationToken);
    }

    private async Task<PurchaseOrder> LoadLinkablePurchaseOrderAsync(Guid purchaseOrderId, Guid supplierId, Guid branchId, CancellationToken cancellationToken)
    {
        var purchaseOrder = await purchaseOrderRepository.GetByIdAsync(purchaseOrderId, cancellationToken)
            ?? throw new NotFoundException("Purchase order", purchaseOrderId);

        if (purchaseOrder.SupplierId != supplierId)
        {
            throw new ValidationException(nameof(purchaseOrder.SupplierId), "The purchase order is for a different supplier.");
        }

        if (purchaseOrder.BranchId != branchId)
        {
            throw new ValidationException(nameof(purchaseOrder.BranchId), "The purchase order is for a different branch.");
        }

        if (purchaseOrder.Status is not (PurchaseOrderStatus.Sent or PurchaseOrderStatus.PartiallyReceived))
        {
            throw new ValidationException(nameof(purchaseOrder.Status), "Only a Submitted or Partially Delivered purchase order can be linked to a delivery.");
        }

        return purchaseOrder;
    }

    /// <summary>Counts the accepted quantities toward the order's lines (matched by item) and recomputes its status.</summary>
    private async Task ApplyToPurchaseOrderAsync(
        PurchaseOrder purchaseOrder,
        IncomingReceivingReport report,
        IReadOnlyList<IncomingReceivingReportLine> reportLines,
        CancellationToken cancellationToken)
    {
        var orderLines = await purchaseOrderRepository.ListLinesAsync(purchaseOrder.Id, cancellationToken);

        foreach (var group in reportLines.Where(line => line.Remark == ReceivingRemark.Accepted).GroupBy(line => line.ItemId))
        {
            var total = group.Sum(line => line.QuantityReceived);
            var remainingToAllocate = total;
            var matching = orderLines.Where(line => line.ItemId == group.Key).ToList();
            if (matching.Count == 0)
            {
                throw new ValidationException("Lines", "A received item is not on the purchase order.");
            }

            foreach (var orderLine in matching)
            {
                var take = Math.Min(remainingToAllocate, orderLine.QuantityOrdered - orderLine.QuantityReceived);
                if (take <= 0)
                {
                    continue;
                }

                orderLine.QuantityReceived += take;
                remainingToAllocate -= take;
            }

            if (remainingToAllocate > 0)
            {
                throw new ValidationException(
                    "Lines",
                    $"Accepting {total} would exceed what the purchase order still expects for this item.");
            }
        }

        report.PurchaseOrderId = purchaseOrder.Id;
        report.AppliedToPurchaseOrder = true;
        purchaseOrder.Status = orderLines.All(line => line.QuantityReceived >= line.QuantityOrdered)
            ? PurchaseOrderStatus.Received
            : PurchaseOrderStatus.PartiallyReceived;
    }

    private async Task<IncomingReceivingDto> ToDtoAsync(IncomingReceivingReport report, CancellationToken cancellationToken)
    {
        var supplier = await supplierRepository.GetByIdAsync(report.SupplierId, cancellationToken);
        var branch = await branchRepository.GetByIdAsync(report.BranchId, cancellationToken);
        var staff = await userRepository.FindActorAsync(report.ReceivedByUserId, cancellationToken);
        var lines = await reportRepository.ListLinesAsync(report.Id, cancellationToken);

        var lineDtos = new List<IncomingReceivingLineDto>();
        foreach (var line in lines)
        {
            var item = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken);
            lineDtos.Add(new IncomingReceivingLineDto(
                line.Id,
                line.ItemId,
                item?.Name ?? "(deleted item)",
                line.QuantityReceived,
                line.Uom,
                line.UnitPrice,
                line.Condition,
                line.Remark));
        }

        return new IncomingReceivingDto(
            report.Id,
            report.PurchaseOrderId,
            report.SupplierId,
            supplier?.Name ?? "(deleted supplier)",
            report.BranchId,
            branch?.Name ?? "(deleted branch)",
            report.ReceivedByUserId,
            staff?.Name ?? "(unknown staff)",
            report.DeliveryDate,
            report.Remarks,
            report.CreatedAt,
            lineDtos);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Incoming receiving requires an authenticated tenant context.");

    private Guid CurrentUserId => currentActorProvider.UserId
        ?? throw new InvalidOperationException("Incoming receiving requires an authenticated staff user.");
}
