using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Domain.Entities;

namespace Purch.Application.Onboarding;

/// <summary>Listing only. Devices are created, paired again and revoked through <see cref="Purch.Application.Devices.IDevicePairingService"/>.</summary>
public sealed class DeviceManagementService(IDeviceRepository deviceRepository, ICurrentTenantProvider currentTenantProvider) : IDeviceManagementService
{
    public async Task<IReadOnlyList<DeviceDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = currentTenantProvider.TenantId
            ?? throw new InvalidOperationException("Device management requires an authenticated tenant context.");
        var devices = await deviceRepository.ListByTenantAsync(tenantId, cancellationToken);
        return [.. devices.Select(ToDto)];
    }

    public static DeviceDto ToDto(Device device)
    {
        return new DeviceDto(
            device.Id,
            device.BranchId,
            device.DeviceIdentifier,
            device.DeviceType,
            device.LastSeenAt,
            device.Name,
            device.Status,
            device.PairedAt,
            device.PairingCodeExpiresAt,
            device.LinkedRegisterDeviceId);
    }
}
