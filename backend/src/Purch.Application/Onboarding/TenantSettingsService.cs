using System.Text.RegularExpressions;
using Purch.Application.Common;
using Purch.Application.Common.Exceptions;

namespace Purch.Application.Onboarding;

public sealed partial class TenantSettingsService(
    ITenantRepository tenantRepository,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentActorProvider currentActorProvider,
    IAuditLogRepository auditLogRepository,
    IUnitOfWork unitOfWork) : ITenantSettingsService
{
    public async Task<TenantSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        return ToDto(tenant);
    }

    public async Task<TenantSettingsDto> UpdateBrandingAsync(UpdateBrandingRequest request, CancellationToken cancellationToken = default)
    {
        ValidateHex(request.BackgroundColorHex, nameof(request.BackgroundColorHex), "Background color");
        ValidateHex(request.AccentColorHex, nameof(request.AccentColorHex), "Accent color");
        ValidateHex(request.PrimaryTextColorHex, nameof(request.PrimaryTextColorHex), "Primary text color");
        ValidateHex(request.SecondaryTextColorHex, nameof(request.SecondaryTextColorHex), "Secondary text color");

        var tenant = await GetCurrentTenantAsync(cancellationToken);
        tenant.BrandingLogoUrl = request.LogoUrl;
        tenant.BrandingBackgroundColorHex = request.BackgroundColorHex;
        tenant.BrandingAccentColorHex = request.AccentColorHex;
        tenant.BrandingPrimaryTextColorHex = request.PrimaryTextColorHex;
        tenant.BrandingSecondaryTextColorHex = request.SecondaryTextColorHex;
        tenant.BrandingFontFamily = request.FontFamily;
        tenant.KioskPosterImageUrl = request.KioskPosterImageUrl;

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

    public async Task<TenantSettingsDto> UpdateCountryAsync(UpdateCountryRequest request, CancellationToken cancellationToken = default)
    {
        var country = request.Country?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!Domain.CountryProfiles.All.ContainsKey(country))
        {
            throw new ValidationException(nameof(request.Country), "That country is not supported yet.");
        }

        var tenant = await GetCurrentTenantAsync(cancellationToken);
        tenant.Country = country;
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(tenant);
    }

    public async Task<TenantSettingsDto> UpdateBarcodeSettingAsync(UpdateBarcodeSettingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ParkedCartTtlHours is < 1 or > 168)
        {
            throw new ValidationException(
                nameof(request.ParkedCartTtlHours),
                "Parked carts must be kept for between 1 and 168 hours.");
        }

        var tenant = await GetCurrentTenantAsync(cancellationToken);
        tenant.RequiresBarcodePerItem = request.RequiresBarcodePerItem;
        if (request.OpenItemEnabled is { } openItemEnabled)
        {
            tenant.OpenItemEnabled = openItemEnabled;
        }

        if (request.ParkedCartTtlHours is { } ttlHours)
        {
            if (ttlHours != tenant.ParkedCartTtlHours)
            {
                auditLogRepository.Add(new Domain.Entities.AuditLog
                {
                    TenantId = tenant.Id,
                    ActorUserId = currentActorProvider.UserId!.Value,
                    ActionType = Domain.Enums.AuditActionType.TenantSettingChanged,
                    TargetEntityType = nameof(Domain.Entities.Tenant),
                    TargetEntityId = tenant.Id,
                    BeforeStateJson = System.Text.Json.JsonSerializer.Serialize(new { parkedCartTtlHours = tenant.ParkedCartTtlHours }),
                    AfterStateJson = System.Text.Json.JsonSerializer.Serialize(new { parkedCartTtlHours = ttlHours }),
                });
            }

            tenant.ParkedCartTtlHours = ttlHours;
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(tenant);
    }

    public async Task<TenantSettingsDto> UpdateCreditLedgerSettingAsync(UpdateCreditLedgerSettingRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        tenant.CreditLedgerEnabled = request.CreditLedgerEnabled;

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(tenant);
    }

    public async Task<TenantSettingsDto> UpdateInventoryTrackingSettingAsync(UpdateInventoryTrackingSettingRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        tenant.UseSeparateInventoryTracking = request.UseSeparateInventoryTracking;
        if (request.UpdateCostOnReceiving is { } updateCost)
        {
            tenant.UpdateCostOnReceiving = updateCost;
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(tenant);
    }

    public async Task<TenantSettingsDto> UpdateDepartmentTrackingSettingAsync(UpdateDepartmentTrackingSettingRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        tenant.UseDepartmentTracking = request.UseDepartmentTracking;

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
        tenant.BrandingBackgroundColorHex,
        tenant.BrandingAccentColorHex,
        tenant.BrandingPrimaryTextColorHex,
        tenant.BrandingSecondaryTextColorHex,
        tenant.BrandingFontFamily,
        tenant.RequiresBarcodePerItem,
        tenant.Tin,
        tenant.RegisteredBusinessName,
        tenant.RegisteredAddress,
        tenant.CreditLedgerRetentionDays,
        tenant.CreditLedgerEnabled,
        tenant.KioskPosterImageUrl,
        tenant.UseSeparateInventoryTracking,
        tenant.UseDepartmentTracking,
        tenant.UpdateCostOnReceiving,
        tenant.OpenItemEnabled,
        tenant.ParkedCartTtlHours,
        tenant.Country);
    }

    private static void ValidateHex(string? value, string field, string label)
    {
        if (value is { } hex && !HexColorPattern().IsMatch(hex))
        {
            throw new ValidationException(field, $"{label} must be a hex value like #4F46E5.");
        }
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorPattern();
}
