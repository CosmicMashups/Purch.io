import 'package:flutter/material.dart';

import '../../../../core/theming/app_tokens.dart';

/// Three-state (plus inactive) badge for the automatic promo rules —
/// Scheduled / Live / Ended / Inactive, computed client-side from
/// startsAt/endsAt/isActive compared to DateTime.now().
class PromoStatusBadge extends StatelessWidget {
  const PromoStatusBadge({
    super.key,
    required this.isActive,
    required this.startsAt,
    required this.endsAt,
  });

  final bool isActive;
  final DateTime? startsAt;
  final DateTime? endsAt;

  @override
  Widget build(BuildContext context) {
    final now = DateTime.now();
    late final String label;
    late final Color color;
    late final Color background;

    if (!isActive) {
      label = 'Inactive';
      color = AppColors.textSecondary;
      background = AppColors.cardHover;
    } else if (endsAt != null && now.isAfter(endsAt!)) {
      label = 'Ended';
      color = AppColors.error;
      background = AppColors.error.withValues(alpha: 0.1);
    } else if (startsAt != null && now.isBefore(startsAt!)) {
      label = 'Scheduled';
      color = AppColors.accentWarm;
      background = AppColors.accentWarmContainer;
    } else {
      label = 'Live';
      color = AppColors.accentEmerald;
      background = AppColors.accentEmeraldContainer;
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(AppRadius.full),
      ),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w700,
          color: color,
        ),
      ),
    );
  }
}
