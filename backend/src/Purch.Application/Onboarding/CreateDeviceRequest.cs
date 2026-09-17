using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

/// <summary>PairingPin is required for every DeviceType except Register.</summary>
public sealed record CreateDeviceRequest(
    Guid BranchId,
    string? DeviceIdentifier,
    DeviceType DeviceType = DeviceType.Register,
    string? PairingPin = null);
