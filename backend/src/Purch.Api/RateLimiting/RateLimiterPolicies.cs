namespace Purch.Api.RateLimiting;

/// <summary>Named rate-limiter policies registered in Program.cs — endpoints opt in via
/// .RequireRateLimiting(RateLimiterPolicies.X) rather than repeating limit numbers at
/// every call site.</summary>
public static class RateLimiterPolicies
{
    /// <summary>Login, kiosk pairing, and password-reset endpoints — anonymous,
    /// credential-guessing surfaces that need a tight per-client limit.</summary>
    public const string AuthSensitive = "auth-sensitive";

    /// <summary>Token refresh — anonymous, but every terminal and browser tab renews on a timer,
    /// so a shop with many devices behind one public IP needs far more headroom than
    /// AuthSensitive allows while still capping a token-guessing loop.</summary>
    public const string Refresh = "refresh";

    /// <summary>Shift close — a cash discrepancy is approved by typing a manager's PIN, so an
    /// unthrottled endpoint lets any cashier guess manager PINs. Counted per signed-in user, not
    /// per IP: everyone in a shop shares one address.</summary>
    public const string ShiftApproval = "shift-approval";
}
