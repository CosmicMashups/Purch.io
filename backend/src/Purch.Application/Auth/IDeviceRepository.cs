using Purch.Domain.Entities;

namespace Purch.Application.Auth;

public interface IDeviceRepository
{
    Task<Device?> FindByPairingCodeAsync(string pairingCode, CancellationToken cancellationToken = default);

    Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>One of the few deliberately unscoped reads: used before a tenant is known (anonymous refresh
    /// and password reset), so it can't go through the tenant filter. Authenticated code must keep using GetByIdAsync.</summary>
    Task<Device?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Device>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Stages a new device for insert — call IUnitOfWork.SaveChangesAsync to commit.</summary>
    void Add(Device device);
}
