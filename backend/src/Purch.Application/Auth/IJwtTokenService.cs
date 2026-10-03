using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Auth;

public interface IJwtTokenService
{
    /// <summary>A kiosk terminal pairs without a staff PIN — the token carries only
    /// tenant/device/branch claims under Role.Kiosk, no sub/user claim at all.</summary>
    string IssueKioskAccessToken(Device device);

    /// <summary>Same shape as IssueKioskAccessToken (no sub/user claim, tenant/device/
    /// branch only) generalized to any unattended device role (OrderBoard,
    /// KitchenDisplay) — these are read-only display terminals, never a cart.</summary>
    string IssueUnattendedAccessToken(Device device, Role role);

    /// <summary>A person signing in with email and password on a personal device: no device or branch claim, so it
    /// cannot sell. The role is the one <see cref="MembershipRoleMapper"/> derives from the membership.</summary>
    string IssueMembershipAccessToken(Membership membership);

    /// <summary>The same person unlocking a paired till or warehouse device: the device's id, branch and session version
    /// are carried (so revoking the device ends this session) and the device's own duty decides a staff member's role.</summary>
    string IssueMembershipAccessToken(Membership membership, Device device);
}
