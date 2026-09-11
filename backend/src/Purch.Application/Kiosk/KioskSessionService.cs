using Purch.Application.Auth;
using Purch.Application.Common.Exceptions;

namespace Purch.Application.Kiosk;

public sealed class KioskSessionService(
    IDeviceRepository deviceRepository,
    IJwtTokenService jwtTokenService) : IKioskSessionService
{
    public async Task<KioskSessionResult> PairAsync(KioskSessionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DevicePairingCode))
        {
            throw new ValidationException(nameof(request.DevicePairingCode), "Device pairing code is required.");
        }

        var device = await deviceRepository.FindByPairingCodeAsync(request.DevicePairingCode, cancellationToken);
        if (device is null)
        {
            return new KioskSessionResult.InvalidDevice();
        }

        var accessToken = jwtTokenService.IssueKioskAccessToken(device);
        return new KioskSessionResult.Success(accessToken);
    }
}
