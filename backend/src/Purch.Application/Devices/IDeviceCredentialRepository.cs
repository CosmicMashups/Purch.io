using Purch.Domain.Entities;

namespace Purch.Application.Devices;

public interface IDeviceCredentialRepository
{
    /// <summary>Tracked, and deliberately not tenant-filtered: the device has no session yet, so the secret itself is the credential.</summary>
    Task<DeviceCredential?> FindByHashAsync(string credentialHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceCredential>> ListActiveByDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);

    void Add(DeviceCredential credential);
}
