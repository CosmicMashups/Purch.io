using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Auth;

/// <summary>Turns a membership into the single API role the existing authorization checks understand.</summary>
public static class MembershipRoleMapper
{
    /// <summary>On a personal device (no device type): Admin and Manager map directly. A staff member gets Warehouse if
    /// qualified for it, otherwise Cashier; a person with neither duty has no API role and cannot sign in.</summary>
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

    /// <summary>On a paired device the device's own duty decides what a staff member is: a Register makes them a Cashier, a
    /// Warehouse device a Warehouse officer. Admin and Manager keep their roles wherever they sign in. Null when the
    /// person may not work on that kind of device at all.</summary>
    public static Role? ToApiRole(Membership membership, DeviceType deviceType)
    {
        if (membership.Role == MembershipRole.Admin)
        {
            return Role.Admin;
        }

        if (membership.Role == MembershipRole.Manager)
        {
            return Role.Manager;
        }

        return deviceType switch
        {
            DeviceType.Register when membership.Duties.HasFlag(StaffDuty.Cashier) => Role.Cashier,
            DeviceType.WarehouseOfficer when membership.Duties.HasFlag(StaffDuty.Warehouse) => Role.Warehouse,
            _ => null,
        };
    }

    /// <summary>Whether this person may sign in on this device: active, qualified for its duty (Admin and Manager always are),
    /// and, for staff, assigned to the device's branch. Only a Register or Warehouse device takes a person at all.</summary>
    public static bool CanWorkOn(Membership membership, Device device)
    {
        if (!membership.IsActive || device.Status != DeviceStatus.Active || device.DeviceType is not (DeviceType.Register or DeviceType.WarehouseOfficer))
        {
            return false;
        }

        if (membership.Role != MembershipRole.Staff)
        {
            return true;
        }

        return ToApiRole(membership, device.DeviceType) is not null && membership.Branches.Any(b => b.BranchId == device.BranchId);
    }

    /// <summary>Admin and Manager reach every branch; a staff member with exactly one branch is limited to it. A staff member
    /// with several branches is given tenant scope on a personal device, because a token carries only one scope id; on a
    /// device the device's own branch is used instead (see <see cref="ToScope(Membership, Device)"/>).</summary>
    public static (ScopeType Scope, Guid? ScopeId) ToScope(Membership membership)
    {
        if (membership.Role == MembershipRole.Staff && membership.Branches.Count == 1)
        {
            return (ScopeType.Branch, membership.Branches[0].BranchId);
        }

        return (ScopeType.Tenant, null);
    }

    public static (ScopeType Scope, Guid? ScopeId) ToScope(Membership membership, Device device)
    {
        return membership.Role == MembershipRole.Staff ? (ScopeType.Branch, device.BranchId) : (ScopeType.Tenant, null);
    }
}
