namespace Purch.Application.Inventory;

public interface IPurchaseOrderService
{
    Task<IReadOnlyList<PurchaseOrderDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>Draft -> Sent.</summary>
    Task<PurchaseOrderDto> MarkSentAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);

    /// <summary>Draft or Sent -> Cancelled.</summary>
    Task<PurchaseOrderDto> CancelAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
}
