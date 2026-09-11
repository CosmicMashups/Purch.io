using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

public interface IInventoryMovementRepository
{
    Task<IReadOnlyList<InventoryMovement>> ListAsync(
        Guid tenantId,
        Guid? itemId,
        Guid? branchId,
        MovementType? type,
        CancellationToken cancellationToken = default);

    void Add(InventoryMovement movement);
}
