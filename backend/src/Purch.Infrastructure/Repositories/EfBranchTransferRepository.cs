using Microsoft.EntityFrameworkCore;
using Purch.Application.Inventory;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfBranchTransferRepository(PurchDbContext dbContext) : IBranchTransferRepository
{
    public async Task<IReadOnlyList<BranchTransfer>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.BranchTransfers
            .AsNoTracking()
            .Where(transfer => transfer.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public Task<BranchTransfer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.BranchTransfers.FirstOrDefaultAsync(transfer => transfer.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<BranchTransferLine>> ListLinesAsync(Guid branchTransferId, CancellationToken cancellationToken = default)
    {
        return await dbContext.BranchTransferLines
            .AsNoTracking()
            .Where(line => line.BranchTransferId == branchTransferId)
            .ToListAsync(cancellationToken);
    }

    public void Add(BranchTransfer branchTransfer)
    {
        _ = dbContext.BranchTransfers.Add(branchTransfer);
    }

    public void AddLine(BranchTransferLine line)
    {
        _ = dbContext.BranchTransferLines.Add(line);
    }
}
