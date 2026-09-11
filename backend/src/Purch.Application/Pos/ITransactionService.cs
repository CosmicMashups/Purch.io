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

    /// <summary>E4's fulfillment choice on the current open cart (e.g. "Dine In"/"Take Out"). Shared by kiosk and cashier POS — either can set it.</summary>
    Task<TransactionDto> SetOrderTypeAsync(SetOrderTypeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Kiosk-only (E6): closes the kiosk device's open cart to further edits, flags it
    /// OriginatedFromKiosk, and issues its KioskPrepNumber — the cart moves Open to
    /// AwaitingPayment, leaving the kiosk free to start building a new customer's order.</summary>
    Task<TransactionDto> SubmitKioskOrderAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists this branch's kiosk orders still awaiting pickup, oldest first.</summary>
    Task<IReadOnlyList<TransactionDto>> ListPendingKioskOrdersAsync(Guid branchId, CancellationToken cancellationToken = default);

    /// <summary>Cashier-POS-only: claims a pending kiosk order onto the calling device, reusing
    /// the exact same payment/discount/promo pipeline as any other cart — reassigns the order
    /// to this device and staff user and reopens it (AwaitingPayment to Open). Rejects if this
    /// device already has its own open cart, or if the order belongs to a different branch.</summary>
    Task<TransactionDto> ClaimKioskOrderAsync(Guid transactionId, CancellationToken cancellationToken = default);
}
