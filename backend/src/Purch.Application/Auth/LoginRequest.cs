namespace Purch.Application.Auth;

/// <summary>
/// Staff log in by PIN on a paired device. The device identifies the tenant;
/// the PIN identifies the staff member within it (checked against every active
/// user's hash, since PINs alone don't name a user — acceptable at small staff counts).
/// </summary>
public sealed record LoginRequest(string DevicePairingCode, string Pin);
