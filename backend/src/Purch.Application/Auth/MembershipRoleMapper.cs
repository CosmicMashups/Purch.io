using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Auth;

/// <summary>Turns a membership into the single API role the existing authorization checks understand. Admin and Manager map
/// directly. A staff member gets Warehouse if qualified for it, otherwise Cashier; a person with neither duty has no API
/// role and cannot sign in. This is the stopgap for a personal-device session: once sessions are tied to a device
/// (see docs/AUTH-REDESIGN.md) the device's own duty decides.</summary>
public static class MembershipRoleMapper
{
    public static Role? ToApiRole(Membership membership)
    {
        return membership.Role switch
        {
            MembershipRole.Admin => Role.Admin,
            MembershipRole.Manager => Role.Manager,
            _ when membership.Duties.HasFlag(StaffDuty.Warehouse) => Role.Warehouse,
            _ when membership.Duties.HasFlag(StaffDuty.Cashier) => Role.Cashier,
            _ => null,
        };
    }

    /// <summary>Admin and Manager reach every branch; a staff member with exactly one branch is limited to it. A staff member
    /// with several branches is given tenant scope for now, because a token carries only one scope id.</summary>
    public static (ScopeType Scope, Guid? ScopeId) ToScope(Membership membership)
    {
        if (membership.Role == MembershipRole.Staff && membership.Branches.Count == 1)
        {
            return (ScopeType.Branch, membership.Branches[0].BranchId);
        }

        return (ScopeType.Tenant, null);
    }
}
