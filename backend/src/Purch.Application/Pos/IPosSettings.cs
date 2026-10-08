namespace Purch.Application.Pos;

/// <summary>Deployment switches for the point of sale.</summary>
public interface IPosSettings
{
    /// <summary>
    /// Whether an online checkout must say what total the customer was shown (<c>ExpectedTotal</c>). On by default: the
    /// server charges its own total, and this check is what proves the customer saw that same amount. It exists as a switch
    /// only so app versions that predate the field can keep working for a short grace period after an upgrade.
    /// </summary>
    bool RequireExpectedTotal { get; }

    /// <summary>
    /// Whether an offline Senior/PWD discount sale may sync without a supervisor attestation, trusting the "rung by" id the till
    /// sent (the old behaviour, which any cashier can forge). Off by default. It exists only so sales already queued on tills
    /// running an app version that predates attestations can still sync for a short grace period after an upgrade; turn it back off
    /// once they have.
    /// </summary>
    bool AcceptUnattestedOfflineDiscounts { get; }
}
