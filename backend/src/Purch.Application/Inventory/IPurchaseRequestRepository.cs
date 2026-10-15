using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public interface IPurchaseRequestRepository
{
    Task<IReadOnlyList<PurchaseRequest>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<PurchaseRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseRequestLine>> ListLinesAsync(Guid purchaseRequestId, CancellationToken cancellationToken = default);

    /// <summary>Lines of every request that is still Open or Approved, for the "already requested" check.</summary>
    Task<IReadOnlyList<PurchaseRequestLine>> ListActiveLinesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    void Add(PurchaseRequest request);

    void AddLine(PurchaseRequestLine line);

    Task<IReadOnlyList<ItemSupplier>> ListSupplierLinksAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Tracked, so replacing an owner's links is one save.</summary>
    Task<IReadOnlyList<ItemSupplier>> ListSupplierLinksForAsync(Guid? itemId, Guid? inventoryItemId, CancellationToken cancellationToken = default);

    void AddSupplierLink(ItemSupplier link);

    void RemoveSupplierLink(ItemSupplier link);

    /// <summary>Tracked row for the branch and kind, created at zero when missing.</summary>
    Task<DocumentSequence> GetOrCreateSequenceAsync(Guid tenantId, Guid branchId, string kind, CancellationToken cancellationToken = default);
}
