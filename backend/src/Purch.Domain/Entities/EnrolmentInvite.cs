using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

/// <summary>A single-use link (also shown as a QR code) that an Admin or Manager hands to a staff member. No email is
/// sent. Only the hash of the token is stored; the raw value is shown once when the invite is created.</summary>
public class EnrolmentInvite : TenantScopedEntity
{
    public InvitePurpose Purpose { get; set; }

    /// <summary>Set for a password reset (the existing membership); null for a new enrolment until it is redeemed.</summary>
    public Guid? MembershipId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The email the Admin entered. Lower-cased and trimmed.</summary>
    public string Email { get; set; } = string.Empty;

    public MembershipRole Role { get; set; } = MembershipRole.Staff;

    public StaffDuty Duties { get; set; }

    public List<Guid> BranchIds { get; set; } = [];

    /// <summary>SHA-256 of the token. Looked up without a tenant (the person opening the link has no session yet).</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RedeemedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? CreatedByMembershipId { get; set; }
}
