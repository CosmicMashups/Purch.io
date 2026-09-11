namespace Purch.Application.Catalog;

public interface IItemComboComponentService
{
    Task<IReadOnlyList<ItemComboComponentDto>> ListForItemAsync(Guid parentItemId, CancellationToken cancellationToken = default);

    Task<ItemComboComponentDto> CreateAsync(
        Guid parentItemId,
        CreateItemComboComponentRequest request,
        CancellationToken cancellationToken = default);
}
