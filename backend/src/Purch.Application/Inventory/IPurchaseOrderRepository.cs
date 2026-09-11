using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public interface IPurchaseOrderRepository
{
    Task<IReadOnlyList<PurchaseOrder>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseOrderLine>> ListLinesAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);

    Task<PurchaseOrderLine?> GetLineAsync(Guid lineId, CancellationToken cancellationToken = default);

    void Add(PurchaseOrder purchaseOrder);

    void AddLine(PurchaseOrderLine line);
}
