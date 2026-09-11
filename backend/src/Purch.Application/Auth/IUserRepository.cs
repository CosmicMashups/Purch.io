using Purch.Domain.Entities;

namespace Purch.Application.Auth;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetActiveUsersByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>All staff for the current tenant, active or not — for the staff management screen (A4).</summary>
    Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Stages a new user for insert — call IUnitOfWork.SaveChangesAsync to commit.</summary>
    void Add(User user);
}
