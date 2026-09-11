namespace Purch.Application.Pos;

public interface ITransactionService
{
    /// <summary>Returns the current device's open cart, creating one if none exists yet.</summary>
    Task<TransactionDto> GetOrCreateOpenCartAsync(CancellationToken cancellationToken = default);

    Task<TransactionDto> AddLineAsync(AddTransactionLineRequest request, CancellationToken cancellationToken = default);

    Task<TransactionDto> UpdateLineAsync(Guid lineId, UpdateTransactionLineRequest request, CancellationToken cancellationToken = default);

    Task<TransactionDto> RemoveLineAsync(Guid lineId, CancellationToken cancellationToken = default);

    Task<TransactionDto> VoidCartAsync(CancellationToken cancellationToken = default);

    /// <summary>Toggles the Senior Citizen/PWD 20% discount on the current open cart — a cashier-facing toggle, applied only after the cashier has verified the customer's physical ID.</summary>
    Task<TransactionDto> ApplySeniorPwdDiscountAsync(ApplySeniorPwdDiscountRequest request, CancellationToken cancellationToken = default);

    /// <summary>Applies (or clears, with a null/blank Code) a cart-level promo code on the current open cart.</summary>
    Task<TransactionDto> ApplyPromoCodeAsync(ApplyPromoCodeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Records a full payment against the current open cart. On success the cart is marked Completed and issued its sequential BIR receipt number.</summary>
    Task<TransactionDto> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default);
}
