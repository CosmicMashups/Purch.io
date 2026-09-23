namespace Purch.Application.CreditLedger;

public sealed record UpdateCreditLimitRequest(decimal CreditLimit, string? Reason = null);
