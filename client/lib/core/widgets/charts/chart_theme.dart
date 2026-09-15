import 'package:flutter/material.dart';

import '../../theming/app_tokens.dart';

/// One chart visual language for the whole app.
///
/// Every chart on Home (and anywhere else) pulls its series colors, axis
/// styling, grid lines and tooltip chrome from here, so the page reads as one
/// system rather than eight different libraries' defaults. Colors are the
/// existing brand tokens first (ultramarine, amber, emerald), then a small
/// set of deliberately desaturated companions — no decorative-only hues, and
/// nothing that competes with the one accent the design system reserves for
/// state.
abstract class ChartTheme {
  /// Categorical series palette, in the order series should be assigned.
  static const List<Color> series = [
    AppColors.brandPrimary,
    AppColors.accentWarm,
    AppColors.accentEmerald,
    Color(0xFF2563EB),
    Color(0xFF7C3AED),
    Color(0xFF0891B2),
    Color(0xFFBE185D),
    Color(0xFF64748B),
  ];

  static Color seriesColor(int index) => series[index % series.length];

  /// Neutral bar/line fill for "this is the measured value" when there's only
  /// one series and color would carry no information.
  static const Color primarySeries = AppColors.brandPrimary;

  static const Color gridLine = AppColors.borderSubtle;
  static const Color axisLine = AppColors.border;

  static const TextStyle axisLabel = TextStyle(
    fontSize: 11,
    fontWeight: FontWeight.w600,
    color: AppColors.textMuted,
    height: 1.1,
  );

  static const TextStyle valueLabel = TextStyle(
    fontSize: 11,
    fontWeight: FontWeight.w700,
    color: AppColors.textSecondary,
    fontFeatures: [FontFeature.tabularFigures()],
    height: 1.1,
  );

  static const TextStyle tooltipText = TextStyle(
    fontSize: 12,
    fontWeight: FontWeight.w600,
    color: Colors.white,
    fontFeatures: [FontFeature.tabularFigures()],
  );

  /// Single tooltip surface for every chart type.
  static const Color tooltipBackground = Color(0xFF0F172A);

  /// Peso amounts on axes are abbreviated so dense tablet charts stay legible
  /// (₱12.4k, not ₱12,432.00 — the exact figure lives in the tooltip).
  static String compactPeso(double value) {
    final sign = value < 0 ? '-' : '';
    final abs = value.abs();
    if (abs >= 1000000) {
      return '$sign₱${(abs / 1000000).toStringAsFixed(abs >= 10000000 ? 0 : 1)}M';
    }
    if (abs >= 1000) {
      return '$sign₱${(abs / 1000).toStringAsFixed(abs >= 10000 ? 0 : 1)}k';
    }
    return '$sign₱${abs.toStringAsFixed(0)}';
  }

  static String peso(double value) {
    final sign = value < 0 ? '-' : '';
    return '$sign₱${value.abs().toStringAsFixed(2)}';
  }

  static String compactCount(double value) {
    final abs = value.abs();
    if (abs >= 1000) {
      return '${(abs / 1000).toStringAsFixed(abs >= 10000 ? 0 : 1)}k';
    }
    return abs.toStringAsFixed(abs.truncateToDouble() == abs ? 0 : 1);
  }
}
