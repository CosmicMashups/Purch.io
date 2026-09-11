using Purch.Domain.Entities;

namespace Purch.Application.Pos;

public interface IPaymentRepository
{
    Task<IReadOnlyList<Payment>> ListByTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default);

    void Add(Payment payment);
}
