namespace Purch.Application.Common;

/// <summary>
/// Supplies the ambient staff user/device/branch for the current request,
/// resolved from the JWT's sub/device_id/branch_id claims. BranchId here is
/// always the authenticated device's own branch (see JwtTokenService), not
/// necessarily the staff member's assigned branch — a transaction happens at
/// one physical terminal, regardless of the cashier's own RBAC scope.
/// </summary>
public interface ICurrentActorProvider
{
    Guid? UserId { get; }

    Guid? DeviceId { get; }

    Guid? BranchId { get; }
}
