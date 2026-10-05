using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

/// <summary>C5 — creating a PO against a supplier, sending it, and receiving
/// tracking deliveries against it. Stock arrives through Incoming Receiving Reports.</summary>
public sealed class PurchaseOrderService(
    IPurchaseOrderRepository purchaseOrderRepository,
    IIncomingReceivingRepository incomingReceivingRepository,
    ISupplierRepository supplierRepository,
    IItemRepository itemRepository,
    IBranchRepository branchRepository,
    IBranchScopeGuard branchScopeGuard,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IPurchaseOrderService
{
    public async Task<IReadOnlyList<PurchaseOrderDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var purchaseOrders = await purchaseOrderRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);

        var dtos = new List<PurchaseOrderDto>();
        foreach (var purchaseOrder in purchaseOrders)
        {
            dtos.Add(await ToDtoAsync(purchaseOrder, cancellationToken));
        }
        return dtos;
    }

    public async Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default)
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

        foreach (var line in request.Lines)
        {
            if (line.QuantityOrdered <= 0)
            {
                throw new ValidationException(nameof(line.QuantityOrdered), "Quantity ordered must be greater than zero.");
            }

            if (line.ExpectedUnitCost < 0)
            {
                throw new ValidationException(nameof(line.ExpectedUnitCost), "Expected unit cost cannot be negative.");
            }

            _ = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken)
                ?? throw new NotFoundException("Item", line.ItemId);
        }

        var purchaseOrder = new PurchaseOrder
        {
            TenantId = CurrentTenantId,
            SupplierId = request.SupplierId,
            BranchId = request.BranchId,
            Status = PurchaseOrderStatus.Draft,
        };
        purchaseOrderRepository.Add(purchaseOrder);

        foreach (var line in request.Lines)
        {
            purchaseOrderRepository.AddLine(new PurchaseOrderLine
            {
                TenantId = CurrentTenantId,
                PurchaseOrderId = purchaseOrder.Id,
                ItemId = line.ItemId,
                QuantityOrdered = line.QuantityOrdered,
                QuantityReceived = 0,
                ExpectedUnitCost = line.ExpectedUnitCost,
            });
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(purchaseOrder, cancellationToken);
    }

    public async Task<PurchaseOrderDto> MarkSentAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var purchaseOrder = await purchaseOrderRepository.GetByIdAsync(purchaseOrderId, cancellationToken)
            ?? throw new NotFoundException("Purchase order", purchaseOrderId);

        await branchScopeGuard.EnsureAllowedAsync(purchaseOrder.BranchId, cancellationToken);

        if (purchaseOrder.Status != PurchaseOrderStatus.Draft)
        {
            throw new ValidationException(nameof(purchaseOrder.Status), "Only a Draft purchase order can be sent.");
        }

        purchaseOrder.Status = PurchaseOrderStatus.Sent;
        purchaseOrder.SentAt = DateTimeOffset.UtcNow;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(purchaseOrder, cancellationToken);
    }

    public async Task<PurchaseOrderDto> CancelAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var purchaseOrder = await purchaseOrderRepository.GetByIdAsync(purchaseOrderId, cancellationToken)
            ?? throw new NotFoundException("Purchase order", purchaseOrderId);

        await branchScopeGuard.EnsureAllowedAsync(purchaseOrder.BranchId, cancellationToken);

        if (purchaseOrder.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Sent))
        {
            throw new ValidationException(nameof(purchaseOrder.Status), "Only a Draft or Sent purchase order can be cancelled.");
        }

        purchaseOrder.Status = PurchaseOrderStatus.Cancelled;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(purchaseOrder, cancellationToken);
    }

    private async Task<PurchaseOrderDto> ToDtoAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken)
    {
        var supplier = await supplierRepository.GetByIdAsync(purchaseOrder.SupplierId, cancellationToken);
        var branch = await branchRepository.GetByIdAsync(purchaseOrder.BranchId, cancellationToken);
        var lines = await purchaseOrderRepository.ListLinesAsync(purchaseOrder.Id, cancellationToken);

        var lineDtos = new List<PurchaseOrderLineDto>();
        foreach (var line in lines)
        {
            var item = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken);
            lineDtos.Add(new PurchaseOrderLineDto(
                line.Id,
                line.ItemId,
                item?.Name ?? "(deleted item)",
                line.QuantityOrdered,
                line.QuantityReceived,
                line.ExpectedUnitCost));
        }

        var receipts = await incomingReceivingRepository.ListByPurchaseOrderAsync(purchaseOrder.Id, cancellationToken);

        return new PurchaseOrderDto(
            purchaseOrder.Id,
            purchaseOrder.SupplierId,
            supplier?.Name ?? "(deleted supplier)",
            purchaseOrder.BranchId,
            branch?.Name ?? "(deleted branch)",
            purchaseOrder.Status,
            purchaseOrder.SentAt,
            lineDtos,
            [.. receipts.Select(report => new PurchaseOrderReceiptDto(report.Id, report.DeliveryDate))]);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Purchase orders require an authenticated tenant context.");
}
