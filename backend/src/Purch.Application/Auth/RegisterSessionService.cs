using Purch.Application.Common;
using Purch.Application.Onboarding;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Auth;

public sealed record RegisterChoice(Guid DeviceId, string Name);

public sealed record RegisterSessionRequest(Guid? DeviceId = null);

public abstract record RegisterSessionResult
{
    public sealed record Success(string AccessToken, string RefreshToken, SupervisorAttestation? Attestation = null) : RegisterSessionResult;

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
    IBranchRepository branchRepository,
    IUnitOfWork unitOfWork,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    ISupervisorAttestationService attestationService) : IRegisterSessionService
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

        // An Admin or Manager never has to pair a device: a business with no Register gets one made for them.
        if (registers.Count == 0)
        {
            var branch = (await branchRepository.ListByTenantAsync(member.TenantId, cancellationToken)).OrderBy(b => b.Name).FirstOrDefault();
            if (branch is null)
            {
                return new RegisterSessionResult.NoRegister();
            }

            var created = new Device
            {
                TenantId = member.TenantId,
                BranchId = branch.Id,
                DeviceType = DeviceType.Register,
                Name = "Admin register",
                Status = DeviceStatus.Active,
                PairedAt = DateTimeOffset.UtcNow,
                PairingCode = string.Empty,
            };
            deviceRepository.Add(created);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            registers.Add(created);
        }

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
        // Only Admins and Managers get a register session, so the attestation always applies.
        return new RegisterSessionResult.Success(accessToken, refreshToken, attestationService.Issue(member.Id, member.TenantId, device.Id, DateTimeOffset.UtcNow));
    }
}
