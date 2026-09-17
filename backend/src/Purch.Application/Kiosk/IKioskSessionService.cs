namespace Purch.Application.Kiosk;

public interface IKioskSessionService
{
    /// <summary>Pairs a kiosk terminal by its device pairing code plus the device's
    /// own pairing PIN (set by an admin, not a staff PIN — a kiosk is customer-facing
    /// and no one is "logging into" it) so a leaked pairing code alone can't be used
    /// to pair a rogue kiosk.</summary>
    Task<KioskSessionResult> PairAsync(KioskSessionRequest request, CancellationToken cancellationToken = default);
}
