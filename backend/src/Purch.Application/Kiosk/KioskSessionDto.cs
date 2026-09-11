namespace Purch.Application.Kiosk;

public sealed record KioskSessionRequest(string DevicePairingCode);

/// <summary>Mirrors Auth.LoginResult's shape — a kiosk terminal has no PIN to get
/// wrong, only a pairing code, so there's just the one failure case.</summary>
public abstract record KioskSessionResult
{
    public sealed record Success(string AccessToken) : KioskSessionResult;

    public sealed record InvalidDevice : KioskSessionResult;
}
