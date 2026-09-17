import 'dart:io' show Platform;
import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';

/// Strongly-typed design tokens derived from docs/design/tokens.json.
/// Adheres to taste-skill / design-taste-frontend anti-slop principles:
/// - Distinct Philippine retail deep viridian / pine teal palette (no generic blue or generic AI purple).
/// - Single corner-radius rhythm (16dp cards, 12dp buttons, 8dp chips, 24dp pills).
/// - Tabular figures on all price displays.
/// - Minimum 48dp touch targets, with 64-72dp for customer-facing kiosk buttons.
abstract class AppColors {
  // Brand (Deep Viridian / Pine Teal - non-blue, high-contrast, premium retail)
  static const Color brandPrimary = Color(0xFF0F766E);
  static const Color brandPrimaryHover = Color(0xFF115E59);
  static const Color brandPrimaryActive = Color(0xFF042F2E);
  static const Color brandPrimaryContainer = Color(0xFFF0FDFA);
  static const Color onBrandPrimary = Colors.white;
  static const Color onBrandPrimaryContainer = Color(0xFF115E59);

  // Accents
  static const Color accentWarm = Color(0xFFD97706);
  static const Color accentWarmContainer = Color(0xFFFEF3C7);
  static const Color onAccentWarmContainer = Color(0xFF92400E);
  static const Color accentEmerald = Color(0xFF059669);
  static const Color accentEmeraldContainer = Color(0xFFD1FAE5);
  static const Color onAccentEmeraldContainer = Color(0xFF065F46);

  // Neutrals
  static const Color surface = Colors.white;
  static const Color background = Color(0xFFF8FAFC);
  static const Color card = Colors.white;
  static const Color cardHover = Color(0xFFF1F5F9);
  static const Color border = Color(0xFFE2E8F0);
  static const Color borderSubtle = Color(0xFFF1F5F9);
  static const Color textPrimary = Color(0xFF0F172A);
  static const Color textSecondary = Color(0xFF475569);
  static const Color textMuted = Color(0xFF94A3B8);

  // Semantic
  static const Color success = Color(0xFF059669);
  static const Color successContainer = Color(0xFFDCFCE7);
  static const Color onSuccessContainer = Color(0xFF14532D);
  static const Color successBorder = Color(0xFF86EFAC);

  static const Color warning = Color(0xFFD97706);
  static const Color warningContainer = Color(0xFFFEF3C7);
  static const Color onWarningContainer = Color(0xFF78350F);
  static const Color warningBorder = Color(0xFFFCD34D);

  static const Color error = Color(0xFFDC2626);
  static const Color errorContainer = Color(0xFFFEE2E2);
  static const Color onErrorContainer = Color(0xFF7F1D1D);
  static const Color errorBorder = Color(0xFFFCA5A5);

  static const Color info = Color(0xFF2563EB);
  static const Color infoContainer = Color(0xFFDBEAFE);
  static const Color onInfoContainer = Color(0xFF1E3A8A);
  static const Color infoBorder = Color(0xFF93C5FD);

  // Status neutrals
  static const Color neutralContainer = Color(0xFFF1F5F9);
  static const Color onNeutralContainer = Color(0xFF475569);
  static const Color neutralBorder = Color(0xFFCBD5E1);

  // Harmonized Category Colors (Container, Foreground, Subtle Border)
  static const List<CategoryColorPair> categoryPalette = [
    CategoryColorPair(Color(0xFFEFF6FF), Color(0xFF1E40AF), Color(0xFFBFDBFE)), // Ultramarine
    CategoryColorPair(Color(0xFFECFDF5), Color(0xFF059669), Color(0xFFA7F3D0)), // Emerald
    CategoryColorPair(Color(0xFFFFFBEB), Color(0xFFD97706), Color(0xFFFDE68A)), // Amber
    CategoryColorPair(Color(0xFFF5F3FF), Color(0xFF7C3AED), Color(0xFFDDD6FE)), // Violet
    CategoryColorPair(Color(0xFFFFF1F2), Color(0xFFE11D48), Color(0xFFFECDD3)), // Rose
    CategoryColorPair(Color(0xFFF0FDFA), Color(0xFF0D9488), Color(0xFF99F6E4)), // Teal
    CategoryColorPair(Color(0xFFFFF7ED), Color(0xFFEA580C), Color(0xFFFFEDD5)), // Orange
    CategoryColorPair(Color(0xFFF8FAFC), Color(0xFF475569), Color(0xFFE2E8F0)), // Slate
  ];
}

