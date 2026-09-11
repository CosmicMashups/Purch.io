namespace Purch.Application.CreditLedger;

public interface ICustomerCreditLedgerService
{
    Task<CustomerCreditLedgerDto> CreateAsync(CreateCustomerCreditLedgerRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerCreditLedgerDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Records a repayment, reducing the customer's balance. Charges
    /// (increasing the balance) only ever happen through the POS's Utang/Credit
    /// payment method at checkout — see TransactionService.RecordPaymentAsync.</summary>
    Task<CustomerCreditLedgerDto> RecordPaymentAsync(Guid ledgerId, RecordCreditPaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>B7's due-date reminders — ledgers with a balance due within
    /// (or already past) the given lookahead window.</summary>
    Task<IReadOnlyList<CreditReminderDto>> ListRemindersAsync(int withinDays, CancellationToken cancellationToken = default);
}
