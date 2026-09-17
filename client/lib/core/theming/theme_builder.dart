import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../db/app_database.dart';
import '../db/db_providers.dart';
import 'app_theme.dart';
import 'app_tokens.dart';

part 'theme_builder.g.dart';

/// Parses a `#RRGGBB` (or `#AARRGGBB`, or the same without the leading `#`)
/// hex string into a [Color]. Returns null for null, blank, or malformed
/// input — callers fall back to their default rather than crashing, because
/// this value comes from a free-text admin field and from whatever the server
/// last handed us.
Color? parseHexColor(String? hex) {
  if (hex == null) return null;

  var value = hex.trim();
  if (value.startsWith('#')) {
    value = value.substring(1);
  }
  if (value.length == 6) {
    value = 'FF$value';
  }
  if (value.length != 8) return null;

  final parsed = int.tryParse(value, radix: 16);
  return parsed == null ? null : Color(parsed);
}

/// Derives a soft, harmonious primary container color from a brand primary color.
/// In light themes, this produces a luminous tinted surface (lightness ~95%, gentle saturation)
/// that pairs gracefully with [brandColor].
Color derivePrimaryContainer(Color brandColor) {
  final hsl = HSLColor.fromColor(brandColor);
  final targetSaturation = (hsl.saturation * 0.40).clamp(0.08, 0.35);
  return hsl.withLightness(0.95).withSaturation(targetSaturation).toColor();
}

/// Derives the matching high-contrast foreground text/icon color for the primary container.
Color deriveOnPrimaryContainer(Color brandColor) {
  final hsl = HSLColor.fromColor(brandColor);
  final targetSaturation = (hsl.saturation * 0.90).clamp(0.50, 0.95);
  return hsl.withLightness(0.20).withSaturation(targetSaturation).toColor();
}

/// The four tenant-configurable theme colours, already resolved to concrete
/// [Color]s — every unset or unparsable value replaced by its AppColors default.
@immutable
class BrandingColors {
  const BrandingColors({
    required this.background,
    required this.accent,
    required this.primaryText,
    required this.secondaryText,
    this.fontFamily,
  });

  /// The built-in Purch.io palette and typography — what the app looks like with no tenant
  /// branding configured at all.
  static const BrandingColors defaults = BrandingColors(
    background: AppColors.background,
    accent: AppColors.brandPrimary,
    primaryText: AppColors.textPrimary,
    secondaryText: AppColors.textSecondary,
    fontFamily: AppTypography.defaultFontFamily,
  );

  /// Resolves a cached branding row (or null, when nothing is cached yet).
  factory BrandingColors.fromCachedBranding(CachedBrandingData? branding) {
    if (branding == null) return defaults;
    return BrandingColors(
      background:
          parseHexColor(branding.backgroundColorHex) ?? defaults.background,
      accent: parseHexColor(branding.accentColorHex) ?? defaults.accent,
      primaryText:
          parseHexColor(branding.primaryTextColorHex) ?? defaults.primaryText,
      secondaryText:
          parseHexColor(branding.secondaryTextColorHex) ??
          defaults.secondaryText,
      fontFamily: branding.fontFamily ?? defaults.fontFamily,
    );
  }

  final Color background;
  final Color accent;
  final Color primaryText;
  final Color secondaryText;
  final String? fontFamily;

  Color get primaryContainer => derivePrimaryContainer(accent);
  Color get onPrimaryContainer => deriveOnPrimaryContainer(accent);

  ThemeData toStaffTheme() => AppTheme.staffTheme(
    background: background,
    accent: accent,
    primaryContainer: primaryContainer,
    onPrimaryContainer: onPrimaryContainer,
    primaryText: primaryText,
    secondaryText: secondaryText,
    fontFamily: fontFamily,
  );

  ThemeData toKioskTheme() => AppTheme.kioskTheme(
    background: background,
    accent: accent,
    primaryContainer: primaryContainer,
    onPrimaryContainer: onPrimaryContainer,
    primaryText: primaryText,
    secondaryText: secondaryText,
    fontFamily: fontFamily,
  );

  @override
  bool operator ==(Object other) =>
      other is BrandingColors &&
      other.background == background &&
      other.accent == accent &&
      other.primaryText == primaryText &&
      other.secondaryText == secondaryText &&
      other.fontFamily == fontFamily;

  @override
  int get hashCode =>
      Object.hash(background, accent, primaryText, secondaryText, fontFamily);
}

/// Streams the single cached branding row for this device. Emits null while
/// nothing is cached (fresh install, or before the first login/sync).
@Riverpod(keepAlive: true)
Stream<CachedBrandingData?> cachedBranding(Ref ref) {
  final db = ref.watch(appDatabaseProvider);
  return (db.select(db.cachedBranding)..limit(1)).watchSingleOrNull();
}

/// The resolved colours currently in force. Defaults are used until (and
/// whenever) branding is unavailable, so the app always has a usable theme.
@Riverpod(keepAlive: true)
BrandingColors brandingColors(Ref ref) {
  final branding = ref.watch(cachedBrandingProvider);
  return BrandingColors.fromCachedBranding(branding.valueOrNull);
}

/// Live ThemeData for the staff shell — watch this instead of calling
/// AppTheme.staffTheme() directly, so admin branding changes take effect.
@Riverpod(keepAlive: true)
ThemeData staffTheme(Ref ref) => ref.watch(brandingColorsProvider).toStaffTheme();

/// Live ThemeData for the customer-facing kiosk shell.
@Riverpod(keepAlive: true)
ThemeData kioskTheme(Ref ref) => ref.watch(brandingColorsProvider).toKioskTheme();