class CategoryColorPair {
  const CategoryColorPair(this.background, this.foreground, this.border);
  final Color background;
  final Color foreground;
  final Color border;
}

abstract class AppRadius {
  static const double sm = 8.0;
  static const double md = 12.0;
  static const double lg = 16.0;
  static const double xl = 24.0;
  static const double full = 9999.0;

  static const BorderRadius smBorder = BorderRadius.all(Radius.circular(sm));
  static const BorderRadius mdBorder = BorderRadius.all(Radius.circular(md));
  static const BorderRadius lgBorder = BorderRadius.all(Radius.circular(lg));
  static const BorderRadius xlBorder = BorderRadius.all(Radius.circular(xl));
}

abstract class AppSpacing {
  static const double xs = 4.0;
  static const double sm = 8.0;
  static const double md = 12.0;
  static const double lg = 16.0;
  static const double xl = 24.0;
  static const double xxl = 32.0;
}

abstract class AppShadows {
  static const List<BoxShadow> subtle = [
    BoxShadow(
      color: Color(0x0F0F172A),
      blurRadius: 3,
      offset: Offset(0, 1),
    ),
    BoxShadow(
      color: Color(0x0A0F172A),
      blurRadius: 2,
      offset: Offset(0, 1),
    ),
  ];

  static const List<BoxShadow> card = [
    BoxShadow(
      color: Color(0x140F172A),
      blurRadius: 8,
      offset: Offset(0, 4),
    ),
  ];

  static const List<BoxShadow> tactileButton = [
    BoxShadow(
      color: Color(0x400F766E),
      blurRadius: 14,
      offset: Offset(0, 6),
    ),
  ];
}

abstract class AppTypography {
  static const String defaultFontFamily = 'Plus Jakarta Sans';
  static const String displayFontFamily = 'Outfit';
  static const String monoFontFamily = 'JetBrains Mono';

  /// Detects whether code is running inside a test harness (where network font fetching is blocked).
  static bool get isTestEnvironment {
    try {
      return Platform.environment.containsKey('FLUTTER_TEST');
    } catch (_) {
      return false;
    }
  }

  /// Curated POS-grade font presets vetted for retail legibility, touch targets, and receipt fidelity.
  static const List<String> curatedPosFonts = [
    'Plus Jakarta Sans',
    'Outfit',
    'Space Grotesk',
    'Chakra Petch',
    'Cinzel',
    'JetBrains Mono',
  ];

  /// Resolves a [TextTheme] using [fontFamily], falling back to Plus Jakarta Sans.
  static TextTheme createTextTheme(String? fontFamily, [TextTheme? base]) {
    final family = (fontFamily != null && fontFamily.trim().isNotEmpty)
        ? fontFamily.trim()
        : defaultFontFamily;
    if (isTestEnvironment) {
      final t = base ?? ThemeData.light().textTheme;
      return t.apply(fontFamily: family);
    }
    try {
      return GoogleFonts.getTextTheme(family, base);
    } catch (_) {
      return GoogleFonts.plusJakartaSansTextTheme(base);
    }
  }

  /// Safely resolves a [TextStyle] for the requested [fontFamily] with fallback.
  static TextStyle getSafeGoogleFont(
    String? fontFamily, {
    double? fontSize,
    FontWeight? fontWeight,
    Color? color,
    double? letterSpacing,
    double? height,
    List<FontFeature>? fontFeatures,
  }) {
    final family = (fontFamily != null && fontFamily.trim().isNotEmpty)
        ? fontFamily.trim()
        : defaultFontFamily;
    if (isTestEnvironment) {
      return TextStyle(
        fontFamily: family,
        fontSize: fontSize,
        fontWeight: fontWeight,
        color: color,
        letterSpacing: letterSpacing,
        height: height,
        fontFeatures: fontFeatures,
      );
    }
    try {
      return GoogleFonts.getFont(
        family,
        fontSize: fontSize,
        fontWeight: fontWeight,
        color: color,
        letterSpacing: letterSpacing,
        height: height,
        fontFeatures: fontFeatures,
      );
    } catch (_) {
      return GoogleFonts.plusJakartaSans(
        fontSize: fontSize,
        fontWeight: fontWeight,
        color: color,
        letterSpacing: letterSpacing,
        height: height,
        fontFeatures: fontFeatures,
      );
    }
  }

