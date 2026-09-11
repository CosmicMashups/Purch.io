namespace Purch.Domain.Enums;

/// <summary>
/// Orthogonal to <see cref="Role"/>: Role governs what actions a user may take,
/// ScopeType + a scope id govern which tenant/branch/department data they may see or act on.
/// </summary>
public enum ScopeType
{
    Tenant,
    Branch,
    Department,
}
