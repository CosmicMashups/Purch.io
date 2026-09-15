using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed record TenantSettingsDto(
    Guid Id,
    string Name,
    BusinessType BusinessType,
    string? BrandingLogoUrl,
    string? BrandingBackgroundColorHex,
    string? BrandingAccentColorHex,
    string? BrandingPrimaryTextColorHex,
    string? BrandingSecondaryTextColorHex,
    string? BrandingFontFamily,
    bool RequiresBarcodePerItem,
    string? Tin,
    string? RegisteredBusinessName,
    string? RegisteredAddress,
    int? CreditLedgerRetentionDays,
    bool CreditLedgerEnabled,
    string? KioskPosterImageUrl);
