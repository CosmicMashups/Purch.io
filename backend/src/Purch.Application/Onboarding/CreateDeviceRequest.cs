namespace Purch.Application.Onboarding;

public sealed record CreateDeviceRequest(Guid BranchId, string? DeviceIdentifier);
