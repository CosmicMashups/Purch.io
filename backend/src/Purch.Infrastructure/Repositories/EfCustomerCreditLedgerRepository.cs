using Microsoft.EntityFrameworkCore;
using Purch.Application.CreditLedger;
using Purch.Domain.Entities;
using Purch.Infrastructure.Persistence;

namespace Purch.Infrastructure.Repositories;

public sealed class EfCustomerCreditLedgerRepository(PurchDbContext dbContext) : ICustomerCreditLedgerRepository
{
    public Task<CustomerCreditLedger?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.CustomerCreditLedgers.FirstOrDefaultAsync(ledger => ledger.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerCreditLedger>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomerCreditLedgers
            .AsNoTracking()
            .Where(ledger => ledger.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerCreditLedger>> ListDueOnOrBeforeAsync(Guid tenantId, DateOnly onOrBefore, CancellationToken cancellationToken = default)
    {
        return await dbContext.CustomerCreditLedgers
            .AsNoTracking()
            .Where(ledger =>
                ledger.TenantId == tenantId
                && ledger.IsActive
                && ledger.Balance > 0
                && ledger.DueDate != null
                && ledger.DueDate <= onOrBefore)
            .ToListAsync(cancellationToken);
    }

    public void Add(CustomerCreditLedger ledger)
    {
        _ = dbContext.CustomerCreditLedgers.Add(ledger);
    }

    public void AddTransaction(CreditTransaction transaction)
    {
        _ = dbContext.CreditTransactions.Add(transaction);
    }
}
