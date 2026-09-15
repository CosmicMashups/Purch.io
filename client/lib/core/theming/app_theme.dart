import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import 'app_tokens.dart';

/// AppTheme builds the ThemeData for Purch.io.
/// - staffTheme: For the landscape staff tablet app (dense, high-contrast, min 48dp targets).
/// - kioskTheme: For the portrait customer-facing self-order flow (tactile, friendly, inviting, min 64dp buttons).
///
/// Both accept the tenant-configurable colours and custom font family.
/// Each defaults to its AppColors and AppTypography tokens, so calling them with no
/// arguments yields exactly the built-in Purch.io look.
abstract class AppTheme {
  static ThemeData staffTheme({
    Color background = AppColors.background,
    Color accent = AppColors.brandPrimary,
    Color primaryText = AppColors.textPrimary,
    Color secondaryText = AppColors.textSecondary,
    String? fontFamily,
  }) {
    final baseColorScheme = ColorScheme.fromSeed(
      seedColor: accent,
      primary: accent,
      onPrimary: AppColors.onBrandPrimary,
      surface: AppColors.surface,
      surfaceContainerLowest: AppColors.surface,
      surfaceContainerLow: background,
      onSurface: primaryText,
      onSurfaceVariant: secondaryText,
      error: AppColors.error,
    );

    final effectiveFontFamily = (fontFamily != null && fontFamily.trim().isNotEmpty)
        ? fontFamily.trim()
        : AppTypography.defaultFontFamily;
    final resolvedTextTheme = AppTypography.createTextTheme(effectiveFontFamily).apply(
      bodyColor: primaryText,
      displayColor: primaryText,
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: baseColorScheme,
      scaffoldBackgroundColor: background,
      textTheme: resolvedTextTheme,
      fontFamily: resolvedTextTheme.bodyMedium?.fontFamily,
      appBarTheme: AppBarTheme(
        backgroundColor: AppColors.surface,
        foregroundColor: primaryText,
        elevation: 0,
        scrolledUnderElevation: 1,
        centerTitle: false,
        titleTextStyle: AppTypography.getSafeGoogleFont(
          AppTypography.displayFontFamily,
          fontSize: 20,
          fontWeight: FontWeight.w700,
          color: primaryText,
          letterSpacing: -0.2,
        ),
      ),
      cardTheme: CardTheme(
        color: AppColors.card,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: AppRadius.mdBorder,
          side: const BorderSide(color: AppColors.border),
        ),
        margin: EdgeInsets.zero,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: accent,
          foregroundColor: AppColors.onBrandPrimary,
          shape: const RoundedRectangleBorder(borderRadius: AppRadius.mdBorder),
          minimumSize: const Size(48, 48),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
          textStyle: const TextStyle(
            fontSize: 16,
            fontWeight: FontWeight.w600,
            letterSpacing: 0.1,
          ),
        ),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: AppColors.surface,
          foregroundColor: accent,
          elevation: 1,
          shape: const RoundedRectangleBorder(borderRadius: AppRadius.mdBorder),
          minimumSize: const Size(48, 48),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
          textStyle: const TextStyle(
            fontSize: 16,
            fontWeight: FontWeight.w600,
          ),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: accent,
          side: const BorderSide(color: AppColors.border),
          shape: const RoundedRectangleBorder(borderRadius: AppRadius.mdBorder),
          minimumSize: const Size(48, 48),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: AppColors.surface,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        border: const OutlineInputBorder(
          borderRadius: AppRadius.mdBorder,
          borderSide: BorderSide(color: AppColors.border),
        ),
        enabledBorder: const OutlineInputBorder(
          borderRadius: AppRadius.mdBorder,
          borderSide: BorderSide(color: AppColors.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: AppRadius.mdBorder,
          borderSide: BorderSide(color: accent, width: 2),
        ),
        errorBorder: const OutlineInputBorder(
          borderRadius: AppRadius.mdBorder,
          borderSide: BorderSide(color: AppColors.error),
        ),
      ),
    );
  }

  static ThemeData kioskTheme({
    Color background = AppColors.background,
    Color accent = AppColors.brandPrimary,
    Color primaryText = AppColors.textPrimary,
    Color secondaryText = AppColors.textSecondary,
    String? fontFamily,
  }) {
    final staff = staffTheme(
      background: background,
      accent: accent,
      primaryText: primaryText,
      secondaryText: secondaryText,
      fontFamily: fontFamily,
    );
    return staff.copyWith(
      scaffoldBackgroundColor: background,
      appBarTheme: staff.appBarTheme.copyWith(
        backgroundColor: AppColors.surface,
        foregroundColor: primaryText,
        elevation: 0,
        centerTitle: true,
        titleTextStyle: AppTypography.getSafeGoogleFont(
          AppTypography.displayFontFamily,
          fontSize: 22,
          fontWeight: FontWeight.w700,
          color: primaryText,
          letterSpacing: -0.3,
        ),
      ),
      cardTheme: CardTheme(
        color: AppColors.card,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: AppRadius.lgBorder,
          side: const BorderSide(color: AppColors.border, width: 1.2),
        ),
        margin: EdgeInsets.zero,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: accent,
          foregroundColor: AppColors.onBrandPrimary,
          elevation: 2,
          shape: const RoundedRectangleBorder(borderRadius: AppRadius.lgBorder),
          minimumSize: const Size(64, 64),
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
          textStyle: const TextStyle(
            fontSize: 20,
            fontWeight: FontWeight.w700,
            letterSpacing: 0.2,
          ),
        ),
      ),
    );
  }
}
