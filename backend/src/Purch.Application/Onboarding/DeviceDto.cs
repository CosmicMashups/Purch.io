namespace Purch.Application.Onboarding;

public sealed record DeviceDto(
    Guid Id,
    Guid BranchId,
    string PairingCode,
    string? DeviceIdentifier,
    DateTimeOffset? LastSeenAt);
