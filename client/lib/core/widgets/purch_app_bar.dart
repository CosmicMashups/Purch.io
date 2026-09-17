import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../theming/app_tokens.dart';
import '../theming/theme_builder.dart';
import 'purch_image.dart';

/// Standard branding logo avatar used across top bars.
/// Displays the merchant logo configured in Business Settings (via cachedBrandingProvider),
/// or falls back to `assets/logo.jpg` in a circle.
class PurchLogoAvatar extends ConsumerWidget {
  const PurchLogoAvatar({
    super.key,
    this.size = 32,
    this.radius,
  });

  final double size;
  final double? radius;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final branding = ref.watch(cachedBrandingProvider).valueOrNull;
    final logoUrl = branding?.logoUrl;
    final effectiveRadius = radius ?? (size / 2);

    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: AppColors.brandPrimary,
        border: Border.all(
          color: AppColors.border.withValues(alpha: 0.5),
          width: 1,
        ),
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(effectiveRadius),
        child: PurchImage(
          imageUrlOrPath: logoUrl,
          fallbackAsset: 'assets/logo.jpg',
          width: size,
          height: size,
          fit: BoxFit.cover,
          semanticLabel: 'Company Logo',
          errorWidget: ClipOval(
            child: Image.asset(
              'assets/logo.jpg',
              width: size,
              height: size,
              fit: BoxFit.cover,
            ),
          ),
        ),
      ),
    );
  }
}

/// Standardized uniform Top Bar / AppBar for Purch.io screens.
/// Features:
/// - Circular company logo from Business Settings (or assets/logo.jpg fallback)
/// - Screen title (e.g., Dashboard, Cashier, Inventory, Business)
/// - Optional subtitle (e.g. branch or terminal info)
/// - Optional actions (like Cashier's custom action buttons, logout, etc.)
/// - Optional leading widget (or default back button when navigatable)
class PurchAppBar extends ConsumerWidget implements PreferredSizeWidget {
  const PurchAppBar({
    super.key,
    required this.title,
    this.subtitle,
    this.actions,
    this.leading,
    this.automaticallyImplyLeading = true,
    this.bottom,
    this.titleWidget,
    this.backgroundColor,
    this.elevation = 0,
    this.scrolledUnderElevation = 1,
  });

  final String title;
  final Widget? subtitle;
  final List<Widget>? actions;
  final Widget? leading;
  final bool automaticallyImplyLeading;
  final PreferredSizeWidget? bottom;
  final Widget? titleWidget;
  final Color? backgroundColor;
  final double elevation;
  final double scrolledUnderElevation;

  @override
  Size get preferredSize => Size.fromHeight(
        kToolbarHeight + (bottom?.preferredSize.height ?? 0.0),
      );

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final canPop = ModalRoute.of(context)?.canPop ?? false;
    final showLeading = leading != null || (automaticallyImplyLeading && canPop);

    return AppBar(
      backgroundColor: backgroundColor ?? AppColors.surface,
      elevation: elevation,
      scrolledUnderElevation: scrolledUnderElevation,
      automaticallyImplyLeading: automaticallyImplyLeading,
      leading: leading,
      titleSpacing: showLeading ? 0 : AppSpacing.md,
      title: titleWidget ??
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              const PurchLogoAvatar(size: 32),
              const SizedBox(width: 10),
              Flexible(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontWeight: FontWeight.w800,
                        fontSize: 18,
                        letterSpacing: -0.3,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    if (subtitle != null) subtitle!,
                  ],
                ),
              ),
            ],
          ),
      actions: actions,
      bottom: bottom,
    );
  }
}
