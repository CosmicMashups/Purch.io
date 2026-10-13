using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Auth;

/// <summary>The fields the per-request device session check reads.</summary>
public sealed record DeviceSessionState(DeviceStatus Status, bool IsDeleted, int SessionVersion);

public interface IDeviceRepository
{
    Task<Device?> FindByPairingCodeAsync(string pairingCode, CancellationToken cancellationToken = default);

    /// <summary>For a one-time pairing code, which is stored only as a hash. Tracked, and not tenant-filtered: the device
    /// has no session yet.</summary>
    Task<Device?> FindByPairingCodeHashAsync(string pairingCodeHash, CancellationToken cancellationToken = default);

    Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Just what the per-request session check needs, read untracked and without loading the whole
    /// device. Tenant-filtered like <see cref="GetByIdAsync"/>; null when the device does not exist for the tenant.</summary>
    Task<DeviceSessionState?> GetSessionStateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>One of the few deliberately unscoped reads: used before a tenant is known (anonymous refresh
    /// and password reset), so it can't go through the tenant filter. Authenticated code must keep using GetByIdAsync.</summary>
    Task<Device?> GetByIdUnscopedAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Device>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Stages a new device for insert — call IUnitOfWork.SaveChangesAsync to commit.</summary>
    void Add(Device device);
}
