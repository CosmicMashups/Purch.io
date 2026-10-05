namespace Purch.Application.Catalog;

public interface IItemComboComponentService
{
    Task<IReadOnlyList<ItemComboComponentDto>> ListForItemAsync(Guid parentItemId, CancellationToken cancellationToken = default);

    Task<ItemComboComponentDto> CreateAsync(
        Guid parentItemId,
        CreateItemComboComponentRequest request,
        CancellationToken cancellationToken = default);

    Task<ItemComboComponentDto> UpdateAsync(
        Guid parentItemId,
        Guid componentId,
        UpdateItemComboComponentRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid parentItemId, Guid componentId, CancellationToken cancellationToken = default);
}
