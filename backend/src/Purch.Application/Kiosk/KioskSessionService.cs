using Purch.Application.Auth;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Enums;

namespace Purch.Application.Kiosk;

public sealed class KioskSessionService(
    IDeviceRepository deviceRepository,
    IPinHasher pinHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService) : IKioskSessionService
{
    public async Task<KioskSessionResult> PairAsync(KioskSessionRequest request, CancellationToken cancellationToken = default)
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

        // Same "invalid credentials" result for an unrecognized code, a code
        // belonging to a non-Kiosk device, and a wrong PIN — never reveal which.
        if (device is null || device.DeviceType != DeviceType.Kiosk || device.PairingPinHash is null ||
            !pinHasher.Verify(request.PairingPin, device.PairingPinHash))
        {
            return new KioskSessionResult.InvalidDevice();
        }

        var accessToken = jwtTokenService.IssueKioskAccessToken(device);
        var refreshToken = await refreshTokenService.IssueAsync(device.TenantId, null, device.Id, cancellationToken);
        return new KioskSessionResult.Success(accessToken, refreshToken);
    }
}
