namespace Purch.Application.Inventory;

public interface IPurchaseOrderService
{
    Task<IReadOnlyList<PurchaseOrderDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>Draft -> Sent.</summary>
    Task<PurchaseOrderDto> MarkSentAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);

    /// <summary>Draft or Sent -> Cancelled.</summary>
    Task<PurchaseOrderDto> CancelAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);

    /// <summary>Sent or PartiallyReceived -> PartiallyReceived or Received, depending on
    /// whether every line is now fully received. Adds the received quantity to
    /// Item.StockOnHand and records a StockIn InventoryMovement per line.</summary>
    Task<PurchaseOrderDto> ReceiveAsync(Guid purchaseOrderId, ReceivePurchaseOrderRequest request, CancellationToken cancellationToken = default);
}
