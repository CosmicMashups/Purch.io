using Purch.Application.Auth;
using Purch.Application.Catalog;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

/// <summary>Restock needs become a purchase request; an approved request is converted into one draft purchase order per
/// supplier. Items and ingredients carry the suppliers they are normally bought from.</summary>
public sealed class PurchaseRequestService(
    IPurchaseRequestRepository requestRepository,
    IPurchaseOrderRepository purchaseOrderRepository,
    IPurchaseOrderService purchaseOrderService,
    IItemRepository itemRepository,
    IItemStockService itemStockService,
    IInventoryItemRepository inventoryItemRepository,
    ISupplierRepository supplierRepository,
    IBranchRepository branchRepository,
    IBranchScopeGuard branchScopeGuard,
    ITenantRepository tenantRepository,
    IUserRepository userRepository,
    ICurrentActorProvider currentActorProvider,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IPurchaseRequestService
{
    public async Task<IReadOnlyList<PurchaseRequestDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var requests = await requestRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        var dtos = new List<PurchaseRequestDto>();
        foreach (var request in requests.OrderByDescending(r => r.CreatedAt))
        {
            dtos.Add(await ToDtoAsync(request, cancellationToken));
        }
        return dtos;
    }

    public async Task<PurchaseRequestDto> CreateAsync(CreatePurchaseRequestRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Lines.Count == 0)
        {
            throw new ValidationException(nameof(request.Lines), "At least one item is required.");
        }

        await branchScopeGuard.EnsureAllowedAsync(request.BranchId, cancellationToken);
        var branch = await branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);
        var tenant = await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken);

        var units = new List<string>();
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0)
            {
                throw new ValidationException(nameof(line.Quantity), "Quantity must be greater than zero.");
            }

            if ((line.ItemId is null) == (line.InventoryItemId is null))
            {
                throw new ValidationException(nameof(line.ItemId), "Each line needs an item or an ingredient.");
            }

            string? defaultUnit;
            if (line.InventoryItemId is { } ingredientId)
            {
                var ingredient = await inventoryItemRepository.GetByIdAsync(ingredientId, cancellationToken)
                    ?? throw new NotFoundException("Ingredient", ingredientId);
                defaultUnit = ingredient.BaseUnit;
            }
            else
            {
                var item = await itemRepository.GetByIdAsync(line.ItemId!.Value, cancellationToken)
                    ?? throw new NotFoundException("Item", line.ItemId.Value);
                defaultUnit = item.Unit;
            }

            units.Add(string.IsNullOrWhiteSpace(line.Unit) ? (string.IsNullOrWhiteSpace(defaultUnit) ? "pc" : defaultUnit) : line.Unit.Trim());
        }

        var userId = currentActorProvider.UserId ?? throw new ValidationException("User", "A signed-in user is required.");
        var user = await userRepository.FindActorAsync(userId, cancellationToken);
        var branchCount = (await branchRepository.ListByTenantAsync(CurrentTenantId, cancellationToken)).Count;

        var purchaseRequest = new PurchaseRequest
        {
            TenantId = CurrentTenantId,
            BranchId = branch.Id,
            ControlNumber = await PurchaseDocuments.NextControlNumberAsync(requestRepository, CurrentTenantId, branch, PurchaseDocuments.RequestKind, cancellationToken),
            RequestDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DateNeeded = request.DateNeeded,
            RequestedByUserId = userId,
            RequestedByName = user?.Name ?? string.Empty,
            DeliveryAddress = PurchaseDocuments.DeliveryAddress(branch, tenant, branchCount),
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
        };
        requestRepository.Add(purchaseRequest);

        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            requestRepository.AddLine(new PurchaseRequestLine
            {
                TenantId = CurrentTenantId,
                PurchaseRequestId = purchaseRequest.Id,
                ItemId = line.InventoryItemId is null ? line.ItemId!.Value : Guid.Empty,
                InventoryItemId = line.InventoryItemId,
                Quantity = line.Quantity,
                Unit = units[i],
            });
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(purchaseRequest, cancellationToken);
    }

    public async Task<PurchaseRequestDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request.Status != PurchaseRequestStatus.Open)
        {
            throw new ValidationException(nameof(request.Status), "Only an Open purchase request can be approved.");
        }

        request.Status = PurchaseRequestStatus.Approved;
        request.ApprovedByUserId = currentActorProvider.UserId;
        request.ApprovedAt = DateTimeOffset.UtcNow;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(request, cancellationToken);
    }

    public async Task<PurchaseRequestDto> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await LoadAsync(id, cancellationToken);
        if (request.Status is not (PurchaseRequestStatus.Open or PurchaseRequestStatus.Approved))
        {
            throw new ValidationException(nameof(request.Status), "Only an Open or Approved purchase request can be cancelled.");
        }

        request.Status = PurchaseRequestStatus.Cancelled;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(request, cancellationToken);
    }

    public async Task<IReadOnlyList<PurchaseOrderDto>> ConvertAsync(Guid id, ConvertPurchaseRequestRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Orders.Count == 0)
        {
            throw new ValidationException(nameof(request.Orders), "Choose a supplier for at least one line.");
        }

        var purchaseRequest = await LoadAsync(id, cancellationToken);
        if (purchaseRequest.Status != PurchaseRequestStatus.Approved)
        {
            throw new ValidationException(nameof(purchaseRequest.Status), "Only an Approved purchase request can be converted.");
        }

        var lines = (await requestRepository.ListLinesAsync(id, cancellationToken)).ToDictionary(line => line.Id);
        var used = new HashSet<Guid>();
        foreach (var order in request.Orders)
        {
            if (order.LineIds.Count == 0)
            {
                throw new ValidationException(nameof(order.LineIds), "An order needs at least one line.");
            }

            foreach (var lineId in order.LineIds)
            {
                if (!lines.ContainsKey(lineId))
                {
                    throw new ValidationException(nameof(order.LineIds), "A line does not belong to this purchase request.");
                }

                if (order.UnitPrices?.GetValueOrDefault(lineId) is < 0)
                {
                    throw new ValidationException(nameof(order.UnitPrices), "Unit price cannot be negative.");
                }

                if (!used.Add(lineId))
                {
                    throw new ValidationException(nameof(order.LineIds), "A line can only go on one purchase order.");
                }
            }
        }

        var costs = await CostsAsync(lines.Values, cancellationToken);
        var orders = new List<PurchaseOrderDto>();
        foreach (var order in request.Orders)
        {
            var orderLines = order.LineIds.Select(lineId => lines[lineId]).Select(line => new CreatePurchaseOrderLineRequest(
                line.ItemId,
                line.Quantity,
                order.UnitPrices is not null && order.UnitPrices.TryGetValue(line.Id, out var price) ? price : costs.GetValueOrDefault(line.Id),
                line.InventoryItemId,
                line.Unit)).ToList();

            orders.Add(await purchaseOrderService.CreateAsync(
                new CreatePurchaseOrderRequest(
                    order.SupplierId,
                    purchaseRequest.BranchId,
                    orderLines,
                    purchaseRequest.DateNeeded,
                    purchaseRequest.Remarks,
                    order.VatInclusive,
                    order.DiscountAmount,
                    purchaseRequest.Id),
                cancellationToken));
        }

        purchaseRequest.Status = PurchaseRequestStatus.Converted;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return orders;
    }

    public async Task<IReadOnlyList<RestockSuggestionDto>> GetRestockSuggestionsAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(CurrentTenantId, cancellationToken);
        var separate = tenant is { UseSeparateInventoryTracking: true };
        var canSeeCosts = currentActorProvider.CanSeeCosts;

        var suppliers = (await supplierRepository.ListByTenantAsync(CurrentTenantId, cancellationToken)).ToDictionary(s => s.Id);
        var links = await requestRepository.ListSupplierLinksAsync(CurrentTenantId, cancellationToken);
        var pending = await PendingAsync(cancellationToken);

        IReadOnlyList<SupplierLinkDto> LinksFor(Guid? itemId, Guid? inventoryItemId) =>
            [.. links
                .Where(l => (itemId is not null && l.ItemId == itemId) || (inventoryItemId is not null && l.InventoryItemId == inventoryItemId))
                .Where(l => suppliers.ContainsKey(l.SupplierId))
                .OrderByDescending(l => l.IsPreferred)
                .ThenBy(l => suppliers[l.SupplierId].Name)
                .Select(l => new SupplierLinkDto(l.SupplierId, suppliers[l.SupplierId].Name, l.IsPreferred, canSeeCosts ? l.UnitCost : null))];

        var rows = new List<RestockSuggestionDto>();

        var items = (await itemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken)).Where(item => item.IsActive).ToList();
        var onHand = await itemStockService.GetOnHandAsync(items, CurrentTenantId, cancellationToken);
        foreach (var item in items.Where(item => onHand.ContainsKey(item.Id)))
        {
            var stock = onHand[item.Id];
            var threshold = item.LowStockThreshold ?? 0m;
            if (stock > 0 && (item.LowStockThreshold is null || stock > threshold))
            {
                continue;
            }

            rows.Add(new RestockSuggestionDto(
                item.Id,
                null,
                item.Name,
                stock,
                threshold,
                Suggested(threshold, stock),
                string.IsNullOrWhiteSpace(item.Unit) ? "pc" : item.Unit,
                canSeeCosts ? item.CostPrice : null,
                LinksFor(item.Id, null),
                pending.GetValueOrDefault((item.Id, null))));
        }

        if (separate)
        {
            var ingredients = (await inventoryItemRepository.ListByTenantAsync(CurrentTenantId, cancellationToken))
                .Where(i => i.IsActive && !i.IsAutoCreatedForItem);
            foreach (var ingredient in ingredients)
            {
                var threshold = ingredient.LowStockThreshold ?? 0m;
                if (ingredient.QuantityOnHand > 0 && (ingredient.LowStockThreshold is null || ingredient.QuantityOnHand > threshold))
                {
                    continue;
                }

                rows.Add(new RestockSuggestionDto(
                    null,
                    ingredient.Id,
                    ingredient.Name,
                    ingredient.QuantityOnHand,
                    threshold,
                    Suggested(threshold, ingredient.QuantityOnHand),
                    ingredient.BaseUnit,
                    canSeeCosts ? ingredient.UnitCost : null,
                    LinksFor(null, ingredient.Id),
                    pending.GetValueOrDefault((null, ingredient.Id))));
            }
        }

        return [.. rows.OrderBy(row => row.OnHand <= 0 ? 0 : 1).ThenBy(row => row.OnHand).ThenBy(row => row.Name)];
    }

    public async Task<IReadOnlyList<SupplierLinkDto>> GetSuppliersAsync(Guid? itemId, Guid? inventoryItemId, CancellationToken cancellationToken = default)
    {
        var links = await requestRepository.ListSupplierLinksForAsync(itemId, inventoryItemId, cancellationToken);
        var dtos = new List<SupplierLinkDto>();
        foreach (var link in links.OrderByDescending(l => l.IsPreferred))
        {
            var supplier = await supplierRepository.GetByIdAsync(link.SupplierId, cancellationToken);
            dtos.Add(new SupplierLinkDto(link.SupplierId, supplier?.Name ?? "(deleted supplier)", link.IsPreferred, currentActorProvider.CanSeeCosts ? link.UnitCost : null));
        }
        return dtos;
    }

    public async Task<IReadOnlyList<SupplierLinkDto>> SetSuppliersAsync(Guid? itemId, Guid? inventoryItemId, SetItemSuppliersRequest request, CancellationToken cancellationToken = default)
    {
        if ((itemId is null) == (inventoryItemId is null))
        {
            throw new ValidationException(nameof(itemId), "Set the suppliers of an item or of an ingredient.");
        }

        if (request.Suppliers.Select(s => s.SupplierId).Distinct().Count() != request.Suppliers.Count)
        {
            throw new ValidationException(nameof(request.Suppliers), "A supplier can only be listed once.");
        }

        if (request.Suppliers.Any(s => s.UnitCost < 0))
        {
            throw new ValidationException(nameof(request.Suppliers), "Unit cost cannot be negative.");
        }

        foreach (var entry in request.Suppliers)
        {
            _ = await supplierRepository.GetByIdAsync(entry.SupplierId, cancellationToken)
                ?? throw new NotFoundException("Supplier", entry.SupplierId);
        }

        if (itemId is { } id)
        {
            _ = await itemRepository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Item", id);
        }
        else
        {
            _ = await inventoryItemRepository.GetByIdAsync(inventoryItemId!.Value, cancellationToken)
                ?? throw new NotFoundException("Ingredient", inventoryItemId.Value);
        }

        // One preferred supplier: the one marked, else the only one there is.
        var preferred = request.Suppliers.FirstOrDefault(s => s.IsPreferred)?.SupplierId
            ?? (request.Suppliers.Count == 1 ? request.Suppliers[0].SupplierId : (Guid?)null);

        foreach (var existing in await requestRepository.ListSupplierLinksForAsync(itemId, inventoryItemId, cancellationToken))
        {
            requestRepository.RemoveSupplierLink(existing);
        }

        foreach (var entry in request.Suppliers)
        {
            requestRepository.AddSupplierLink(new ItemSupplier
            {
                TenantId = CurrentTenantId,
                ItemId = itemId,
                InventoryItemId = inventoryItemId,
                SupplierId = entry.SupplierId,
                IsPreferred = entry.SupplierId == preferred,
                UnitCost = entry.UnitCost,
            });
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetSuppliersAsync(itemId, inventoryItemId, cancellationToken);
    }

    /// <summary>What is still short, rounded up to a whole unit; at least one when out of stock with no level set.</summary>
    private static decimal Suggested(decimal threshold, decimal onHand) => Math.Max(Math.Ceiling(threshold - onHand), 1m);

    /// <summary>Items and ingredients already covered by an open request or an open order, keyed by (item, ingredient).</summary>
    private async Task<Dictionary<(Guid? ItemId, Guid? InventoryItemId), string>> PendingAsync(CancellationToken cancellationToken)
    {
        var pending = new Dictionary<(Guid?, Guid?), string>();

        var requests = (await requestRepository.ListByTenantAsync(CurrentTenantId, cancellationToken))
            .Where(r => r.Status is PurchaseRequestStatus.Open or PurchaseRequestStatus.Approved)
            .ToDictionary(r => r.Id);
        foreach (var line in await requestRepository.ListActiveLinesAsync(CurrentTenantId, cancellationToken))
        {
            if (requests.TryGetValue(line.PurchaseRequestId, out var request))
            {
                pending[Key(line.ItemId, line.InventoryItemId)] = request.ControlNumber;
            }
        }

        var open = (await purchaseOrderRepository.ListByTenantAsync(CurrentTenantId, cancellationToken))
            .Where(o => o.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Sent or PurchaseOrderStatus.PartiallyReceived);
        foreach (var order in open)
        {
            foreach (var line in await purchaseOrderRepository.ListLinesAsync(order.Id, cancellationToken))
            {
                if (line.QuantityReceived < line.QuantityOrdered)
                {
                    pending[Key(line.ItemId, line.InventoryItemId)] = string.IsNullOrEmpty(order.ControlNumber) ? "Purchase order" : order.ControlNumber;
                }
            }
        }

        return pending;

        static (Guid?, Guid?) Key(Guid itemId, Guid? inventoryItemId) =>
            inventoryItemId is { } ingredientId ? (null, ingredientId) : (itemId, null);
    }

    /// <summary>Expected unit cost for each request line: the preferred supplier link's cost, else the item or ingredient cost, else 0.</summary>
    private async Task<Dictionary<Guid, decimal>> CostsAsync(IEnumerable<PurchaseRequestLine> lines, CancellationToken cancellationToken)
    {
        var links = await requestRepository.ListSupplierLinksAsync(CurrentTenantId, cancellationToken);
        var result = new Dictionary<Guid, decimal>();
        foreach (var line in lines)
        {
            var link = links
                .Where(l => line.InventoryItemId is { } ingredientId ? l.InventoryItemId == ingredientId : l.ItemId == line.ItemId)
                .OrderByDescending(l => l.IsPreferred)
                .FirstOrDefault(l => l.UnitCost is not null);
            if (link?.UnitCost is { } linked)
            {
                result[line.Id] = linked;
            }
            else if (line.InventoryItemId is { } ingredientId)
            {
                result[line.Id] = (await inventoryItemRepository.GetByIdAsync(ingredientId, cancellationToken))?.UnitCost ?? 0m;
            }
            else
            {
                result[line.Id] = (await itemRepository.GetByIdAsync(line.ItemId, cancellationToken))?.CostPrice ?? 0m;
            }
        }
        return result;
    }

    private async Task<PurchaseRequest> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var request = await requestRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Purchase request", id);
        await branchScopeGuard.EnsureAllowedAsync(request.BranchId, cancellationToken);
        return request;
    }

    private async Task<PurchaseRequestDto> ToDtoAsync(PurchaseRequest request, CancellationToken cancellationToken)
    {
        var branch = await branchRepository.GetByIdAsync(request.BranchId, cancellationToken);
        var lines = new List<PurchaseRequestLineDto>();
        foreach (var line in await requestRepository.ListLinesAsync(request.Id, cancellationToken))
        {
            var name = line.InventoryItemId is { } ingredientId
                ? (await inventoryItemRepository.GetByIdAsync(ingredientId, cancellationToken))?.Name
                : (await itemRepository.GetByIdAsync(line.ItemId, cancellationToken))?.Name;
            lines.Add(new PurchaseRequestLineDto(line.Id, line.ItemId, line.InventoryItemId, name ?? "(deleted item)", line.Quantity, line.Unit));
        }

        return new PurchaseRequestDto(
            request.Id,
            request.ControlNumber,
            request.BranchId,
            branch?.Name ?? "(deleted branch)",
            request.RequestDate,
            request.DateNeeded,
            request.RequestedByName,
            request.DeliveryAddress,
            request.Remarks,
            request.Status,
            lines);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Purchase requests require an authenticated tenant context.");
}
