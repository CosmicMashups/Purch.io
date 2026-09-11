using Purch.Domain.Entities;

namespace Purch.Application.Inventory;

public interface IBranchTransferRepository
{
    Task<IReadOnlyList<BranchTransfer>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<BranchTransfer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BranchTransferLine>> ListLinesAsync(Guid branchTransferId, CancellationToken cancellationToken = default);

    void Add(BranchTransfer branchTransfer);

    void AddLine(BranchTransferLine line);
}
