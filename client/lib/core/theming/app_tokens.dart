import 'package:flutter/material.dart';

/// Strongly-typed design tokens derived from docs/design/tokens.json.
/// Adheres to taste-skill / design-taste-frontend anti-slop principles:
/// - Distinct Philippine retail ultramarine & amber palette (no generic AI purple).
/// - Single corner-radius rhythm (16dp cards, 12dp buttons, 8dp chips, 24dp pills).
/// - Tabular figures on all price displays.
/// - Minimum 48dp touch targets, with 64-72dp for customer-facing kiosk buttons.
abstract class AppColors {
  // Brand
  static const Color brandPrimary = Color(0xFF1E40AF);
  static const Color brandPrimaryHover = Color(0xFF1D4ED8);
  static const Color brandPrimaryActive = Color(0xFF1E3A8A);
  static const Color brandPrimaryContainer = Color(0xFFEFF6FF);
  static const Color onBrandPrimary = Colors.white;
  static const Color onBrandPrimaryContainer = Color(0xFF1E3A8A);

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
  static const Color warning = Color(0xFFD97706);
  static const Color error = Color(0xFFDC2626);
  static const Color info = Color(0xFF2563EB);
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
      color: Color(0x401E40AF),
      blurRadius: 14,
      offset: Offset(0, 6),
    ),
  ];
}

abstract class AppTypography {
  // General hierarchy — added for the dashboard/tab-shell surfaces, which
  // need real heading weight beyond the price-specific styles below.
  static const TextStyle headlineLg = TextStyle(
    fontSize: 28,
    fontWeight: FontWeight.w700,
    color: AppColors.textPrimary,
    letterSpacing: -0.4,
    height: 1.15,
  );

  static const TextStyle headlineSm = TextStyle(
    fontSize: 22,
    fontWeight: FontWeight.w700,
    color: AppColors.textPrimary,
    letterSpacing: -0.3,
    height: 1.2,
  );

  static const TextStyle titleMd = TextStyle(
    fontSize: 17,
    fontWeight: FontWeight.w600,
    color: AppColors.textPrimary,
    letterSpacing: -0.1,
  );

  static const TextStyle sectionLabel = TextStyle(
    fontSize: 13,
    fontWeight: FontWeight.w700,
    color: AppColors.textSecondary,
    letterSpacing: 0.4,
  );

  static const TextStyle body = TextStyle(
    fontSize: 15,
    fontWeight: FontWeight.w400,
    color: AppColors.textSecondary,
    height: 1.4,
  );

  static const TextStyle bodySm = TextStyle(
    fontSize: 13,
    fontWeight: FontWeight.w400,
    color: AppColors.textMuted,
    height: 1.3,
  );

  static const TextStyle labelMd = TextStyle(
    fontSize: 14,
    fontWeight: FontWeight.w600,
    color: AppColors.textPrimary,
  );

  static const TextStyle priceHero = TextStyle(
    fontSize: 32,
    fontWeight: FontWeight.w700,
    color: AppColors.textPrimary,
    fontFeatures: [FontFeature.tabularFigures()],
    letterSpacing: -0.5,
  );

  static const TextStyle priceBadge = TextStyle(
    fontSize: 16,
    fontWeight: FontWeight.w700,
    color: AppColors.brandPrimary,
    fontFeatures: [FontFeature.tabularFigures()],
  );

  static const TextStyle priceLine = TextStyle(
    fontSize: 15,
    fontWeight: FontWeight.w600,
    color: AppColors.textPrimary,
    fontFeatures: [FontFeature.tabularFigures()],
  );
}
