namespace Purch.Application.CreditLedger;

public sealed record CustomerCreditLedgerDto(
    Guid Id,
    string CustomerFullName,
    string CustomerPhoneNumber,
    string? CustomerAddress,
    decimal Balance,
    decimal CreditLimit,
    DateOnly? DueDate,
    bool IsActive);

/// <summary>Minimum PII by design (B7) — full name + phone required, address
/// optional. See CustomerCreditLedger's own doc comment / docs/adr/0006.</summary>
public sealed record CreateCustomerCreditLedgerRequest(
    string CustomerFullName,
    string CustomerPhoneNumber,
    string? CustomerAddress,
    decimal CreditLimit,
    DateOnly? DueDate);

/// <summary>A repayment against the customer's balance — always reduces it;
/// use the POS's own Utang/Credit payment method to add a charge instead.</summary>
public sealed record RecordCreditPaymentRequest(decimal Amount, string? Note);

/// <summary>A ledger with an unpaid balance whose due date is at or before the
/// requested lookahead window — the backend's half of B7's "due-date
/// reminders": no SMS/email vendor is chosen yet (same "needs its own
/// research spike" boundary as Phase 4's bill-payment provider), so this is
/// surfaced as a queryable list for the app to display, not a push notification.</summary>
public sealed record CreditReminderDto(
    Guid Id,
    string CustomerFullName,
    string CustomerPhoneNumber,
    decimal Balance,
    DateOnly DueDate,
    bool IsOverdue);
