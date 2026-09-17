namespace Purch.Application.Auth;

/// <summary>Email+password login for a tenant admin/owner — independent of
/// LoginRequest's device pairing code + PIN, used for back-office access.</summary>
public sealed record AdminLoginRequest(string Email, string Password);
