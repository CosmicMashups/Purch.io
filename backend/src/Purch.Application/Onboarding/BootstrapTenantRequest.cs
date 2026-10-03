using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

/// <summary>
/// One-time setup: creates the tenant, its first branch, and the owner's account with an Admin membership. The owner then
/// signs in with POST /auth/sign-in using the email and password given here. AdminPin is the owner's personal PIN for
/// unlocking a till. Email and password are required.
/// </summary>
public sealed record BootstrapTenantRequest(
    string TenantName,
    BusinessType BusinessType,
    string BranchName,
    string AdminName,
    string AdminPin,
    string? AdminEmail = null,
    string? AdminPassword = null);
