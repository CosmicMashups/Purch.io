using Microsoft.EntityFrameworkCore;
using Purch.Application.Sync;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfSyncedRecordRepository(PurchDbContext dbContext) : ISyncedRecordRepository
{
    public Task<SyncedRecord?> GetByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return dbContext.SyncedRecords
            .FirstOrDefaultAsync(record => record.TenantId == tenantId && record.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public Task<SyncedRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.SyncedRecords.FirstOrDefaultAsync(record => record.Id == id, cancellationToken);
    }

    public async Task<SyncedRecord?> GetCurrentWinnerAsync(Guid tenantId, string entityType, Guid entityId, CancellationToken cancellationToken = default)
    {
        return await dbContext.SyncedRecords
            .Where(record =>
                record.TenantId == tenantId
                && record.EntityType == entityType
                && record.EntityId == entityId
                && !record.FlaggedForReview)
            .OrderByDescending(record => record.ClientTimestamp)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SyncedRecord>> ListFlaggedAsync(Guid tenantId, int? limit = null, CancellationToken cancellationToken = default)
    {
        return await dbContext.SyncedRecords
            .Where(record => record.TenantId == tenantId && record.FlaggedForReview)
            .OrderByDescending(record => record.ClientTimestamp)
            .Take(Purch.Application.Common.Paging.ClampLimit(limit))
            .ToListAsync(cancellationToken);
    }

    public void Add(SyncedRecord record)
    {
        _ = dbContext.SyncedRecords.Add(record);
    }
}