  // General hierarchy — Display weight (Outfit) for kiosk/tab-shell headers
  static TextStyle get headlineLg => getSafeGoogleFont(
    displayFontFamily,
    fontSize: 28,
    fontWeight: FontWeight.w700,
    color: AppColors.textPrimary,
    letterSpacing: -0.4,
    height: 1.15,
  );

  static TextStyle get headlineSm => getSafeGoogleFont(
    displayFontFamily,
    fontSize: 22,
    fontWeight: FontWeight.w700,
    color: AppColors.textPrimary,
    letterSpacing: -0.3,
    height: 1.2,
  );

  // Standard UI hierarchy using primary POS font (Plus Jakarta Sans)
  static TextStyle get titleMd => getSafeGoogleFont(
    defaultFontFamily,
    fontSize: 17,
    fontWeight: FontWeight.w600,
    color: AppColors.textPrimary,
    letterSpacing: -0.1,
  );

  static TextStyle get sectionLabel => getSafeGoogleFont(
    defaultFontFamily,
    fontSize: 13,
    fontWeight: FontWeight.w700,
    color: AppColors.textSecondary,
    letterSpacing: 0.4,
  );

  static TextStyle get body => getSafeGoogleFont(
    defaultFontFamily,
    fontSize: 15,
    fontWeight: FontWeight.w400,
    color: AppColors.textSecondary,
    height: 1.4,
  );

  static TextStyle get bodySm => getSafeGoogleFont(
    defaultFontFamily,
    fontSize: 13,
    fontWeight: FontWeight.w400,
    color: AppColors.textMuted,
    height: 1.3,
  );

  static TextStyle get labelMd => getSafeGoogleFont(
    defaultFontFamily,
    fontSize: 14,
    fontWeight: FontWeight.w600,
    color: AppColors.textPrimary,
  );

  // Monospace / Tabular figures for prices, receipts, barcodes & totals (JetBrains Mono)
  static TextStyle get priceHero => getSafeGoogleFont(
    monoFontFamily,
    fontSize: 32,
    fontWeight: FontWeight.w700,
    color: AppColors.textPrimary,
    letterSpacing: -0.5,
  );

  static TextStyle get priceBadge => getSafeGoogleFont(
    monoFontFamily,
    fontSize: 16,
    fontWeight: FontWeight.w700,
    color: AppColors.brandPrimary,
  );

  static TextStyle get priceLine => getSafeGoogleFont(
    monoFontFamily,
    fontSize: 15,
    fontWeight: FontWeight.w600,
    color: AppColors.textPrimary,
  );

  static TextStyle get receiptMono => getSafeGoogleFont(
    monoFontFamily,
    fontSize: 14,
    fontWeight: FontWeight.w500,
    color: AppColors.textPrimary,
  );

  static TextStyle get receiptMonoBold => getSafeGoogleFont(
    monoFontFamily,
    fontSize: 14,
    fontWeight: FontWeight.w700,
    color: AppColors.textPrimary,
  );

  static TextStyle get badgeSm => getSafeGoogleFont(
    defaultFontFamily,
    fontSize: 11,
    fontWeight: FontWeight.w600,
    letterSpacing: 0.2,
    fontFeatures: const [FontFeature.tabularFigures()],
  );

  static TextStyle get badgeMd => getSafeGoogleFont(
    defaultFontFamily,
    fontSize: 12,
    fontWeight: FontWeight.w600,
    letterSpacing: 0.2,
    fontFeatures: const [FontFeature.tabularFigures()],
  );
}
