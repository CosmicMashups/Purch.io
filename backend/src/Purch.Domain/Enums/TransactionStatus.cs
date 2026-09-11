namespace Purch.Domain.Enums;

public enum TransactionStatus
{
    Open,
    AwaitingPayment,
    Completed,
    Voided,
    Refunded,
}
