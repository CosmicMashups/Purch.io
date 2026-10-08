using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

/// <summary>One account's place in one business: the role, the duties they are qualified for, their personal PIN for
/// unlocking a till, and (through <see cref="MembershipBranch"/>) the branches they work at. The same account can have
/// a membership in several businesses.</summary>
public class Membership : TenantScopedEntity, ISoftDeletable
{
    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Guid? DeletedByUserId { get; set; }

    public Guid AccountId { get; set; }

    public Account? Account { get; set; }

    public MembershipRole Role { get; set; } = MembershipRole.Staff;

    /// <summary>Only meaningful for <see cref="MembershipRole.Staff"/>; Admin and Manager can work any duty.</summary>
    public StaffDuty Duties { get; set; }

    /// <summary>The person's own PIN for unlocking a paired till. Always checked against this one membership
    /// (the person is chosen first), so unlike the old PIN login it needs no cross-user uniqueness. Null until set.</summary>
    public string? PinHash { get; set; }

    public int PinFailedAttempts { get; set; }

    /// <summary>Set once PinFailedAttempts reaches the limit; PIN sign-in is refused until it passes.</summary>
    public DateTimeOffset? PinLockedUntil { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>The pre-redesign User this membership replaces, kept until existing staff have re-enrolled so old
    /// sales and audit rows can be tied to the new record.</summary>
    public Guid? LegacyUserId { get; set; }

    public List<MembershipBranch> Branches { get; set; } = [];
}
