namespace Purch.Domain.Enums;

/// <summary>
/// Schema-only hook, unread by any gate in v1 — see docs/adr on licensing removal.
/// Reserved for a possible future subscription-billed Cloud tier.
/// </summary>
public enum LicenseStatus
{
    Trial,
    Active,
    Suspended,
}
