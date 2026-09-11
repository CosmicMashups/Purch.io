using System.Text.RegularExpressions;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;

namespace Purch.Application.Onboarding;

public sealed partial class TenantSettingsService(
    ITenantRepository tenantRepository,
    ICurrentTenantProvider currentTenantProvider,
    IUnitOfWork unitOfWork) : ITenantSettingsService
{
    public async Task<TenantSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        return ToDto(tenant);
    }

    public async Task<TenantSettingsDto> UpdateBrandingAsync(UpdateBrandingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ThemeColorHex is { } hex && !HexColorPattern().IsMatch(hex))
        {
            throw new ValidationException(nameof(request.ThemeColorHex), "Theme color must be a hex value like #4F46E5.");
        }

        var tenant = await GetCurrentTenantAsync(cancellationToken);
        tenant.BrandingLogoUrl = request.LogoUrl;
        tenant.BrandingThemeColorHex = request.ThemeColorHex;
        tenant.BrandingFontFamily = request.FontFamily;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(tenant);
    }

    public async Task<TenantSettingsDto> UpdateBirSettingsAsync(UpdateBirSettingsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.CreditLedgerRetentionDays is < 1)
        {
            throw new ValidationException(
                nameof(request.CreditLedgerRetentionDays),
                "Retention period must be at least 1 day when set.");
        }

        var tenant = await GetCurrentTenantAsync(cancellationToken);
        tenant.Tin = request.Tin;
        tenant.RegisteredBusinessName = request.RegisteredBusinessName;
        tenant.RegisteredAddress = request.RegisteredAddress;
        tenant.CreditLedgerRetentionDays = request.CreditLedgerRetentionDays;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(tenant);
    }

    public async Task<TenantSettingsDto> UpdateBarcodeSettingAsync(UpdateBarcodeSettingRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        tenant.RequiresBarcodePerItem = request.RequiresBarcodePerItem;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(tenant);
    }

    private async Task<Domain.Entities.Tenant> GetCurrentTenantAsync(CancellationToken cancellationToken)
    {
        var tenantId = currentTenantProvider.TenantId
            ?? throw new InvalidOperationException("Tenant settings require an authenticated tenant context.");

        return await tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", tenantId);
    }

    private static TenantSettingsDto ToDto(Domain.Entities.Tenant tenant)
    {
        return new(
        tenant.Id,
        tenant.Name,
        tenant.BusinessType,
        tenant.BrandingLogoUrl,
        tenant.BrandingThemeColorHex,
        tenant.BrandingFontFamily,
        tenant.RequiresBarcodePerItem,
        tenant.Tin,
        tenant.RegisteredBusinessName,
        tenant.RegisteredAddress,
        tenant.CreditLedgerRetentionDays);
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorPattern();
}
