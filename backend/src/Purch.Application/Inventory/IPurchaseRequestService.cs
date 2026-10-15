namespace Purch.Application.Inventory;

public interface IPurchaseRequestService
{
    Task<IReadOnlyList<PurchaseRequestDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<PurchaseRequestDto> CreateAsync(CreatePurchaseRequestRequest request, CancellationToken cancellationToken = default);

    /// <summary>Open -> Approved. Admin/Manager only (enforced at the endpoint).</summary>
    Task<PurchaseRequestDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Open or Approved -> Cancelled.</summary>
    Task<PurchaseRequestDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Approved -> Converted, creating one draft purchase order per supplier group.</summary>
    Task<IReadOnlyList<PurchaseOrderDto>> ConvertAsync(Guid id, ConvertPurchaseRequestRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RestockSuggestionDto>> GetRestockSuggestionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierLinkDto>> SetSuppliersAsync(Guid? itemId, Guid? inventoryItemId, SetItemSuppliersRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierLinkDto>> GetSuppliersAsync(Guid? itemId, Guid? inventoryItemId, CancellationToken cancellationToken = default);
}
