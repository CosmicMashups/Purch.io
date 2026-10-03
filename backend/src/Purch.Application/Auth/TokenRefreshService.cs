using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Auth;

public sealed class TokenRefreshService(
    IRefreshTokenService refreshTokenService,
    IUserRepository userRepository,
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

        var user = owner.UserId is { } userId ? await userRepository.GetByIdUnscopedAsync(userId, cancellationToken) : null;
        var device = owner.DeviceId is { } deviceId ? await deviceRepository.GetByIdUnscopedAsync(deviceId, cancellationToken) : null;

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
                DeviceType.WarehouseOfficer => jwtTokenService.IssueUnattendedAccessToken(device, Role.Warehouse),
                DeviceType.CustomerDisplay => jwtTokenService.IssueUnattendedAccessToken(device, Role.CustomerDisplay),
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
