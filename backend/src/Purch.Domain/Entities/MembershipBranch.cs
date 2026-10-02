using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>A branch a staff member works at. Admin and Manager memberships reach every branch, so they need no rows.</summary>
public class MembershipBranch : TenantScopedEntity
{
    public Guid MembershipId { get; set; }

    public Guid BranchId { get; set; }
}
