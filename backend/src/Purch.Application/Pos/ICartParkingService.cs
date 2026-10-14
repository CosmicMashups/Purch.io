namespace Purch.Application.Pos;

public interface ICartParkingService
{
    /// <summary>Sets the device's open cart aside (Status Parked). 400 if it is empty or a claimed kiosk order.</summary>
    Task ParkAsync(string? label, CancellationToken cancellationToken = default);

    /// <summary>Parked carts of the current branch, newest first.</summary>
    Task<IReadOnlyList<ParkedCartSummaryDto>> ListParkedAsync(CancellationToken cancellationToken = default);

    /// <summary>Makes a parked cart this device's open cart, repricing it and reporting changed lines.</summary>
    Task<TransactionDto> ResumeAsync(Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>Voids a parked cart without payment. A cart with items needs an approver PIN, like VoidCartAsync.</summary>
    Task DiscardAsync(Guid transactionId, string? approverPin, CancellationToken cancellationToken = default);

    /// <summary>Voids every parked cart older than its tenant's ParkedCartTtlHours. Cross-tenant; needs no
    /// request context (called from the retention sweep). Returns how many carts were expired.</summary>
    Task<int> ExpireStaleAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
