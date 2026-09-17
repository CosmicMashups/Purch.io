namespace Purch.Api.RateLimiting;

/// <summary>Named rate-limiter policies registered in Program.cs — endpoints opt in via
/// .RequireRateLimiting(RateLimiterPolicies.X) rather than repeating limit numbers at
/// every call site.</summary>
public static class RateLimiterPolicies
{
    /// <summary>Login, kiosk pairing, and password-reset endpoints — anonymous,
    /// credential-guessing surfaces that need a tight per-client limit.</summary>
    public const string AuthSensitive = "auth-sensitive";
}
