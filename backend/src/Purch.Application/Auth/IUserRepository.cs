using Purch.Domain.Entities;

namespace Purch.Application.Auth;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetActiveUsersByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
