namespace Purch.Application.Pos;

/// <summary>Manager/Admin manual discounts and price overrides on the device's open cart. Exclusive with
/// Senior/PWD (409 while it is applied); each action is audited in the same save as the change.</summary>
public interface IManualDiscountService
{
    Task<TransactionDto> SetLineDiscountAsync(Guid lineId, SetManualDiscountRequest request, CancellationToken cancellationToken = default);

    Task<TransactionDto> SetCartDiscountAsync(SetManualDiscountRequest request, CancellationToken cancellationToken = default);

    Task<TransactionDto> OverridePriceAsync(Guid lineId, OverrideLinePriceRequest request, CancellationToken cancellationToken = default);
}
