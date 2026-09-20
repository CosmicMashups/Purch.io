using Purch.Domain.Enums;

namespace Purch.Application.Auth;

public sealed class TokenRefreshService(
    IRefreshTokenService refreshTokenService,
    IUserRepository userRepository,
    IDeviceRepository deviceRepository,
    IJwtTokenService jwtTokenService) : ITokenRefreshService
{
    public async Task<TokenRefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var owner = await refreshTokenService.RedeemAsync(refreshToken, cancellationToken);
        if (owner is null)
        {
            return new TokenRefreshResult.InvalidToken();
        }

        var user = owner.UserId is { } userId ? await userRepository.GetByIdAsync(userId, cancellationToken) : null;
        var device = owner.DeviceId is { } deviceId ? await deviceRepository.GetByIdAsync(deviceId, cancellationToken) : null;

        // A deactivated staff member must not be able to keep renewing their session.
        if (user is { IsActive: false })
        {
            return new TokenRefreshResult.InvalidToken();
        }

        // A token bound to a device that no longer exists must die, not fall through to an
        // admin token below (which would silently drop the device binding of a staff session).
        if (owner.DeviceId is not null && device is null)
        {
            return new TokenRefreshResult.InvalidToken();
        }

        // Mirrors exactly which Issue* method originally produced the access token
        // this refresh token was paired with (see RefreshTokenOwner).
        string accessToken;
        if (user is not null && device is not null)
        {
            accessToken = jwtTokenService.IssueAccessToken(user, device);
        }
        else if (device is not null)
        {
            // Re-issue with the device's own role: OrderBoard/KitchenDisplay tokens must not
            // turn into Kiosk tokens on refresh (they'd lose their endpoints and gain cart rights).
            accessToken = device.DeviceType switch
            {
                DeviceType.OrderBoard => jwtTokenService.IssueUnattendedAccessToken(device, Role.OrderBoard),
                DeviceType.KitchenDisplay => jwtTokenService.IssueUnattendedAccessToken(device, Role.KitchenDisplay),
                DeviceType.Kiosk or DeviceType.Register => jwtTokenService.IssueKioskAccessToken(device),
                _ => throw new InvalidOperationException($"Unhandled {nameof(DeviceType)}: {device.DeviceType}"),
            };
        }
        else if (user is not null)
        {
            accessToken = jwtTokenService.IssueAdminAccessToken(user);
        }
        else
        {
            // The owning user/device was deleted since this refresh token was issued.
            return new TokenRefreshResult.InvalidToken();
        }

        var newRefreshToken = await refreshTokenService.IssueAsync(owner.TenantId, owner.UserId, owner.DeviceId, cancellationToken);
        return new TokenRefreshResult.Success(accessToken, newRefreshToken);
    }
}
