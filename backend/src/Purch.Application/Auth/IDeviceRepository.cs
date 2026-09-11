using Purch.Domain.Entities;

namespace Purch.Application.Auth;

public interface IDeviceRepository
{
    Task<Device?> FindByPairingCodeAsync(string pairingCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Device>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Stages a new device for insert — call IUnitOfWork.SaveChangesAsync to commit.</summary>
    void Add(Device device);
}
