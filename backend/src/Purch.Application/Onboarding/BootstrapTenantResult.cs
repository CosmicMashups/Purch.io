namespace Purch.Application.Onboarding;

public sealed record BootstrapTenantResult(
    Guid TenantId,
    Guid BranchId,
    Guid DeviceId,
    string DevicePairingCode,
    Guid AdminUserId);
