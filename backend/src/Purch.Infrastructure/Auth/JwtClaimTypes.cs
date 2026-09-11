namespace Purch.Infrastructure.Auth;

/// <summary>Custom claim names embedded in the JWT — read by TenantResolutionMiddleware and RBAC filters.</summary>
public static class JwtClaimTypes
{
    public const string TenantId = "tenant_id";
    public const string Role = "role";
    public const string ScopeType = "scope_type";
    public const string ScopeId = "scope_id";
    public const string BranchId = "branch_id";
}
