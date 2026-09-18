namespace Purch.Application.Promotions;

public interface IBogoPromoRuleService
{
    Task<IReadOnlyList<BogoPromoRuleDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<BogoPromoRuleDto> CreateAsync(CreateBogoPromoRuleRequest request, CancellationToken cancellationToken = default);

    Task<BogoPromoRuleDto> UpdateAsync(Guid id, UpdateBogoPromoRuleRequest request, CancellationToken cancellationToken = default);
}
