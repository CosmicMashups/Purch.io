namespace Purch.Application.Promotions;

public interface IItemDiscountPromoRuleService
{
    Task<IReadOnlyList<ItemDiscountPromoRuleDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<ItemDiscountPromoRuleDto> CreateAsync(CreateItemDiscountPromoRuleRequest request, CancellationToken cancellationToken = default);

    Task<ItemDiscountPromoRuleDto> UpdateAsync(Guid id, UpdateItemDiscountPromoRuleRequest request, CancellationToken cancellationToken = default);
}
