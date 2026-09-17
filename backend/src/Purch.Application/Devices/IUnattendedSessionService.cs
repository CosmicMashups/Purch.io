using Purch.Domain.Enums;

namespace Purch.Application.Devices;

public interface IUnattendedSessionService
{
    /// <summary>Pairs an unattended display device (Order Board, Kitchen Display) by
    /// its pairing code plus the device's own pairing PIN. Rejects if the code
    /// doesn't exist, belongs to a device of a different DeviceType than
    /// expected, or the PIN doesn't match.</summary>
    Task<UnattendedSessionResult> PairAsync(
        UnattendedSessionRequest request,
        DeviceType expectedDeviceType,
        Role role,
        CancellationToken cancellationToken = default);
}
