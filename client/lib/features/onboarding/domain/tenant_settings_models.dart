import 'onboarding_enums.dart';

/// Mirrors Purch.Application.Onboarding.TenantSettingsDto.
class TenantSettings {
  const TenantSettings({
    required this.id,
    required this.name,
    required this.businessType,
    required this.brandingLogoUrl,
    required this.brandingThemeColorHex,
    required this.brandingFontFamily,
    required this.requiresBarcodePerItem,
    required this.tin,
    required this.registeredBusinessName,
    required this.registeredAddress,
    required this.creditLedgerRetentionDays,
    required this.creditLedgerEnabled,
  });

  factory TenantSettings.fromJson(Map<String, dynamic> json) {
    return TenantSettings(
      id: json['id'] as String,
      name: json['name'] as String,
      businessType: BusinessType.values[json['businessType'] as int],
      brandingLogoUrl: json['brandingLogoUrl'] as String?,
      brandingThemeColorHex: json['brandingThemeColorHex'] as String?,
      brandingFontFamily: json['brandingFontFamily'] as String?,
      requiresBarcodePerItem: json['requiresBarcodePerItem'] as bool,
      tin: json['tin'] as String?,
      registeredBusinessName: json['registeredBusinessName'] as String?,
      registeredAddress: json['registeredAddress'] as String?,
      creditLedgerRetentionDays: json['creditLedgerRetentionDays'] as int?,
      creditLedgerEnabled: json['creditLedgerEnabled'] as bool,
    );
  }

  final String id;
  final String name;
  final BusinessType businessType;
  final String? brandingLogoUrl;
  final String? brandingThemeColorHex;
  final String? brandingFontFamily;
  final bool requiresBarcodePerItem;
  final String? tin;
  final String? registeredBusinessName;
  final String? registeredAddress;
  final int? creditLedgerRetentionDays;
  final bool creditLedgerEnabled;
}

/// Mirrors Purch.Application.Onboarding.UpdateBrandingRequest. logoUrl is an
/// already-hosted URL, not a file upload — see the backend's own note on why.
class UpdateBrandingRequest {
  const UpdateBrandingRequest({
    this.logoUrl,
    this.themeColorHex,
    this.fontFamily,
  });

  final String? logoUrl;
  final String? themeColorHex;
  final String? fontFamily;

  Map<String, dynamic> toJson() => {
    'logoUrl': logoUrl,
    'themeColorHex': themeColorHex,
    'fontFamily': fontFamily,
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
