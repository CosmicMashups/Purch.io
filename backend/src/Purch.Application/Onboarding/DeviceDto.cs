using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed record DeviceDto(
    Guid Id,
    Guid BranchId,
    string? DeviceIdentifier,
    DeviceType DeviceType,
    DateTimeOffset? LastSeenAt,
    string? Name = null,
    DeviceStatus Status = DeviceStatus.Active,
    DateTimeOffset? PairedAt = null,
    DateTimeOffset? PairingCodeExpiresAt = null,
    Guid? LinkedRegisterDeviceId = null);
