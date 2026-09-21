using Purch.Domain.Enums;

namespace Purch.Application.Inventory;

public interface IInventoryMovementService
{
    Task<IReadOnlyList<InventoryMovementDto>> ListAsync(
        Guid? itemId,
        Guid? branchId,
        MovementType? type,
        DateTimeOffset? before = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    Task<InventoryMovementDto> RecordAsync(RecordMovementRequest request, CancellationToken cancellationToken = default);
}
