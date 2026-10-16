using Microsoft.EntityFrameworkCore;
using Purch.Application.Inventory.Import;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfImportBatchRepository(PurchDbContext dbContext) : IImportBatchRepository
{
    public Task<ImportBatch?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.ImportBatches.FirstOrDefaultAsync(batch => batch.Id == id, cancellationToken);
    }

    public Task<ImportBatch?> GetUntrackedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.ImportBatches.AsNoTracking().FirstOrDefaultAsync(batch => batch.Id == id, cancellationToken);
    }

    public Task<ImportBatch?> FindByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return dbContext.ImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(batch => batch.TenantId == tenantId && batch.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public void Add(ImportBatch batch)
    {
        _ = dbContext.ImportBatches.Add(batch);
    }
}
