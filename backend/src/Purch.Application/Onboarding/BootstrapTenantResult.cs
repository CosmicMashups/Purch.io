namespace Purch.Application.Onboarding;

/// <summary>AdminMembershipId is the owner's place in the new business. No device is created here: the owner signs in with
/// email and password and adds devices from the Devices page.</summary>
public sealed record BootstrapTenantResult(
    Guid TenantId,
    Guid BranchId,
    Guid AdminMembershipId);
