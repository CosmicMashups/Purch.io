using Purch.Domain.Enums;

namespace Purch.Application.Onboarding;

public sealed record TenantSettingsDto(
    Guid Id,
    string Name,
    BusinessType BusinessType,
    string? BrandingLogoUrl,
    string? BrandingThemeColorHex,
    string? BrandingFontFamily,
    bool RequiresBarcodePerItem,
    string? Tin,
    string? RegisteredBusinessName,
    string? RegisteredAddress,
    int? CreditLedgerRetentionDays,
    bool CreditLedgerEnabled,
    string? KioskPosterImageUrl);
