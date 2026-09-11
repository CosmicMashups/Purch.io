using Microsoft.EntityFrameworkCore;
using Purch.Application.Pos;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfReceiptSequenceRepository(PurchDbContext dbContext) : IReceiptSequenceRepository
{
    public async Task<ReceiptSequence> GetOrCreateTrackedAsync(Guid tenantId, Guid branchId, Guid deviceId, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.ReceiptSequences.FirstOrDefaultAsync(
            sequence => sequence.TenantId == tenantId && sequence.BranchId == branchId && sequence.DeviceId == deviceId,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var created = new ReceiptSequence
        {
            TenantId = tenantId,
            BranchId = branchId,
            DeviceId = deviceId,
            LastIssuedNumber = 0,
        };

        _ = dbContext.ReceiptSequences.Add(created);
        return created;
    }
}
