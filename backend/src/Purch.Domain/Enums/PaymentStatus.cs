namespace Purch.Domain.Enums;

/// <summary>
/// Cash and manual Bank Transfer/Utang confirmations go straight to Confirmed.
/// QR Ph starts Pending until the Xendit webhook lands (async, not instant).
/// </summary>
public enum PaymentStatus
{
    Pending,
    Confirmed,
    Failed,
    Cancelled,
}
