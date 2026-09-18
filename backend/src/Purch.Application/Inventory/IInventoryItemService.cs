namespace Purch.Application.Inventory;

public interface IInventoryItemService
{
    Task<IReadOnlyList<InventoryItemDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<InventoryItemDto> CreateAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken = default);

    Task<InventoryItemDto> UpdateAsync(Guid inventoryItemId, UpdateInventoryItemRequest request, CancellationToken cancellationToken = default);

    Task<InventoryItemDto> UpdatePhysicalCountAsync(Guid inventoryItemId, UpdatePhysicalCountRequest request, CancellationToken cancellationToken = default);

    Task<InventoryItemDto> ReceiveStockAsync(Guid inventoryItemId, ReceiveInventoryStockRequest request, CancellationToken cancellationToken = default);
}
