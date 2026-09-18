namespace Purch.Application.Promotions;

public interface IComboPromoRuleService
{
    Task<IReadOnlyList<ComboPromoRuleDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<ComboPromoRuleDto> CreateAsync(CreateComboPromoRuleRequest request, CancellationToken cancellationToken = default);

    Task<ComboPromoRuleDto> UpdateAsync(Guid id, UpdateComboPromoRuleRequest request, CancellationToken cancellationToken = default);
}
