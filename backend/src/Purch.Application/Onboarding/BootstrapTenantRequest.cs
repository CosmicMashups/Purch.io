using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

/// <summary>
/// One-time setup (A1–A3 combined): creates the tenant, its first branch, a
/// paired device, and the initial Admin account all together, since none of
/// those can exist meaningfully without the others. The admin then logs in
/// normally via POST /auth/login using the returned pairing code + their PIN.
/// </summary>
public sealed record BootstrapTenantRequest(
    string TenantName,
    BusinessType BusinessType,
    string BranchName,
    string AdminName,
    string AdminPin,
    /// <summary>Optional — when both are set, the admin can also log in via
    /// POST /auth/admin-login instead of only device pairing code + PIN.</summary>
    string? AdminEmail = null,
    string? AdminPassword = null);
