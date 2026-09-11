using Purch.Application.Common;
using Purch.Application.Common.Exceptions;
using Purch.Domain.Entities;
using Purch.Domain.Enums;

namespace Purch.Application.Promotions;

public sealed class PromoCodeService(
    IPromoCodeRepository promoCodeRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : IPromoCodeService
{
    public async Task<IReadOnlyList<PromoCodeDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var promoCodes = await promoCodeRepository.ListByTenantAsync(CurrentTenantId, cancellationToken);
        return [.. promoCodes.OrderBy(promoCode => promoCode.Code).Select(ToDto)];
    }

    public async Task<PromoCodeDto> CreateAsync(CreatePromoCodeRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ValidationException(nameof(request.Code), "Promo code is required.");
        }

        if (request.DiscountValue <= 0)
        {
            throw new ValidationException(nameof(request.DiscountValue), "Discount value must be greater than zero.");
        }

        if (request.DiscountType == PromoDiscountType.Percentage && request.DiscountValue > 100)
        {
            throw new ValidationException(nameof(request.DiscountValue), "A percentage discount can't exceed 100.");
        }

        var trimmedCode = request.Code.Trim();
        var existing = await promoCodeRepository.GetByCodeAsync(CurrentTenantId, trimmedCode, cancellationToken);
        if (existing is not null)
        {
            throw new ValidationException(nameof(request.Code), "A promo code with this code already exists.");
        }

        var promoCode = new PromoCode
        {
            TenantId = CurrentTenantId,
            Code = trimmedCode,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            IsActive = true,
            ExpiresAt = request.ExpiresAt,
        };

        promoCodeRepository.Add(promoCode);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(promoCode);
    }

    private Guid CurrentTenantId => currentTenantProvider.TenantId
        ?? throw new InvalidOperationException("Promo code management requires an authenticated tenant context.");

    private static PromoCodeDto ToDto(PromoCode promoCode)
    {
        return new(promoCode.Id, promoCode.Code, promoCode.DiscountType, promoCode.DiscountValue, promoCode.IsActive, promoCode.ExpiresAt);
    }
}
