import 'onboarding_enums.dart';

/// Mirrors Purch.Application.Onboarding.TenantSettingsDto.
class TenantSettings {
  const TenantSettings({
    required this.id,
    required this.name,
    required this.businessType,
    required this.brandingLogoUrl,
    required this.brandingBackgroundColorHex,
    required this.brandingAccentColorHex,
    required this.brandingPrimaryTextColorHex,
    required this.brandingSecondaryTextColorHex,
    required this.brandingFontFamily,
    required this.requiresBarcodePerItem,
    required this.tin,
    required this.registeredBusinessName,
    required this.registeredAddress,
    required this.creditLedgerRetentionDays,
    required this.creditLedgerEnabled,
    required this.kioskPosterImageUrl,
    this.useSeparateInventoryTracking = false,
  });

  factory TenantSettings.fromJson(Map<String, dynamic> json) {
    return TenantSettings(
      id: json['id'] as String,
      name: json['name'] as String,
      businessType: BusinessType.values[json['businessType'] as int],
      brandingLogoUrl: json['brandingLogoUrl'] as String?,
      brandingBackgroundColorHex: json['brandingBackgroundColorHex'] as String?,
      brandingAccentColorHex: json['brandingAccentColorHex'] as String?,
      brandingPrimaryTextColorHex:
          json['brandingPrimaryTextColorHex'] as String?,
      brandingSecondaryTextColorHex:
          json['brandingSecondaryTextColorHex'] as String?,
      brandingFontFamily: json['brandingFontFamily'] as String?,
      requiresBarcodePerItem: json['requiresBarcodePerItem'] as bool,
      tin: json['tin'] as String?,
      registeredBusinessName: json['registeredBusinessName'] as String?,
      registeredAddress: json['registeredAddress'] as String?,
      creditLedgerRetentionDays: json['creditLedgerRetentionDays'] as int?,
      creditLedgerEnabled: json['creditLedgerEnabled'] as bool,
      kioskPosterImageUrl: json['kioskPosterImageUrl'] as String?,
      useSeparateInventoryTracking:
          json['useSeparateInventoryTracking'] as bool? ?? false,
    );
  }

  final String id;
  final String name;
  final BusinessType businessType;
  final String? brandingLogoUrl;
  final String? brandingBackgroundColorHex;
  final String? brandingAccentColorHex;
  final String? brandingPrimaryTextColorHex;
  final String? brandingSecondaryTextColorHex;
  final String? brandingFontFamily;
  final bool requiresBarcodePerItem;
  final String? tin;
  final String? registeredBusinessName;
  final String? registeredAddress;
  final int? creditLedgerRetentionDays;
  final bool creditLedgerEnabled;

  /// URL to the promotional poster image shown full-bleed on the kiosk
  /// landing screen (E1). Null = show the wordmark card fallback.
  final String? kioskPosterImageUrl;

  /// Opt-in-per-tenant flag: when true, Cashier items can be backed by
  /// separate InventoryItem records (ingredients) linked via a recipe/BOM,
  /// instead of the item's own direct stockOnHand tracking.
  final bool useSeparateInventoryTracking;
}

/// Mirrors Purch.Application.Onboarding.UpdateBrandingRequest. logoUrl is an
/// already-hosted URL, not a file upload — see the backend's own note on why.
class UpdateBrandingRequest {
  const UpdateBrandingRequest({
    this.logoUrl,
    this.backgroundColorHex,
    this.accentColorHex,
    this.primaryTextColorHex,
    this.secondaryTextColorHex,
    this.fontFamily,
    this.kioskPosterImageUrl,
  });

  final String? logoUrl;
  final String? backgroundColorHex;
  final String? accentColorHex;
  final String? primaryTextColorHex;
  final String? secondaryTextColorHex;
  final String? fontFamily;

  /// Hosted URL for the kiosk landing screen poster image. Null clears it.
  final String? kioskPosterImageUrl;

  Map<String, dynamic> toJson() => {
    'logoUrl': logoUrl,
    'backgroundColorHex': backgroundColorHex,
    'accentColorHex': accentColorHex,
    'primaryTextColorHex': primaryTextColorHex,
    'secondaryTextColorHex': secondaryTextColorHex,
    'fontFamily': fontFamily,
    'kioskPosterImageUrl': kioskPosterImageUrl,
  };
}

/// Mirrors Purch.Application.Onboarding.UpdateBirSettingsRequest.
class UpdateBirSettingsRequest {
  const UpdateBirSettingsRequest({
    this.tin,
    this.registeredBusinessName,
    this.registeredAddress,
    this.creditLedgerRetentionDays,
  });

  final String? tin;
  final String? registeredBusinessName;
  final String? registeredAddress;
  final int? creditLedgerRetentionDays;

  Map<String, dynamic> toJson() => {
    'tin': tin,
    'registeredBusinessName': registeredBusinessName,
    'registeredAddress': registeredAddress,
    'creditLedgerRetentionDays': creditLedgerRetentionDays,
  };
}

/// Mirrors Purch.Application.Onboarding.UpdateCreditLedgerSettingRequest —
/// B7's toggle. Off by default: full checkout enforcement (credit limits,
/// due-date reminders) is a Phase 9 concern once transaction data exists.
class UpdateCreditLedgerSettingRequest {
  const UpdateCreditLedgerSettingRequest({required this.creditLedgerEnabled});

  final bool creditLedgerEnabled;

  Map<String, dynamic> toJson() => {'creditLedgerEnabled': creditLedgerEnabled};
}

/// Mirrors Purch.Application.Onboarding.UpdateInventoryTrackingSettingRequest.
class UpdateInventoryTrackingSettingRequest {
  const UpdateInventoryTrackingSettingRequest({
    required this.useSeparateInventoryTracking,
  });

  final bool useSeparateInventoryTracking;

  Map<String, dynamic> toJson() => {
    'useSeparateInventoryTracking': useSeparateInventoryTracking,
  };
}
