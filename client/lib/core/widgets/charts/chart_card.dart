import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../errors/failure.dart';
import '../../theming/app_tokens.dart';
import 'chart_theme.dart';

/// The one card chrome every chart lives in: same 16dp radius, same border,
/// same header rhythm, same body height contract. Charts differ in their
/// marks, never in their frame.
class ChartCard extends StatelessWidget {
  const ChartCard({
    super.key,
    required this.title,
    this.subtitle,
    this.trailing,
    this.footer,
    this.height = 220,
    required this.child,
  });

  final String title;
  final String? subtitle;

  /// Header-right slot — a range filter, an export button, a legend toggle.
  final Widget? trailing;

  /// Optional below-the-plot slot, normally a [ChartLegend].
  final Widget? footer;

  /// Fixed plot height. Charts need a bounded box; leaving this to the parent
  /// is how chart layouts end up inconsistent from card to card.
  final double height;

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: AppRadius.lgBorder,
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.subtle,
      ),
      padding: const EdgeInsets.all(AppSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          _ChartCardHeader(
            title: title,
            subtitle: subtitle,
            trailing: trailing,
          ),
          const SizedBox(height: AppSpacing.lg),
          SizedBox(height: height, child: child),
          if (footer != null) ...[
            const SizedBox(height: AppSpacing.md),
            footer!,
          ],
        ],
      ),
    );
  }
}

/// The card's title/subtitle block beside its optional [trailing] control.
///
/// A trailing control is usually a cluster of chips with a real intrinsic
/// width (the Sales Trend range filter is five of them), so on a narrow
/// page — a phone, or a portrait tablet where the card spans the full width
/// — title + trailing can't share a line without the header `Row`
/// overflowing. Below [_stackBelowWidth] the trailing block moves under the
/// title instead, full-width and left-aligned. With no trailing, or with
/// room for both, the header lays out exactly as it always has.
class _ChartCardHeader extends StatelessWidget {
  const _ChartCardHeader({
    required this.title,
    required this.subtitle,
    required this.trailing,
  });

  /// Enough for a title plus a five-chip filter cluster (~350dp) with the
  /// card's own padding already taken off.
  static const double _stackBelowWidth = 560;

  final String title;
  final String? subtitle;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final titleBlock = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(title, style: AppTypography.titleMd),
        if (subtitle != null) ...[
          const SizedBox(height: 2),
          Text(subtitle!, style: AppTypography.bodySm),
        ],
      ],
    );

    if (trailing == null) {
      return Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [Expanded(child: titleBlock)],
      );
    }

    return LayoutBuilder(
      builder: (context, constraints) {
        if (constraints.maxWidth.isFinite &&
            constraints.maxWidth < _stackBelowWidth) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              titleBlock,
              const SizedBox(height: AppSpacing.md),
              SizedBox(width: double.infinity, child: trailing),
            ],
          );
        }

        return Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(child: titleBlock),
            const SizedBox(width: AppSpacing.md),
            trailing!,
          ],
        );
      },
    );
  }
}

/// A shared, keyed legend row — used by every multi-series chart so a color
/// means the same thing wherever it appears on the page.
class ChartLegend extends StatelessWidget {
  const ChartLegend({super.key, required this.entries});

  final List<ChartLegendEntry> entries;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: AppSpacing.lg,
      runSpacing: AppSpacing.sm,
      children: [
        for (final entry in entries)
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 10,
                height: 10,
                decoration: BoxDecoration(
                  color: entry.color,
                  borderRadius: BorderRadius.circular(3),
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              Text(entry.label, style: ChartTheme.valueLabel),
              if (entry.value != null) ...[
                const SizedBox(width: AppSpacing.xs),
                Text(
                  entry.value!,
                  style: ChartTheme.valueLabel.copyWith(
                    color: AppColors.textPrimary,
                  ),
                ),
              ],
            ],
          ),
      ],
    );
  }
}

class ChartLegendEntry {
  const ChartLegendEntry({
    required this.label,
    required this.color,
    this.value,
  });

  final String label;
  final Color color;
  final String? value;
}

/// The chart-shaped loading state. Deliberately *not* a bare spinner: the
/// app's Operate-mode design floor says a loading surface should hold the
/// shape of what's arriving so the page doesn't jump when it lands.
class ChartSkeleton extends StatelessWidget {
  const ChartSkeleton({super.key, this.barCount = 7});

