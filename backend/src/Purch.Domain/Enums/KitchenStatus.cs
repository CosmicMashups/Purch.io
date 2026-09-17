namespace Purch.Domain.Enums;

/// <summary>Kitchen-prep workflow state for a kiosk-originated order, orthogonal to
/// TransactionStatus (payment). An order can be Ready before it's paid, or paid
/// before it's Ready. Only ever set on OriginatedFromKiosk transactions.</summary>
public enum KitchenStatus
{
    /// <summary>Submitted, kitchen hasn't started yet.</summary>
    Queued,

    /// <summary>Kitchen staff has started preparing it.</summary>
    Preparing,

    /// <summary>Food is ready, waiting for the customer to collect it.</summary>
    Ready,

    /// <summary>Customer has collected it — terminal state, drops off the Order
    /// Board and Kitchen Display's active queues.</summary>
    PickedUp,
}
