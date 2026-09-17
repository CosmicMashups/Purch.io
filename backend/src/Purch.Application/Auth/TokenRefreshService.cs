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

        // Mirrors exactly which Issue* method originally produced the access token
        // this refresh token was paired with (see RefreshTokenOwner).
        string accessToken;
        if (user is not null && device is not null)
        {
            accessToken = jwtTokenService.IssueAccessToken(user, device);
        }
        else if (device is not null)
        {
            accessToken = jwtTokenService.IssueKioskAccessToken(device);
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
