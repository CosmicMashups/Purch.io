using Purch.Domain.Entities;

namespace Purch.Application.Onboarding;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Department>> ListByBranchAsync(Guid branchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Department>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    void Add(Department department);
}
