namespace Purch.Application.Onboarding;

public interface ITenantSettingsService
{
    Task<TenantSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task<TenantSettingsDto> UpdateBrandingAsync(UpdateBrandingRequest request, CancellationToken cancellationToken = default);

    Task<TenantSettingsDto> UpdateBirSettingsAsync(UpdateBirSettingsRequest request, CancellationToken cancellationToken = default);

    Task<TenantSettingsDto> UpdateBarcodeSettingAsync(UpdateBarcodeSettingRequest request, CancellationToken cancellationToken = default);

    Task<TenantSettingsDto> UpdateCreditLedgerSettingAsync(UpdateCreditLedgerSettingRequest request, CancellationToken cancellationToken = default);
}
