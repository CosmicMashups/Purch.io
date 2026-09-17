namespace Purch.Application.Devices;

/// <summary>Shared pairing request for every unattended, PIN-protected device
/// type (OrderBoard, KitchenDisplay). Kiosk keeps its own dedicated
/// KioskSessionRequest/Service since it predates this and its cart endpoints
/// live in their own module; this covers the read-only display terminals.</summary>
public sealed record UnattendedSessionRequest(string DevicePairingCode, string PairingPin);

/// <summary>Deliberately one failure case for an unrecognized code, a code
/// belonging to the wrong device type, and a wrong PIN, mirroring
/// Auth.LoginResult/Kiosk.KioskSessionResult's rationale.</summary>
public abstract record UnattendedSessionResult
{
    public sealed record Success(string AccessToken, string RefreshToken) : UnattendedSessionResult;

    public sealed record InvalidDevice : UnattendedSessionResult;
}
