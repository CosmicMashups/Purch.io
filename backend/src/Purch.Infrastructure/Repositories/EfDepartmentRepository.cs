using Microsoft.EntityFrameworkCore;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfDepartmentRepository(PurchDbContext dbContext) : IDepartmentRepository
{
    public Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Departments.FirstOrDefaultAsync(department => department.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Department>> ListByBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Departments
            .AsNoTracking()
            .Where(department => department.BranchId == branchId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Department>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Departments
            .AsNoTracking()
            .Where(department => department.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public void Add(Department department)
    {
        _ = dbContext.Departments.Add(department);
    }
}
