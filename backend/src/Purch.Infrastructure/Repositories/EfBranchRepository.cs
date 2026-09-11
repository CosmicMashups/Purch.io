using Microsoft.EntityFrameworkCore;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfBranchRepository(PurchDbContext dbContext) : IBranchRepository
{
    public Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Branches.FirstOrDefaultAsync(branch => branch.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Branch>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public void Add(Branch branch)
    {
        _ = dbContext.Branches.Add(branch);
    }
}
