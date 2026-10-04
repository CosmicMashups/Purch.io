using Purch.Application.Common;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Auth;

public sealed record RegisterChoice(Guid DeviceId, string Name);

public sealed record RegisterSessionRequest(Guid? DeviceId = null);

public abstract record RegisterSessionResult
{
    public sealed record Success(string AccessToken, string RefreshToken) : RegisterSessionResult;

    /// <summary>More than one Register and none chosen yet.</summary>
    public sealed record ChooseRegister(IReadOnlyList<RegisterChoice> Registers) : RegisterSessionResult;

    /// <summary>The business has no active Register, or the one asked for is not one this person may use.</summary>
    public sealed record NoRegister : RegisterSessionResult;
}

public interface IRegisterSessionService
{
    /// <summary>Lets an Admin or Manager signed in by email work the till: the session is re-issued tied to an active Register
    /// of their business, which is what every sale, cart and shift is recorded against.</summary>
    Task<RegisterSessionResult> StartAsync(Guid membershipId, RegisterSessionRequest request, CancellationToken cancellationToken = default);
}

public sealed class RegisterSessionService(
    IAccountRepository accountRepository,
    IDeviceRepository deviceRepository,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService) : IRegisterSessionService
{
    public async Task<RegisterSessionResult> StartAsync(Guid membershipId, RegisterSessionRequest request, CancellationToken cancellationToken = default)
    {
        var member = await accountRepository.GetMembershipAsync(membershipId, cancellationToken);
        if (member is null || !member.IsActive || member.Role == MembershipRole.Staff)
        {
            return new RegisterSessionResult.NoRegister();
        }

        var registers = (await deviceRepository.ListByTenantAsync(member.TenantId, cancellationToken))
            .Where(d => d.DeviceType == DeviceType.Register && d.Status == DeviceStatus.Active)
            .OrderBy(d => d.Name)
            .ToList();

        Device? device;
        if (request.DeviceId is { } id)
        {
            device = registers.FirstOrDefault(d => d.Id == id);
        }
        else if (registers.Count > 1)
        {
            return new RegisterSessionResult.ChooseRegister(registers.Select(d => new RegisterChoice(d.Id, d.Name ?? "Register")).ToList());
        }
        else
        {
            device = registers.FirstOrDefault();
        }

        if (device is null || !MembershipRoleMapper.CanWorkOn(member, device))
        {
            return new RegisterSessionResult.NoRegister();
        }

        var accessToken = jwtTokenService.IssueMembershipAccessToken(member, device);
        var refreshToken = await refreshTokenService.IssueForMembershipAsync(member.TenantId, member.Id, device.Id, cancellationToken);
        return new RegisterSessionResult.Success(accessToken, refreshToken);
    }
}
