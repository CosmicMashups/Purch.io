using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed class DeviceManagementService(
    IDeviceRepository deviceRepository,
    IBranchRepository branchRepository,
    ICurrentTenantProvider currentTenantProvider,
    IPinHasher pinHasher,
    IRefreshTokenService refreshTokenService,
    IUnitOfWork unitOfWork) : IDeviceManagementService
{
    public async Task<IReadOnlyList<DeviceDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var devices = await deviceRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. devices.Select(ToDto)];
    }

    public async Task<DeviceDto> CreateAsync(CreateDeviceRequest request, CancellationToken cancellationToken = default)
    {
        // Confirms the branch both exists and belongs to this tenant (the query
        // filter makes a cross-tenant branch id come back null, same as everywhere else).
        _ = await branchRepository.GetByIdAsync(request.BranchId, cancellationToken)
            ?? throw new NotFoundException("Branch", request.BranchId);

        // Every unattended device type needs a pairing PIN — only an attended
        // Register terminal relies on a staff member's own PIN instead.
        if (request.DeviceType != DeviceType.Register && string.IsNullOrWhiteSpace(request.PairingPin))
        {
            throw new ValidationException(nameof(request.PairingPin), "A pairing PIN is required for this device type.");
        }

        var device = new Device
        {
            TenantId = CurrentTenantId,
            BranchId = request.BranchId,
            DeviceIdentifier = request.DeviceIdentifier,
            DeviceType = request.DeviceType,
            PairingCode = PairingCodeGenerator.Generate(),
            PairingPinHash = request.PairingPin is { Length: > 0 } pin ? pinHasher.Hash(pin) : null,
        };

        deviceRepository.Add(device);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(device);
    }

    public async Task<DeviceDto> ResetPairingCodeAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await GetOwnedDeviceAsync(deviceId, cancellationToken);

        device.PairingCode = PairingCodeGenerator.Generate();
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        // The old code is gone the moment it's overwritten (lookup is by value, not a
        // separate active/inactive flag), but a session already issued under it would
        // otherwise keep working until its refresh token naturally expires.
        await refreshTokenService.RevokeAllForDeviceAsync(device.Id, cancellationToken);

        return ToDto(device);
    }

    public async Task<DeviceDto> ResetPairingPinAsync(Guid deviceId, string newPin, CancellationToken cancellationToken = default)
    {
        var device = await GetOwnedDeviceAsync(deviceId, cancellationToken);

        // Same rule as device creation: every unattended device type needs a pairing PIN.
        if (device.DeviceType != DeviceType.Register && string.IsNullOrWhiteSpace(newPin))
        {
            throw new ValidationException(nameof(newPin), "A pairing PIN is required for this device type.");
        }

        device.PairingPinHash = string.IsNullOrWhiteSpace(newPin) ? null : pinHasher.Hash(newPin);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        await refreshTokenService.RevokeAllForDeviceAsync(device.Id, cancellationToken);

        return ToDto(device);
    }

    // The tenant query filter makes a cross-tenant device id come back null, same as the
    // branch lookup in CreateAsync above.
    private async Task<Device> GetOwnedDeviceAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        return await deviceRepository.GetByIdAsync(deviceId, cancellationToken)
            ?? throw new NotFoundException("Device", deviceId);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Device management requires an authenticated tenant context.");

    private static DeviceDto ToDto(Device device)
    {
        return new(device.Id, device.BranchId, device.PairingCode, device.DeviceIdentifier, device.DeviceType, device.LastSeenAt);
    }
}
