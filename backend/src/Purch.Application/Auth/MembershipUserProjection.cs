using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Auth;

/// <summary>Much of the till (shifts, approvals, discounts, reports) still looks a person up as a <see cref="User"/> by the
/// id in their token. A membership is shown to that code as a User built on the fly: same id, name, role and PIN. These
/// objects are never added to the database context, so nothing written to them is saved.</summary>
public static class MembershipUserProjection
{
    public static User ToUser(Membership membership)
    {
        var (scope, scopeId) = MembershipRoleMapper.ToScope(membership);
        return new User
        {
            Id = membership.Id,
            TenantId = membership.TenantId,
            Name = membership.Account?.DisplayName ?? string.Empty,
            Role = MembershipRoleMapper.ToApiRole(membership) ?? Role.Cashier,
            ScopeType = scope,
            ScopeId = scopeId,
            PinHash = membership.PinHash ?? string.Empty,
            Email = membership.Account?.Email,
            IsActive = membership.IsActive,
            CreatedAt = membership.CreatedAt,
        };
    }
}
