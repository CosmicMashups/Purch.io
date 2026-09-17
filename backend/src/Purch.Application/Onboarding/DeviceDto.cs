using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed record DeviceDto(
    Guid Id,
    Guid BranchId,
    string PairingCode,
    string? DeviceIdentifier,
    DeviceType DeviceType,
    DateTimeOffset? LastSeenAt);
