using Purch.Domain.Entities;

namespace Purch.Application.CreditLedger;

public interface ICustomerCreditLedgerRepository
{
    Task<CustomerCreditLedger?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerCreditLedger>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Active ledgers with an unpaid balance whose DueDate falls at or
    /// before the given date (covers both "coming up soon" and "already overdue" —
    /// the caller decides the lookahead date, the service flags which is which).</summary>
    Task<IReadOnlyList<CustomerCreditLedger>> ListDueOnOrBeforeAsync(Guid tenantId, DateOnly onOrBefore, CancellationToken cancellationToken = default);

    void Add(CustomerCreditLedger ledger);

    void AddTransaction(CreditTransaction transaction);
}
