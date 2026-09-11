using Microsoft.EntityFrameworkCore;
using Purch.Application.Pos;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfPaymentRepository(PurchDbContext dbContext) : IPaymentRepository
{
    public async Task<IReadOnlyList<Payment>> ListByTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.TransactionId == transactionId)
            .ToListAsync(cancellationToken);
    }

    public void Add(Payment payment)
    {
        _ = dbContext.Payments.Add(payment);
    }
}
