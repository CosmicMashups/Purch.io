using Purch.Application.Auth;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Enums;

namespace Purch.Application.Devices;

public sealed class UnattendedSessionService(
    IDeviceRepository deviceRepository,
    IPinHasher pinHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService) : IUnattendedSessionService
{
    public async Task<UnattendedSessionResult> PairAsync(
        UnattendedSessionRequest request,
        DeviceType expectedDeviceType,
        Role role,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DevicePairingCode))
        {
            throw new ValidationException(nameof(request.DevicePairingCode), "Device pairing code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PairingPin))
        {
            throw new ValidationException(nameof(request.PairingPin), "Pairing PIN is required.");
        }

        var device = await deviceRepository.FindByPairingCodeAsync(request.DevicePairingCode, cancellationToken);

        if (device is null || device.DeviceType != expectedDeviceType || device.PairingPinHash is null ||
            !pinHasher.Verify(request.PairingPin, device.PairingPinHash))
        {
            return new UnattendedSessionResult.InvalidDevice();
        }

        var accessToken = jwtTokenService.IssueUnattendedAccessToken(device, role);
        var refreshToken = await refreshTokenService.IssueAsync(device.TenantId, null, device.Id, cancellationToken);
        return new UnattendedSessionResult.Success(accessToken, refreshToken);
    }
}
