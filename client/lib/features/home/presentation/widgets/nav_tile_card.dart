import 'package:flutter/material.dart';

import '../../../../core/theming/app_tokens.dart';

/// A single destination tile on a tab landing page: icon chip, label, and an
/// optional subtitle, in a bordered card with a chevron affordance. Shared by
/// every tab screen so the app reads as one system rather than five bespoke
/// layouts.
class NavTileCard extends StatelessWidget {
  const NavTileCard({
    super.key,
    required this.icon,
    required this.label,
    this.subtitle,
    required this.onTap,
    this.badgeCount,
    this.iconColor,
    this.iconBackground,
  });

  final IconData icon;
  final String label;
  final String? subtitle;
  final VoidCallback onTap;
  final int? badgeCount;
  final Color? iconColor;
  final Color? iconBackground;

  @override
  Widget build(BuildContext context) {
    final resolvedIconColor = iconColor ?? AppColors.brandPrimary;
    final resolvedIconBackground = iconBackground ?? AppColors.brandPrimaryContainer;

    return Material(
      color: AppColors.card,
      borderRadius: AppRadius.lgBorder,
      child: InkWell(
        borderRadius: AppRadius.lgBorder,
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.all(AppSpacing.lg),
          decoration: BoxDecoration(
            borderRadius: AppRadius.lgBorder,
            border: Border.all(color: AppColors.border),
          ),
          child: Row(
            children: [
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: resolvedIconBackground,
                  borderRadius: AppRadius.mdBorder,
                ),
                child: Icon(icon, color: resolvedIconColor, size: 22),
              ),
              const SizedBox(width: AppSpacing.lg),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(label, style: AppTypography.titleMd),
                    if (subtitle != null) ...[
                      const SizedBox(height: 2),
                      Text(subtitle!, style: AppTypography.bodySm),
                    ],
                  ],
                ),
              ),
              if (badgeCount != null && badgeCount! > 0) ...[
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                  decoration: BoxDecoration(
                    color: AppColors.error,
                    borderRadius: BorderRadius.circular(AppRadius.full),
                  ),
                  child: Text(
                    badgeCount! > 99 ? '99+' : '$badgeCount',
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                const SizedBox(width: AppSpacing.sm),
              ],
              const Icon(Icons.chevron_right_rounded, color: AppColors.textMuted),
            ],
          ),
        ),
      ),
    );
  }
}

/// Section header used above a group of [NavTileCard]s (Inventory's
/// Catalog/Stock/Purchasing, Business's Customers/People/Configuration).
class NavSectionHeader extends StatelessWidget {
  const NavSectionHeader({super.key, required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(AppSpacing.xs, AppSpacing.lg, AppSpacing.xs, AppSpacing.sm),
      child: Text(title.toUpperCase(), style: AppTypography.sectionLabel),
    );
  }
}
