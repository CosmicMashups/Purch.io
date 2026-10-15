using Purch.Application.Auth;
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
    IInventoryItemRepository inventoryItemRepository,
    IPurchaseRequestRepository purchaseRequestRepository,
    ITenantRepository tenantRepository,
    IUserRepository userRepository,
    ICurrentActorProvider currentActorProvider,
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

        var supplier = await supplierRepository.GetByIdAsync(request.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Supplier", request.SupplierId);
        var branch = await branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);
        var tenant = await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken);
        var vatRate = Purch.Domain.CountryProfiles.Get(tenant?.Country).VatRate;

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

            if (line.InventoryItemId is { } ingredientId)
            {
                _ = await inventoryItemRepository.GetByIdAsync(ingredientId, cancellationToken)
                    ?? throw new NotFoundException("Ingredient", ingredientId);
            }
            else
            {
                _ = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken)
                    ?? throw new NotFoundException("Item", line.ItemId);
            }
        }

        PurchaseRequest? purchaseRequest = null;
        if (request.PurchaseRequestId is { } purchaseRequestId)
        {
            purchaseRequest = await purchaseRequestRepository.GetByIdAsync(purchaseRequestId, cancellationToken)
                ?? throw new NotFoundException("Purchase request", purchaseRequestId);
        }

        var requestedBy = purchaseRequest?.RequestedByName;
        if (requestedBy is null && currentActorProvider.UserId is { } userId)
        {
            requestedBy = (await userRepository.FindActorAsync(userId, cancellationToken))?.Name;
        }

        var totals = PurchaseDocuments.Totals(
            request.Lines.Select(line => (line.QuantityOrdered, line.ExpectedUnitCost)),
            request.DiscountAmount,
            request.VatInclusive,
            vatRate);

        var purchaseOrder = new PurchaseOrder
        {
            TenantId = CurrentTenantId,
            SupplierId = request.SupplierId,
            BranchId = request.BranchId,
            Status = PurchaseOrderStatus.Draft,
            ControlNumber = await PurchaseDocuments.NextControlNumberAsync(purchaseRequestRepository, CurrentTenantId, branch, PurchaseDocuments.OrderKind, cancellationToken),
            PurchaseRequestId = purchaseRequest?.Id,
            RequestDate = purchaseRequest?.RequestDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            DateNeeded = request.DateNeeded ?? purchaseRequest?.DateNeeded,
            RequestedByName = requestedBy ?? string.Empty,
            DeliveryAddress = purchaseRequest?.DeliveryAddress ?? await ResolveDeliveryAddressAsync(branch, tenant, cancellationToken),
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? purchaseRequest?.Remarks : request.Remarks.Trim(),
            PaymentAccountName = supplier.PaymentAccountName,
            PaymentAccountNumber = supplier.PaymentAccountNumber,
            VatInclusive = request.VatInclusive,
            DiscountAmount = totals.Discount,
            VatRate = vatRate,
            Subtotal = totals.Subtotal,
            VatAmount = totals.Vat,
            OrderTotal = totals.Total,
        };
        purchaseOrderRepository.Add(purchaseOrder);

        foreach (var line in request.Lines)
        {
            purchaseOrderRepository.AddLine(new PurchaseOrderLine
            {
                TenantId = CurrentTenantId,
                PurchaseOrderId = purchaseOrder.Id,
                ItemId = line.InventoryItemId is null ? line.ItemId : Guid.Empty,
                InventoryItemId = line.InventoryItemId,
                Unit = string.IsNullOrWhiteSpace(line.Unit) ? "pc" : line.Unit.Trim(),
                QuantityOrdered = line.QuantityOrdered,
                QuantityReceived = 0,
                ExpectedUnitCost = line.ExpectedUnitCost,
            });
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(purchaseOrder, cancellationToken);
    }

    public async Task<PurchaseOrderDto> UpdateAsync(Guid purchaseOrderId, UpdatePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        var purchaseOrder = await purchaseOrderRepository.GetByIdAsync(purchaseOrderId, cancellationToken)
            ?? throw new NotFoundException("Purchase order", purchaseOrderId);

        await branchScopeGuard.EnsureAllowedAsync(purchaseOrder.BranchId, cancellationToken);

        if (purchaseOrder.Status != PurchaseOrderStatus.Draft)
        {
            throw new ValidationException(nameof(purchaseOrder.Status), "Only a Draft purchase order can be edited.");
        }

        if (request.Lines.Count == 0)
        {
            throw new ValidationException(nameof(request.Lines), "At least one item is required.");
        }

        var supplier = await supplierRepository.GetByIdAsync(request.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Supplier", request.SupplierId);

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

            if (line.InventoryItemId is { } ingredientId)
            {
                _ = await inventoryItemRepository.GetByIdAsync(ingredientId, cancellationToken)
                    ?? throw new NotFoundException("Ingredient", ingredientId);
            }
            else
            {
                _ = await itemRepository.GetByIdAsync(line.ItemId, cancellationToken)
                    ?? throw new NotFoundException("Item", line.ItemId);
            }
        }

        var tenant = await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken);
        var vatRate = Purch.Domain.CountryProfiles.Get(tenant?.Country).VatRate;
        var totals = PurchaseDocuments.Totals(
            request.Lines.Select(line => (line.QuantityOrdered, line.ExpectedUnitCost)),
            request.DiscountAmount,
            request.VatInclusive,
            vatRate);

        purchaseOrder.SupplierId = supplier.Id;
        purchaseOrder.PaymentAccountName = supplier.PaymentAccountName;
        purchaseOrder.PaymentAccountNumber = supplier.PaymentAccountNumber;
        purchaseOrder.DateNeeded = request.DateNeeded;
        purchaseOrder.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        purchaseOrder.VatInclusive = request.VatInclusive;
        purchaseOrder.DiscountAmount = totals.Discount;
        purchaseOrder.VatRate = vatRate;
        purchaseOrder.Subtotal = totals.Subtotal;
        purchaseOrder.VatAmount = totals.Vat;
        purchaseOrder.OrderTotal = totals.Total;

        foreach (var existing in await purchaseOrderRepository.ListLinesAsync(purchaseOrder.Id, cancellationToken))
        {
            purchaseOrderRepository.RemoveLine(existing);
        }

        foreach (var line in request.Lines)
        {
            purchaseOrderRepository.AddLine(new PurchaseOrderLine
            {
                TenantId = CurrentTenantId,
                PurchaseOrderId = purchaseOrder.Id,
                ItemId = line.InventoryItemId is null ? line.ItemId : Guid.Empty,
                InventoryItemId = line.InventoryItemId,
                Unit = string.IsNullOrWhiteSpace(line.Unit) ? "pc" : line.Unit.Trim(),
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
            var name = line.InventoryItemId is { } ingredientId
                ? (await inventoryItemRepository.GetByIdAsync(ingredientId, cancellationToken))?.Name
                : (await itemRepository.GetByIdAsync(line.ItemId, cancellationToken))?.Name;
            lineDtos.Add(new PurchaseOrderLineDto(
                line.Id,
                line.ItemId,
                name ?? "(deleted item)",
                line.QuantityOrdered,
                line.QuantityReceived,
                line.ExpectedUnitCost,
                line.InventoryItemId,
                line.Unit,
                Math.Round(line.QuantityOrdered * line.ExpectedUnitCost, 2)));
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
            [.. receipts.Select(report => new PurchaseOrderReceiptDto(report.Id, report.DeliveryDate))],
            purchaseOrder.ControlNumber,
            purchaseOrder.PurchaseRequestId,
            purchaseOrder.RequestDate,
            purchaseOrder.DateNeeded,
            purchaseOrder.RequestedByName,
            purchaseOrder.DeliveryAddress,
            purchaseOrder.Remarks,
            purchaseOrder.PaymentAccountName,
            purchaseOrder.PaymentAccountNumber,
            purchaseOrder.VatInclusive,
            purchaseOrder.Subtotal,
            purchaseOrder.DiscountAmount,
            purchaseOrder.VatRate,
            purchaseOrder.VatAmount,
            purchaseOrder.OrderTotal);
    }

    /// <summary>The branch address, or the registered address when the business has only the one branch.</summary>
    private async Task<string?> ResolveDeliveryAddressAsync(Branch branch, Tenant? tenant, CancellationToken cancellationToken)
    {
        var branches = await branchRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return PurchaseDocuments.DeliveryAddress(branch, tenant, branches.Count);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Purchase orders require an authenticated tenant context.");
}
