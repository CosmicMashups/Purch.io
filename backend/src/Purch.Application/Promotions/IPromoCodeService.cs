namespace Purch.Application.Promotions;

public interface IPromoCodeService
{
    Task<IReadOnlyList<PromoCodeDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<PromoCodeDto> CreateAsync(CreatePromoCodeRequest request, CancellationToken cancellationToken = default);
}
