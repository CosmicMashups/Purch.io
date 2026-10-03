using Purch.Application.Auth;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed record CreateInviteRequest(string Name, string Email, MembershipRole Role, StaffDuty Duties, IReadOnlyList<Guid>? BranchIds, Guid? LegacyUserId = null);

/// <summary>Someone from the old sign-in who has not been invited to the new one yet. The role and duty are suggestions taken
/// from what they were; the Admin confirms them, and gives the email the old system never had.</summary>
public sealed record LegacyStaffDto(Guid Id, string Name, MembershipRole SuggestedRole, StaffDuty SuggestedDuties, Guid? BranchId);

public sealed record InviteDto(Guid Id, InvitePurpose Purpose, string Name, string Email, MembershipRole Role, StaffDuty Duties, IReadOnlyList<Guid> BranchIds, DateTimeOffset ExpiresAt);

/// <summary>The raw token is shown once, as a link or QR code; the server keeps only its hash.</summary>
public sealed record InviteLinkDto(InviteDto Invite, string Token);

public sealed record MemberDto(Guid Id, string Name, string Email, MembershipRole Role, StaffDuty Duties, IReadOnlyList<Guid> BranchIds, bool IsActive, bool HasPin);

public sealed record UpdateMemberRequest(MembershipRole Role, StaffDuty Duties, IReadOnlyList<Guid>? BranchIds, bool IsActive);

/// <summary>What the person sees when they open the link, before choosing a password.</summary>
public sealed record InvitePreviewDto(string BusinessName, string Name, string Email, InvitePurpose Purpose, MembershipRole Role, StaffDuty Duties, bool HasAccount);

/// <summary>Password is a new one for a new account or a reset; for a person who already has an account (another business)
/// it is their existing password, which proves the account is theirs. Pin is required for an enrolment.</summary>
public sealed record RedeemInviteRequest(string Token, string Password, string? Pin, string? Email = null);

public abstract record RedeemInviteResult
{
    public sealed record Success(string AccessToken, string RefreshToken) : RedeemInviteResult;

    /// <summary>Unknown, already used, revoked or expired. One case on purpose.</summary>
    public sealed record InvalidLink : RedeemInviteResult;
}

public interface IMembershipRepository
{
    /// <summary>The current business's people, with their account and branches loaded.</summary>
    Task<IReadOnlyList<Membership>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Tracked, inside the current business only (the tenant filter applies).</summary>
    Task<Membership?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsForEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Not tenant-filtered: used while redeeming a link, when nobody is signed in.</summary>
    Task<Membership?> GetUnscopedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Every person of a business with their account and branches, without needing a signed-in tenant: a till asks
    /// for its roster using only its own device credential.</summary>
    Task<IReadOnlyList<Membership>> ListByTenantUnscopedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public interface IEnrolmentInviteRepository
{
    void Add(EnrolmentInvite invite);

    /// <summary>The still-usable invites of the current business.</summary>
    Task<IReadOnlyList<EnrolmentInvite>> ListPendingAsync(CancellationToken cancellationToken = default);

    Task<EnrolmentInvite?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked and not tenant-filtered: the person opening the link has no session, so the secret is the credential.</summary>
    Task<EnrolmentInvite?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<string?> GetBusinessNameAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public interface IStaffEnrolmentService
{
    /// <summary>Admin or Manager: invites a person. A Manager can only invite staff, never another Manager or Admin.</summary>
    Task<InviteLinkDto> CreateInviteAsync(CreateInviteRequest request, bool actorIsAdmin, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InviteDto>> ListInvitesAsync(CancellationToken cancellationToken = default);

    /// <summary>People from the old sign-in still to be invited, so none is forgotten when it is retired.</summary>
    Task<IReadOnlyList<LegacyStaffDto>> ListLegacyAsync(CancellationToken cancellationToken = default);

    Task RevokeInviteAsync(Guid inviteId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemberDto>> ListMembersAsync(CancellationToken cancellationToken = default);

    Task<MemberDto> UpdateMemberAsync(Guid memberId, UpdateMemberRequest request, bool actorIsAdmin, CancellationToken cancellationToken = default);

    /// <summary>A single-use link to set a new password, for someone who forgot theirs and cannot get an email.</summary>
    Task<InviteLinkDto> CreateResetLinkAsync(Guid memberId, bool actorIsAdmin, CancellationToken cancellationToken = default);

    /// <summary>Anonymous: what a link is for, or null when it is not usable.</summary>
    Task<InvitePreviewDto?> PreviewAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Anonymous: sets up the account (or the new password) and signs the person in.</summary>
    Task<RedeemInviteResult> RedeemAsync(RedeemInviteRequest request, CancellationToken cancellationToken = default);
}
