using Purch.Domain.Entities;

namespace Purch.Application.Onboarding;

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Branch>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Stages a new branch for insert — call IUnitOfWork.SaveChangesAsync to commit.</summary>
    void Add(Branch branch);
}
