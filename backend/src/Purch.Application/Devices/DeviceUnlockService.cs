using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Devices;

public sealed record RosterRequest(string DeviceCredential);

public sealed record UnlockRequest(string DeviceCredential, Guid MembershipId, string Pin);

public sealed record RosterEntryDto(Guid MembershipId, string Name, MembershipRole Role, bool HasPin);

public sealed record DeviceRosterDto(string? DeviceName, DeviceType DeviceType, IReadOnlyList<RosterEntryDto> People);

public abstract record RosterResult
{
    public sealed record Success(DeviceRosterDto Roster) : RosterResult;

    /// <summary>Unknown or revoked credential, or a device that does not take people (a kiosk or a display).</summary>
    public sealed record Invalid : RosterResult;
}

public abstract record UnlockResult
{
    public sealed record Success(string AccessToken, string RefreshToken, RosterEntryDto Person) : UnlockResult;

    /// <summary>Bad credential, unknown person, or a person who may not work on this device. One case on purpose.</summary>
    public sealed record Invalid : UnlockResult;

    public sealed record WrongPin(int AttemptsLeft) : UnlockResult;

    public sealed record Locked(DateTimeOffset Until) : UnlockResult;
}

public interface IDeviceUnlockService
{
    /// <summary>Who can pick their name on this till: active staff qualified for its duty and assigned to its branch, and
    /// every Admin and Manager.</summary>
    Task<RosterResult> GetRosterAsync(RosterRequest request, CancellationToken cancellationToken = default);

    /// <summary>The person chosen on the till types their own PIN. A wrong PIN counts against that person only, and locks
    /// them out for a while after a few misses.</summary>
    Task<UnlockResult> UnlockAsync(UnlockRequest request, CancellationToken cancellationToken = default);
}

public sealed class DeviceUnlockService(
    IDeviceCredentialRepository credentialRepository,
    IDeviceRepository deviceRepository,
    IAccountRepository accountRepository,
    IMembershipRepository membershipRepository,
    IPinHasher pinHasher,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IUnitOfWork unitOfWork) : IDeviceUnlockService
{
    /// <summary>Wrong PINs allowed for one person before they are locked out.</summary>
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    public async Task<RosterResult> GetRosterAsync(RosterRequest request, CancellationToken cancellationToken = default)
    {
        var device = await ActiveDeviceAsync(request.DeviceCredential, cancellationToken);
        if (device is null || device.DeviceType is not (DeviceType.Register or DeviceType.WarehouseOfficer))
        {
            return new RosterResult.Invalid();
        }

        var people = (await membershipRepository.ListByTenantUnscopedAsync(device.TenantId, cancellationToken))
            .Where(m => MembershipRoleMapper.CanWorkOn(m, device))
            .OrderBy(m => m.Account?.DisplayName)
            .Select(ToEntry)
            .ToList();
        return new RosterResult.Success(new DeviceRosterDto(device.Name, device.DeviceType, people));
    }

    public async Task<UnlockResult> UnlockAsync(UnlockRequest request, CancellationToken cancellationToken = default)
    {
        var device = await ActiveDeviceAsync(request.DeviceCredential, cancellationToken);
        var member = await accountRepository.GetMembershipAsync(request.MembershipId, cancellationToken);
        if (device is null || member is null || member.TenantId != device.TenantId || !MembershipRoleMapper.CanWorkOn(member, device))
        {
            return new UnlockResult.Invalid();
        }

        var now = DateTimeOffset.UtcNow;
        if (member.PinLockedUntil is { } until && until > now)
        {
            return new UnlockResult.Locked(until);
        }

        if (string.IsNullOrWhiteSpace(request.Pin) || member.PinHash is null || !pinHasher.Verify(request.Pin, member.PinHash))
        {
            // Saved on its own so a miss is never lost to anything that fails later: the point of counting is that it survives.
            member.PinFailedAttempts++;
            if (member.PinFailedAttempts >= MaxFailedAttempts)
            {
                member.PinFailedAttempts = 0;
                member.PinLockedUntil = now + LockoutDuration;
                _ = await unitOfWork.SaveChangesAsync(cancellationToken);
                return new UnlockResult.Locked(member.PinLockedUntil.Value);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            return new UnlockResult.WrongPin(MaxFailedAttempts - member.PinFailedAttempts);
        }

        member.PinFailedAttempts = 0;
        member.PinLockedUntil = null;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = jwtTokenService.IssueMembershipAccessToken(member, device);
        var refreshToken = await refreshTokenService.IssueForMembershipAsync(member.TenantId, member.Id, device.Id, cancellationToken);
        return new UnlockResult.Success(accessToken, refreshToken, ToEntry(member));
    }

    private async Task<Device?> ActiveDeviceAsync(string credential, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(credential))
        {
            return null;
        }

        var stored = await credentialRepository.FindByHashAsync(DevicePairingService.Hash(credential), cancellationToken);
        if (stored is null || stored.RevokedAt is not null)
        {
            return null;
        }

        var device = await deviceRepository.GetByIdUnscopedAsync(stored.DeviceId, cancellationToken);
        return device is { Status: DeviceStatus.Active } ? device : null;
    }

    private static RosterEntryDto ToEntry(Membership member)
    {
        return new RosterEntryDto(member.Id, member.Account?.DisplayName ?? string.Empty, member.Role, member.PinHash is not null);
    }
}
