namespace Purch.Application.Inventory;

public interface IItemRecipeService
{
    Task<IReadOnlyList<ItemRecipeLineDto>> GetRecipeAsync(Guid itemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ItemRecipeLineDto>> ReplaceRecipeAsync(Guid itemId, ReplaceItemRecipeRequest request, CancellationToken cancellationToken = default);
}
