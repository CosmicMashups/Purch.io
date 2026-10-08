using Purch.Domain.Common;

namespace Purch.Domain.Entities;

/// <summary>Failed password attempts for one email address, so guessing is slowed per account and not only per IP.
/// Keyed by a hash of the normalized email and kept for emails that have no account too, so being throttled never
/// reveals whether an account exists. Not tenant scoped: sign-in happens before a business is chosen.</summary>
public class SignInThrottle : Entity
{
    /// <summary>SHA-256 of the lower-cased, trimmed email, hex encoded; unique.</summary>
    public string EmailHash { get; set; } = string.Empty;

    public int Failures { get; set; }

    /// <summary>When the current run of failures began; a run older than the policy window starts over.</summary>
    public DateTimeOffset WindowStartedAt { get; set; }

    public DateTimeOffset? BlockedUntil { get; set; }
}
