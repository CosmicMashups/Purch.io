namespace Purch.Application.Kiosk;

/// <summary>PairingPin is the device's own pairing PIN (set by an admin in Manage
/// Devices), not a staff PIN — required so a leaked/guessed pairing code alone
/// isn't enough for a stranger to pair a rogue kiosk as this tenant.</summary>
public sealed record KioskSessionRequest(string DevicePairingCode, string PairingPin);

/// <summary>Mirrors Auth.LoginResult's shape — deliberately one failure case for
/// both an unrecognized code and a wrong PIN, so a caller can't tell which part
/// was wrong (same rationale as staff login).</summary>
public abstract record KioskSessionResult
{
    public sealed record Success(string AccessToken, string RefreshToken) : KioskSessionResult;

    public sealed record InvalidDevice : KioskSessionResult;
}
