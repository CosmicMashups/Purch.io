using Microsoft.EntityFrameworkCore;
using Purch.Application.Pos;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfKioskPrepSequenceRepository(PurchDbContext dbContext) : IKioskPrepSequenceRepository
{
    public async Task<KioskPrepSequence> GetOrCreateTrackedAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.KioskPrepSequences.FirstOrDefaultAsync(
            sequence => sequence.TenantId == tenantId && sequence.BranchId == branchId,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var created = new KioskPrepSequence
        {
            TenantId = tenantId,
            BranchId = branchId,
            LastIssuedNumber = 0,
        };

        _ = dbContext.KioskPrepSequences.Add(created);
        return created;
    }
}
