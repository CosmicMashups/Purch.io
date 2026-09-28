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
}
