namespace Purch.Application.Kiosk;

public interface IKioskSessionService
{
    /// <summary>Pairs a kiosk terminal by its device pairing code alone — no staff PIN,
    /// since a kiosk is customer-facing and no one is "logging into" it.</summary>
    Task<KioskSessionResult> PairAsync(KioskSessionRequest request, CancellationToken cancellationToken = default);
}
