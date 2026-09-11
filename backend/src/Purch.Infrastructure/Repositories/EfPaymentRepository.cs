using Microsoft.EntityFrameworkCore;
using Purch.Application.Pos;
using Purch.Domain.Entities;
using Purch.Domain.Enums;
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

    public async Task<decimal> SumCashCollectedByDeviceSinceAsync(Guid deviceId, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var query =
            from payment in dbContext.Payments.AsNoTracking()
            join transaction in dbContext.Transactions.AsNoTracking() on payment.TransactionId equals transaction.Id
            where transaction.DeviceId == deviceId
                && payment.Method == PaymentMethod.Cash
                && payment.Status == PaymentStatus.Confirmed
                && payment.CreatedAt >= since
            select payment.Amount;

        return await query.SumAsync(cancellationToken);
    }

    public void Add(Payment payment)
    {
        _ = dbContext.Payments.Add(payment);
    }
}
