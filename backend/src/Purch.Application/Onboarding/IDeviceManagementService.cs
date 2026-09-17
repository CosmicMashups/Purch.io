namespace Purch.Application.Onboarding;

public interface IDeviceManagementService
{
    Task<IReadOnlyList<DeviceDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<DeviceDto> CreateAsync(CreateDeviceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Regenerates the device's pairing code, immediately invalidating the old
    /// one, and revokes every refresh token issued to it.</summary>
    Task<DeviceDto> ResetPairingCodeAsync(Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>Overwrites the device's pairing PIN and revokes every refresh token
    /// issued to it. Only valid for device types that require a pairing PIN.</summary>
    Task<DeviceDto> ResetPairingPinAsync(Guid deviceId, string newPin, CancellationToken cancellationToken = default);
}
