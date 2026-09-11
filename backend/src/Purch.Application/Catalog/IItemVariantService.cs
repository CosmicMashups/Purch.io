namespace Purch.Application.Catalog;

public interface IItemVariantService
{
    Task<IReadOnlyList<ItemVariantDto>> ListForItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task<ItemVariantDto> CreateAsync(
        Guid itemId,
        CreateItemVariantRequest request,
        CancellationToken cancellationToken = default);
}
