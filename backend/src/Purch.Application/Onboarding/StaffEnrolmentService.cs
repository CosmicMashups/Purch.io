using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Purch.Application.Auth;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed class StaffEnrolmentService(
    IMembershipRepository membershipRepository,
    IEnrolmentInviteRepository inviteRepository,
    IAccountRepository accountRepository,
    IAccountService accountService,
    IIdentityProvider identityProvider,
    IBranchRepository branchRepository,
    IPinHasher pinHasher,
    IRefreshTokenService refreshTokenService,
    IAuditLogRepository auditLogRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IUnitOfWork unitOfWork) : IStaffEnrolmentService
{
    /// <summary>Long enough for the person to be handed the link and open it, short enough that a forgotten one does not linger.</summary>
    public static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(3);

    public async Task<InviteLinkDto> CreateInviteAsync(CreateInviteRequest request, bool actorIsAdmin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException(nameof(request.Name), "Name is required.");
        }

        var email = AccountService.NormalizeEmail(request.Email ?? string.Empty);
        if (!email.Contains('@') || email.Length < 3)
        {
            throw new ValidationException(nameof(request.Email), "Enter a valid email address.");
        }

        RequireMayManage(request.Role, actorIsAdmin);
        var branchIds = await ValidateAccessAsync(request.Role, request.Duties, request.BranchIds, cancellationToken);

        if (await membershipRepository.ExistsForEmailAsync(email, cancellationToken))
        {
            throw new ConflictException("That email already belongs to someone in this business.");
        }

        var invite = new EnrolmentInvite
        {
            TenantId = CurrentTenantId,
            Purpose = InvitePurpose.Enrolment,
            Name = request.Name.Trim(),
            Email = email,
            Role = request.Role,
            Duties = request.Role == MembershipRole.Staff ? request.Duties : StaffDuty.None,
            BranchIds = branchIds,
            CreatedByMembershipId = await CurrentMembershipIdAsync(cancellationToken),
        };

        var token = StageToken(invite);
        inviteRepository.Add(invite);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return new InviteLinkDto(ToDto(invite), token);
    }

    public async Task<IReadOnlyList<InviteDto>> ListInvitesAsync(CancellationToken cancellationToken = default)
    {
        return [.. (await inviteRepository.ListPendingAsync(cancellationToken)).Select(ToDto)];
    }

    public async Task RevokeInviteAsync(Guid inviteId, CancellationToken cancellationToken = default)
    {
        var invite = await inviteRepository.GetByIdAsync(inviteId, cancellationToken)
            ?? throw new NotFoundException("Invitation", inviteId);

        invite.RevokedAt ??= DateTimeOffset.UtcNow;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(CancellationToken cancellationToken = default)
    {
        return [.. (await membershipRepository.ListAsync(cancellationToken)).Select(ToDto)];
    }

    public async Task<MemberDto> UpdateMemberAsync(Guid memberId, UpdateMemberRequest request, bool actorIsAdmin, CancellationToken cancellationToken = default)
    {
        var member = await membershipRepository.GetByIdAsync(memberId, cancellationToken)
            ?? throw new NotFoundException("Person", memberId);

        RequireMayManage(member.Role, actorIsAdmin);
        RequireMayManage(request.Role, actorIsAdmin);
        var branchIds = await ValidateAccessAsync(request.Role, request.Duties, request.BranchIds, cancellationToken);
        var duties = request.Role == MembershipRole.Staff ? request.Duties : StaffDuty.None;

        // The business must always keep someone who can administer it.
        var losesAdmin = member.Role == MembershipRole.Admin && member.IsActive && (request.Role != MembershipRole.Admin || !request.IsActive);
        if (losesAdmin && await membershipRepository.CountActiveAdminsAsync(cancellationToken) <= 1)
        {
            throw new ValidationException(nameof(request.Role), "The business needs at least one active Admin.");
        }

        var currentBranches = member.Branches.Select(b => b.BranchId).Order().ToList();
        var authorityChanged = member.Role != request.Role
            || member.Duties != duties
            || member.IsActive != request.IsActive
            || !currentBranches.SequenceEqual(branchIds.Order());

        if (authorityChanged)
        {
            auditLogRepository.Add(new AuditLog
            {
                TenantId = CurrentTenantId,
                ActorUserId = currentActorProvider.UserId ?? Guid.Empty,
                ActionType = AuditActionType.StaffAccessChanged,
                TargetEntityType = nameof(Membership),
                TargetEntityId = member.Id,
                BeforeStateJson = JsonSerializer.Serialize(new { role = member.Role.ToString(), duties = member.Duties.ToString(), branchIds = currentBranches, isActive = member.IsActive }),
                AfterStateJson = JsonSerializer.Serialize(new { role = request.Role.ToString(), duties = duties.ToString(), branchIds, isActive = request.IsActive }),
            });
        }

        member.Role = request.Role;
        member.Duties = duties;
        member.IsActive = request.IsActive;
        member.Branches.RemoveAll(b => !branchIds.Contains(b.BranchId));
        foreach (var branchId in branchIds.Where(id => member.Branches.All(b => b.BranchId != id)))
        {
            member.Branches.Add(new MembershipBranch { TenantId = member.TenantId, MembershipId = member.Id, BranchId = branchId });
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        // A token carries the role it was issued with, so ending the refresh tokens limits an old authority to the
        // access token's remaining half hour instead of the refresh token's thirty days.
        if (authorityChanged)
        {
            await refreshTokenService.RevokeAllForMembershipAsync(member.Id, cancellationToken);
        }

        return ToDto(member);
    }

    public async Task<InviteLinkDto> CreateResetLinkAsync(Guid memberId, bool actorIsAdmin, CancellationToken cancellationToken = default)
    {
        var member = await membershipRepository.GetByIdAsync(memberId, cancellationToken)
            ?? throw new NotFoundException("Person", memberId);
        RequireMayManage(member.Role, actorIsAdmin);

        var invite = new EnrolmentInvite
        {
            TenantId = CurrentTenantId,
            Purpose = InvitePurpose.PasswordReset,
            MembershipId = member.Id,
            Name = member.Account?.DisplayName ?? string.Empty,
            Email = member.Account?.Email ?? string.Empty,
            Role = member.Role,
            Duties = member.Duties,
            BranchIds = [.. member.Branches.Select(b => b.BranchId)],
            CreatedByMembershipId = await CurrentMembershipIdAsync(cancellationToken),
        };

        var token = StageToken(invite);
        inviteRepository.Add(invite);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return new InviteLinkDto(ToDto(invite), token);
    }

    public async Task<InvitePreviewDto?> PreviewAsync(string token, CancellationToken cancellationToken = default)
    {
        var invite = await FindUsableAsync(token, cancellationToken);
        if (invite is null)
        {
            return null;
        }

        var business = await inviteRepository.GetBusinessNameAsync(invite.TenantId, cancellationToken) ?? string.Empty;
        var hasAccount = await accountRepository.FindByEmailAsync(invite.Email, cancellationToken) is not null;
        return new InvitePreviewDto(business, invite.Name, invite.Email, invite.Purpose, invite.Role, invite.Duties, hasAccount);
    }

    public async Task<RedeemInviteResult> RedeemAsync(RedeemInviteRequest request, CancellationToken cancellationToken = default)
    {
        var invite = await FindUsableAsync(request.Token, cancellationToken);
        if (invite is null)
        {
            return new RedeemInviteResult.InvalidLink();
        }

        var tenantId = invite.TenantId;
        var email = invite.Email;

        if (invite.Purpose == InvitePurpose.PasswordReset)
        {
            await ResetPasswordAsync(invite, request, cancellationToken);
        }
        else
        {
            await EnrolAsync(invite, request, cancellationToken);
        }

        var signedIn = await accountService.SignInAsync(new SignInRequest(email, request.Password, tenantId), cancellationToken);
        return signedIn is SignInResult.Success success
            ? new RedeemInviteResult.Success(success.AccessToken, success.RefreshToken)
            : new RedeemInviteResult.InvalidLink();
    }

    private async Task EnrolAsync(EnrolmentInvite invite, RedeemInviteRequest request, CancellationToken cancellationToken)
    {
        if (PinPolicy.Validate(request.Pin) is { } pinError)
        {
            throw new ValidationException(nameof(request.Pin), pinError);
        }

        // A person who already has an account (another business) proves it with their existing password, so only a new
        // account is held to the password rules.
        var existing = await accountRepository.FindByEmailAsync(invite.Email, cancellationToken);
        if (existing is null && PasswordPolicy.Validate(request.Password) is { } passwordError)
        {
            throw new ValidationException(nameof(request.Password), passwordError);
        }

        var account = await accountService.CreateAccountAsync(invite.Email, invite.Name, request.Password, cancellationToken);

        if (await membershipRepository.ExistsAsync(invite.TenantId, account.Id, cancellationToken))
        {
            throw new ConflictException("This person is already part of the business.");
        }

        var membership = new Membership
        {
            TenantId = invite.TenantId,
            AccountId = account.Id,
            Role = invite.Role,
            Duties = invite.Duties,
            PinHash = pinHasher.Hash(request.Pin!),
        };
        membership.Branches.AddRange(invite.BranchIds.Select(branchId => new MembershipBranch { TenantId = invite.TenantId, MembershipId = membership.Id, BranchId = branchId }));
        accountRepository.Add(membership);

        invite.RedeemedAt = DateTimeOffset.UtcNow;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ResetPasswordAsync(EnrolmentInvite invite, RedeemInviteRequest request, CancellationToken cancellationToken)
    {
        if (PasswordPolicy.Validate(request.Password) is { } passwordError)
        {
            throw new ValidationException(nameof(request.Password), passwordError);
        }

        var member = invite.MembershipId is { } id ? await membershipRepository.GetUnscopedAsync(id, cancellationToken) : null;
        var account = member is null ? null : await accountRepository.FindByEmailAsync(invite.Email, cancellationToken);
        if (member is null || account is null)
        {
            throw new NotFoundException("Person", invite.MembershipId ?? Guid.Empty);
        }

        if (!string.IsNullOrWhiteSpace(request.Pin))
        {
            if (PinPolicy.Validate(request.Pin) is { } pinError)
            {
                throw new ValidationException(nameof(request.Pin), pinError);
            }

            member.PinHash = pinHasher.Hash(request.Pin);
            member.PinFailedAttempts = 0;
            member.PinLockedUntil = null;
        }

        invite.RedeemedAt = DateTimeOffset.UtcNow;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        await identityProvider.SetPasswordAsync(account.SupabaseUserId, request.Password, cancellationToken);

        // Any session opened with the old password stops renewing.
        foreach (var (other, _) in await accountRepository.ListActiveMembershipsAsync(account.Id, cancellationToken))
        {
            await refreshTokenService.RevokeAllForMembershipAsync(other.Id, cancellationToken);
        }
    }

    private async Task<EnrolmentInvite?> FindUsableAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var invite = await inviteRepository.FindByTokenHashAsync(Hash(token.Trim()), cancellationToken);
        return invite is null || invite.RedeemedAt is not null || invite.RevokedAt is not null || invite.ExpiresAt <= DateTimeOffset.UtcNow
            ? null
            : invite;
    }

    /// <summary>Admin and Manager are only for an Admin to hand out; a Manager invites and manages staff.</summary>
    private static void RequireMayManage(MembershipRole role, bool actorIsAdmin)
    {
        if (role != MembershipRole.Staff && !actorIsAdmin)
        {
            throw new ForbiddenException("Only an Admin can invite or change an Admin or Manager.");
        }
    }

    /// <summary>A staff member needs at least one duty and one branch; every branch has to be one of this business's.</summary>
    private async Task<List<Guid>> ValidateAccessAsync(MembershipRole role, StaffDuty duties, IReadOnlyList<Guid>? requestedBranchIds, CancellationToken cancellationToken)
    {
        if (role != MembershipRole.Staff)
        {
            return [];
        }

        if (duties == StaffDuty.None)
        {
            throw new ValidationException(nameof(CreateInviteRequest.Duties), "Choose at least one duty for this staff member.");
        }

        var branchIds = (requestedBranchIds ?? []).Distinct().ToList();
        if (branchIds.Count == 0)
        {
            throw new ValidationException(nameof(CreateInviteRequest.BranchIds), "Choose at least one branch.");
        }

        foreach (var branchId in branchIds)
        {
            _ = await branchRepository.GetByIdAsync(branchId, cancellationToken)
                ?? throw new NotFoundException("Branch", branchId);
        }

        return branchIds;
    }

    private async Task<Guid?> CurrentMembershipIdAsync(CancellationToken cancellationToken)
    {
        // A session from the older PIN login carries a User id, not a membership id; only a real membership is recorded.
        return currentActorProvider.UserId is { } id && await membershipRepository.GetByIdAsync(id, cancellationToken) is not null ? id : null;
    }

    private static string StageToken(EnrolmentInvite invite)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        invite.TokenHash = Hash(token);
        invite.ExpiresAt = DateTimeOffset.UtcNow.Add(InviteLifetime);
        return token;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Staff management requires an authenticated tenant context.");

    private static InviteDto ToDto(EnrolmentInvite invite)
    {
        return new InviteDto(invite.Id, invite.Purpose, invite.Name, invite.Email, invite.Role, invite.Duties, invite.BranchIds, invite.ExpiresAt);
    }

    private static MemberDto ToDto(Membership member)
    {
        return new MemberDto(
            member.Id,
            member.Account?.DisplayName ?? string.Empty,
            member.Account?.Email ?? string.Empty,
            member.Role,
            member.Duties,
            [.. member.Branches.Select(b => b.BranchId)],
            member.IsActive,
            member.PinHash is not null);
    }
}
