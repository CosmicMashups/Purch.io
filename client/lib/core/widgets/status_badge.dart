import 'package:flutter/material.dart';

import '../theming/app_tokens.dart';

enum StatusBadgeType {
  success,
  warning,
  error,
  info,
  neutral,
}

/// A tactile, double-bezel pill badge representing entity or operational statuses.
/// Adheres to /high-end-visual-design and /impeccable standards:
/// - Distinct semantic contrast (never decorative-only or misleading).
/// - Double-bezel architecture: subtle outer border ring with tailored container fill.
/// - Crisp typography with tabular figure rendering for numbers.
class StatusBadge extends StatelessWidget {
  const StatusBadge({
    super.key,
    required this.label,
    this.type = StatusBadgeType.neutral,
    this.icon,
    this.showDot = false,
    this.isSmall = false,
    this.customBackground,
    this.customForeground,
    this.customBorder,
  });

  /// Factory for an "Active" state.
  factory StatusBadge.active({String label = 'Active', bool isSmall = false}) =>
      StatusBadge(
        label: label,
        type: StatusBadgeType.success,
        showDot: true,
        isSmall: isSmall,
      );

  /// Factory for an "Inactive" state.
  factory StatusBadge.inactive({String label = 'Inactive', bool isSmall = false}) =>
      StatusBadge(
        label: label,
        type: StatusBadgeType.neutral,
        showDot: true,
        isSmall: isSmall,
      );

  /// Factory for stock levels.
  factory StatusBadge.stockLevel({
    required double stockOnHand,
    double? lowStockThreshold,
    bool isSmall = false,
  }) {
    if (stockOnHand <= 0) {
      return StatusBadge(
        label: 'Out of Stock',
        type: StatusBadgeType.error,
        icon: Icons.error_outline_rounded,
        isSmall: isSmall,
      );
    }
    if (lowStockThreshold != null &&
        lowStockThreshold > 0 &&
        stockOnHand <= lowStockThreshold) {
      final formatted = stockOnHand % 1 == 0
          ? stockOnHand.toInt().toString()
          : stockOnHand.toStringAsFixed(1);
      return StatusBadge(
        label: 'Low Stock ($formatted)',
        type: StatusBadgeType.warning,
        icon: Icons.warning_amber_rounded,
        isSmall: isSmall,
      );
    }
    final formatted = stockOnHand % 1 == 0
        ? stockOnHand.toInt().toString()
        : stockOnHand.toStringAsFixed(1);
    return StatusBadge(
      label: '$formatted in stock',
      type: StatusBadgeType.success,
      showDot: true,
      isSmall: isSmall,
    );
  }

  final String label;
  final StatusBadgeType type;
  final IconData? icon;
  final bool showDot;
  final bool isSmall;
  final Color? customBackground;
  final Color? customForeground;
  final Color? customBorder;

  @override
  Widget build(BuildContext context) {
    final (bg, fg, border) = _resolveColors();

    final padding = isSmall
        ? const EdgeInsets.symmetric(horizontal: 7, vertical: 2.5)
        : const EdgeInsets.symmetric(horizontal: 10, vertical: 4);

    final textStyle = (isSmall ? AppTypography.badgeSm : AppTypography.badgeMd)
        .copyWith(color: fg);

    return Container(
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(AppRadius.full),
        border: Border.all(color: border, width: 1),
        boxShadow: const [
          BoxShadow(
            color: Color(0x050F172A),
            blurRadius: 2,
            offset: Offset(0, 1),
          ),
        ],
      ),
      padding: padding,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          if (showDot) ...[
            Container(
              width: isSmall ? 5 : 6,
              height: isSmall ? 5 : 6,
              decoration: BoxDecoration(
                color: fg,
                shape: BoxShape.circle,
              ),
            ),
            SizedBox(width: isSmall ? 4 : 6),
          ] else if (icon != null) ...[
            Icon(
              icon,
              size: isSmall ? 12 : 14,
              color: fg,
            ),
            SizedBox(width: isSmall ? 4 : 5),
          ],
          Text(
            label,
            style: textStyle,
          ),
        ],
      ),
    );
  }

  (Color, Color, Color) _resolveColors() {
    if (customBackground != null &&
        customForeground != null &&
        customBorder != null) {
      return (customBackground!, customForeground!, customBorder!);
    }

    switch (type) {
      case StatusBadgeType.success:
        return (
          AppColors.successContainer,
          AppColors.onSuccessContainer,
          AppColors.successBorder,
        );
      case StatusBadgeType.warning:
        return (
          AppColors.warningContainer,
          AppColors.onWarningContainer,
          AppColors.warningBorder,
        );
      case StatusBadgeType.error:
        return (
          AppColors.errorContainer,
          AppColors.onErrorContainer,
          AppColors.errorBorder,
        );
      case StatusBadgeType.info:
        return (
          AppColors.infoContainer,
          AppColors.onInfoContainer,
          AppColors.infoBorder,
        );
      case StatusBadgeType.neutral:
        return (
          AppColors.neutralContainer,
          AppColors.onNeutralContainer,
          AppColors.neutralBorder,
        );
    }
  }
}
