using Purch.Domain.Entities;

namespace Purch.Application.Onboarding;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Stages a new tenant for insert — call IUnitOfWork.SaveChangesAsync to commit.</summary>
    void Add(Tenant tenant);
}
