namespace Purch.Application.Catalog;

public interface IItemBatchService
{
    Task<IReadOnlyList<ItemBatchDto>> ListForItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task<ItemBatchDto> ReceiveAsync(Guid itemId, CreateItemBatchRequest request, CancellationToken cancellationToken = default);
}
