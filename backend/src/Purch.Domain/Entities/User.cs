using Purch.Domain.Common;
using Purch.Domain.Enums;

namespace Purch.Domain.Entities;

public class User : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;

    public Role Role { get; set; }

    /// <summary>Orthogonal to Role — governs which data this user may see/act on.</summary>
    public ScopeType ScopeType { get; set; }

    public Guid? ScopeId { get; set; }

    public Guid? BranchId { get; set; }

    public string PinHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
