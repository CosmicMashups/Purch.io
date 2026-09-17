using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Auth;

public interface IJwtTokenService
{
    /// <summary>device is the physical terminal being logged into — its BranchId is the ground truth for where a transaction happens, since a tenant-scoped staff member's own User.BranchId can be null.</summary>
    string IssueAccessToken(User user, Device device);

    /// <summary>A kiosk terminal pairs without a staff PIN — the token carries only
    /// tenant/device/branch claims under Role.Kiosk, no sub/user claim at all.</summary>
    string IssueKioskAccessToken(Device device);

    /// <summary>An admin/owner logging in via email+password from the back office,
    /// not any particular physical terminal — no device/branch claims to carry.</summary>
    string IssueAdminAccessToken(User user);

    /// <summary>Same shape as IssueKioskAccessToken (no sub/user claim, tenant/device/
    /// branch only) generalized to any unattended device role (OrderBoard,
    /// KitchenDisplay) — these are read-only display terminals, never a cart.</summary>
    string IssueUnattendedAccessToken(Device device, Role role);
}
