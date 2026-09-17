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

    /// <summary>Set only for admin/owner accounts that use the separate
    /// email+password login (back-office use) instead of device+PIN.
    /// Null for ordinary staff, who only ever log in via PIN.</summary>
    public string? Email { get; set; }

    /// <summary>Paired with <see cref="Email"/> — null unless email login is enabled for this user.</summary>
    public string? PasswordHash { get; set; }

    public bool IsActive { get; set; } = true;
}
