using Purch.Domain.Entities;

namespace Purch.Application.Pos;

public interface IPaymentRepository
{
    Task<IReadOnlyList<Payment>> ListByTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>Total confirmed Cash payments recorded on this device's sales since the given time — D7's "expected cash" basis.</summary>
    Task<decimal> SumCashCollectedByDeviceSinceAsync(Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>Cash paid back out of this device's drawer for sales it refunded since the given time: what those sales
    /// took in as cash.</summary>
    Task<decimal> SumCashRefundedByDeviceSinceAsync(Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken = default);

    void Add(Payment payment);
}