  final int barCount;

  @override
  Widget build(BuildContext context) {
    // Stable pseudo-random heights — a skeleton that re-shuffles on every
    // rebuild reads as flicker, not as loading.
    const fractions = <double>[0.45, 0.72, 0.58, 0.9, 0.5, 0.78, 0.62, 0.84];

    return Semantics(
      label: 'Loading chart',
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          for (var i = 0; i < barCount; i++) ...[
            if (i > 0) const SizedBox(width: AppSpacing.sm),
            Expanded(
              child: FractionallySizedBox(
                heightFactor: fractions[i % fractions.length],
                child: Container(
                  decoration: BoxDecoration(
                    color: AppColors.borderSubtle,
                    borderRadius: const BorderRadius.vertical(
                      top: Radius.circular(AppRadius.sm),
                    ),
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// Compact in-card empty state. The page-level [EmptyStateView] is too tall
/// for a 200dp plot box, and a chart with nothing in it still needs to say
/// *why* rather than render an empty grid.
class ChartEmptyState extends StatelessWidget {
  const ChartEmptyState({
    super.key,
    required this.icon,
    required this.title,
    this.description,
  });

  final IconData icon;
  final String title;
  final String? description;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 26, color: AppColors.textMuted),
            const SizedBox(height: AppSpacing.sm),
            Text(
              title,
              textAlign: TextAlign.center,
              style: AppTypography.labelMd.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
            if (description != null) ...[
              const SizedBox(height: AppSpacing.xs),
              Text(
                description!,
                textAlign: TextAlign.center,
                style: AppTypography.bodySm,
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// Compact in-card error state with a retry — same reasoning as
/// [ChartEmptyState]: the full-page ErrorStateView doesn't fit a plot box.
class ChartErrorState extends StatelessWidget {
  const ChartErrorState({super.key, required this.message, this.onRetry});

  final String message;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(
              Icons.error_outline_rounded,
              size: 26,
              color: AppColors.error,
            ),
            const SizedBox(height: AppSpacing.sm),
            Text(
              message,
              textAlign: TextAlign.center,
              maxLines: 3,
              overflow: TextOverflow.ellipsis,
              style: AppTypography.bodySm.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
            if (onRetry != null) ...[
              const SizedBox(height: AppSpacing.sm),
              TextButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh_rounded, size: 16),
                label: const Text('Try Again'),
                style: TextButton.styleFrom(
                  foregroundColor: AppColors.brandPrimary,
                  visualDensity: VisualDensity.compact,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// A [ChartCard] wired to an [AsyncValue], with a real loading skeleton, a
/// real empty state and a real error state — the three states every chart on
/// Home is required to implement.
class ChartAsyncCard<T> extends ConsumerWidget {
  const ChartAsyncCard({
    super.key,
    required this.title,
    this.subtitle,
    this.trailing,
    this.height = 220,
    required this.value,
    required this.isEmpty,
    required this.emptyIcon,
    required this.emptyTitle,
    this.emptyDescription,
    this.onRetry,
    required this.builder,
    this.footerBuilder,
    this.skeletonBarCount = 7,
  });

  final String title;
  final String? subtitle;
  final Widget? trailing;
  final double height;

  final AsyncValue<T> value;
  final bool Function(T data) isEmpty;

  final IconData emptyIcon;
  final String emptyTitle;
  final String? emptyDescription;

  final VoidCallback? onRetry;

  final Widget Function(BuildContext context, T data) builder;
  final Widget? Function(BuildContext context, T data)? footerBuilder;

  final int skeletonBarCount;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final data = value.valueOrNull;
    final hasData = value.hasValue && data != null && !isEmpty(data);

    Widget body;
    Widget? footer;

    if (value.isLoading && !value.hasValue) {
      body = ChartSkeleton(barCount: skeletonBarCount);
    } else if (value.hasError && !value.hasValue) {
      body = ChartErrorState(
        message: describeError(value.error!),
        onRetry: onRetry,
      );
    } else if (!hasData) {
      body = ChartEmptyState(
        icon: emptyIcon,
        title: emptyTitle,
        description: emptyDescription,
      );
    } else {
      body = builder(context, data as T);
      footer = footerBuilder?.call(context, data);
    }

    return ChartCard(
      title: title,
      subtitle: subtitle,
      trailing: trailing,
      height: height,
      footer: footer,
      child: body,
    );
  }
}
