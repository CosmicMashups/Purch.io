using Purch.Domain.Entities;

namespace Purch.Application.Pos;

public interface IPaymentRepository
{
    Task<IReadOnlyList<Payment>> ListByTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>Total confirmed Cash payments recorded on this device's sales since the given time — D7's "expected cash" basis.</summary>
    Task<decimal> SumCashCollectedByDeviceSinceAsync(Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken = default);

    void Add(Payment payment);
}
