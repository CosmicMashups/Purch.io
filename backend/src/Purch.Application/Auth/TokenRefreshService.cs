using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Auth;

public sealed class TokenRefreshService(
    IRefreshTokenService refreshTokenService,
    IDeviceRepository deviceRepository,
    IAccountRepository accountRepository,
    IJwtTokenService jwtTokenService) : ITokenRefreshService
{
    public async Task<TokenRefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var owner = await refreshTokenService.RedeemAsync(refreshToken, cancellationToken);
        if (owner is null)
        {
            return new TokenRefreshResult.InvalidToken();
        }

        if (owner.MembershipId is { } membershipId)
        {
            var membership = await accountRepository.GetMembershipAsync(membershipId, cancellationToken);

            // A person who has been deactivated, or whose business role no longer lets them sign in, stops renewing.
            if (membership is not { IsActive: true } || MembershipRoleMapper.ToApiRole(membership) is null)
            {
                return new TokenRefreshResult.InvalidToken();
            }

            Device? sessionDevice = null;
            if (owner.DeviceId is { } sessionDeviceId)
            {
                sessionDevice = await deviceRepository.GetByIdUnscopedAsync(sessionDeviceId, cancellationToken);
                if (sessionDevice is null || !MembershipRoleMapper.CanWorkOn(membership, sessionDevice))
                {
                    return new TokenRefreshResult.InvalidToken();
                }
            }

            var membershipAccess = sessionDevice is null
                ? jwtTokenService.IssueMembershipAccessToken(membership)
                : jwtTokenService.IssueMembershipAccessToken(membership, sessionDevice);
            var membershipRefresh = await refreshTokenService.IssueForMembershipAsync(owner.TenantId, membership.Id, sessionDevice?.Id, cancellationToken);
            return new TokenRefreshResult.Success(membershipAccess, membershipRefresh);
        }

        // Only a person's session (a membership) or an unattended device's session can renew. A refresh token from the old
        // sign-in, which named a user and no membership, ends here.
        if (owner.UserId is not null || owner.DeviceId is not { } deviceId)
        {
            return new TokenRefreshResult.InvalidToken();
        }

        var device = await deviceRepository.GetByIdUnscopedAsync(deviceId, cancellationToken);
        if (device is not { Status: DeviceStatus.Active })
        {
            return new TokenRefreshResult.InvalidToken();
        }

        // Re-issue with the device's own role: OrderBoard/KitchenDisplay/CustomerDisplay tokens must not turn into Kiosk tokens
        // on refresh (they'd lose their endpoints and gain cart rights).
        string accessToken = device.DeviceType switch
        {
            DeviceType.OrderBoard => jwtTokenService.IssueUnattendedAccessToken(device, Role.OrderBoard),
            DeviceType.KitchenDisplay => jwtTokenService.IssueUnattendedAccessToken(device, Role.KitchenDisplay),
            DeviceType.CustomerDisplay => jwtTokenService.IssueUnattendedAccessToken(device, Role.CustomerDisplay),
            DeviceType.Kiosk => jwtTokenService.IssueKioskAccessToken(device),

            // A Register or Warehouse device is only ever used by a person (a membership session, handled above).
            _ => string.Empty,
        };
        if (accessToken.Length == 0)
        {
            return new TokenRefreshResult.InvalidToken();
        }

        var newRefreshToken = await refreshTokenService.IssueAsync(owner.TenantId, null, device.Id, cancellationToken);
        return new TokenRefreshResult.Success(accessToken, newRefreshToken);
    }
}
