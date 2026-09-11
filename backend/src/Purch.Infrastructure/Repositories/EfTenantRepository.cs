using Microsoft.EntityFrameworkCore;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfTenantRepository(PurchDbContext dbContext) : ITenantRepository
{
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Tenants.FirstOrDefaultAsync(tenant => tenant.Id == id, cancellationToken);
    }

    public void Add(Tenant tenant)
    {
        _ = dbContext.Tenants.Add(tenant);
    }
}
