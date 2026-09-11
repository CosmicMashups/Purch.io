namespace Purch.Application.Catalog;

public interface IItemService
{
    Task<IReadOnlyList<ItemDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<ItemDto> CreateAsync(CreateItemRequest request, CancellationToken cancellationToken = default);

    Task<ItemDto> UpdateAsync(Guid itemId, UpdateItemRequest request, CancellationToken cancellationToken = default);

    Task<ItemDto> UpdateTingiConfigAsync(Guid itemId, UpdateTingiConfigRequest request, CancellationToken cancellationToken = default);

    Task<ItemDto> UpdateServiceDurationAsync(Guid itemId, UpdateServiceDurationRequest request, CancellationToken cancellationToken = default);

    Task<ItemDto> UpdateDepartmentAsync(Guid itemId, UpdateItemDepartmentRequest request, CancellationToken cancellationToken = default);
}
