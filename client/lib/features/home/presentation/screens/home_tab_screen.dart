import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/routing/auth_gate.dart';
import '../../../../core/sync/sync_providers.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/charts/chart_card.dart';
import '../../../../core/widgets/charts/chart_primitives.dart';
import '../../../../core/widgets/charts/chart_theme.dart';
import '../../../credit_ledger/presentation/providers/credit_ledger_providers.dart';
import '../../../inventory/domain/inventory_movement_models.dart';
import '../../../inventory/presentation/providers/inventory_providers.dart';
import '../../../onboarding/domain/onboarding_enums.dart';
import '../../../reports/domain/sales_dashboard_models.dart';
import '../../../reports/domain/sales_trend_models.dart';
import '../../../reports/presentation/providers/reports_providers.dart';
import '../providers/home_dashboard_providers.dart';
import '../widgets/nav_tile_card.dart';

/// Home tab: the operational cockpit. Replaces the old launchpad *and* the
/// standalone Reports tab — greeting, the sales trend with a real time-range
/// control, revenue cards with period-over-period movement, quick actions,
/// and compact charts for inventory movement, low stock, cashier
/// performance, shift attendance, and department/category mix.
///
/// Every chart on this page uses the shared `core/widgets/charts` system, so
/// the page reads as one report rather than eight chart libraries' defaults,
/// and every one implements a real skeleton/empty/error state rather than a
/// bare spinner.
class HomeTabScreen extends ConsumerWidget {
  const HomeTabScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final role = ref.watch(currentStaffRoleProvider).value;
    final syncConflicts =
        ref.watch(flaggedSyncRecordsProvider).value?.length ?? 0;
    final paymentReminders =
        role == StaffRole.cashier || role == StaffRole.warehouse
            ? 0
            : ref.watch(creditRemindersProvider()).value?.length ?? 0;

    // Cashiers and warehouse staff don't get the management reporting
    // surface — they'd only ever see permission errors where the charts are.
    final showReporting =
        role == StaffRole.admin || role == StaffRole.manager;

    final range = ref.watch(homeTrendRangeControllerProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Purch.io')),
      body: RefreshIndicator(
        color: AppColors.brandPrimary,
        onRefresh: () async {
          ref.invalidate(salesDashboardProvider);
          ref.invalidate(salesTrendProvider);
          ref.invalidate(movementSummaryProvider);
          ref.invalidate(staffPerformanceProvider);
          ref.invalidate(departmentSalesProvider);
          ref.invalidate(categorySalesProvider);
        },
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          children: [
            Text(_greeting(), style: AppTypography.headlineSm),
            const SizedBox(height: 2),
            Text(
              "Here's what's happening right now.",
              style: AppTypography.body,
            ),
            const SizedBox(height: AppSpacing.xl),

            if (showReporting) ...[
              _SalesTrendSection(range: range),
              const SizedBox(height: AppSpacing.lg),
              const _RevenueSummarySection(),
              const SizedBox(height: AppSpacing.xl),
            ],

            const NavSectionHeader(title: 'Quick actions'),
            _QuickActions(
              paymentReminders: paymentReminders,
              showExports: showReporting,
            ),

            if (syncConflicts > 0) ...[
              const SizedBox(height: AppSpacing.xl),
              const NavSectionHeader(title: 'Needs your attention'),
              NavTileCard(
                icon: Icons.sync_problem_outlined,
                label: 'Sync Conflicts',
                subtitle:
                    '$syncConflicts record${syncConflicts == 1 ? '' : 's'} waiting for review',
                iconColor: AppColors.error,
                iconBackground: const Color(0xFFFEE2E2),
                badgeCount: syncConflicts,
                onTap: () => context.push('/home/sync-conflicts'),
              ),
            ],

            if (showReporting) ...[
              const SizedBox(height: AppSpacing.xl),
              const NavSectionHeader(title: 'Stock'),
              _ChartGrid(
                children: [
                  _MovementSummaryChart(range: range),
                  const _LowStockChart(),
                ],
              ),
              const SizedBox(height: AppSpacing.xl),
              const NavSectionHeader(title: 'People'),
              _ChartGrid(
                children: [
                  _SalesPerCashierChart(range: range),
                  _ShiftAttendanceChart(range: range),
                ],
              ),
              const SizedBox(height: AppSpacing.xl),
              const NavSectionHeader(title: 'Sales mix'),
              _ChartGrid(
                children: [
                  _DepartmentBreakdownChart(range: range),
                  const _CategorySalesChart(),
                ],
              ),
            ],
            const SizedBox(height: AppSpacing.xl),
          ],
        ),
      ),
    );
  }

  String _greeting() {
    final hour = DateTime.now().hour;
    if (hour < 12) return 'Good morning';
    if (hour < 18) return 'Good afternoon';
    return 'Good evening';
  }
}

/// Two columns on a landscape tablet, one on a phone or a portrait tablet.
class _ChartGrid extends StatelessWidget {
  const _ChartGrid({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        if (constraints.maxWidth < 760) {
          return Column(
            children: [
              for (var i = 0; i < children.length; i++) ...[
                if (i > 0) const SizedBox(height: AppSpacing.lg),
                children[i],
              ],
            ],
          );
        }
        return Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            for (var i = 0; i < children.length; i++) ...[
              if (i > 0) const SizedBox(width: AppSpacing.lg),
              Expanded(child: children[i]),
            ],
          ],
        );
      },
    );
  }
}

// ---------------------------------------------------------------------------
// 2. Sales trend
// ---------------------------------------------------------------------------

class _SalesTrendSection extends ConsumerWidget {
  const _SalesTrendSection({required this.range});

  final HomeTrendRange range;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final params = (
      fromDate: range.from,
      toDate: range.to,
      granularity: range.granularity,
    );
    final trendAsync = ref.watch(
      salesTrendProvider(
        fromDate: params.fromDate,
        toDate: params.toDate,
        granularity: params.granularity,
      ),
    );

    return ChartAsyncCard<SalesTrendSeries>(
      title: 'Sales Trend',
      subtitle: range.description,
      height: 240,
      trailing: _RangeFilter(range: range),
      value: trendAsync,
      isEmpty: (series) => series.isEmpty,
      emptyIcon: Icons.show_chart_rounded,
      emptyTitle: 'No sales recorded in this range.',
      emptyDescription:
          'Completed sales appear here within a few seconds of checkout.',
      onRetry:
          () => ref.invalidate(
            salesTrendProvider(
              fromDate: params.fromDate,
              toDate: params.toDate,
              granularity: params.granularity,
            ),
          ),
      builder:
          (context, series) => TrendLineChart(
            data: [
              for (final point in series.points)
                ChartDatum(label: point.label, value: point.revenue),
            ],
          ),
      footerBuilder: (context, series) => _TrendFooter(series: series),
    );
  }
}

class _TrendFooter extends StatelessWidget {
  const _TrendFooter({required this.series});

  final SalesTrendSeries series;

  @override
  Widget build(BuildContext context) {
    final change = series.changeFraction;
    final coverageNote = _coverageNote();

    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        Text(
          ChartTheme.peso(series.total),
          style: AppTypography.priceLine.copyWith(fontSize: 17),
        ),
        const SizedBox(width: AppSpacing.md),
        if (change == null)
          Text('No comparable prior period', style: AppTypography.bodySm)
        else
          _DeltaPill(fraction: change),
        const Spacer(),
        if (coverageNote != null)
          Flexible(
            child: Text(
              coverageNote,
              textAlign: TextAlign.right,
              maxLines: 2,
              style: AppTypography.bodySm,
            ),
          ),
      ],
    );
  }

  /// Says so out loud when the server's data doesn't span the whole selected
  /// window, rather than letting a short line read as "we sold nothing".
  String? _coverageNote() {
    final coverageFrom = series.coverageFrom;
    if (coverageFrom == null || !coverageFrom.isAfter(series.from)) {
      return null;
    }
    return 'Data available from ${HomeTrendRange.formatDay(coverageFrom)}';
  }
}

/// The +X% / -X% indicator. Green up, red down, neutral when flat — the one
/// accent the design system reserves for state, used for exactly that.
class _DeltaPill extends StatelessWidget {
  const _DeltaPill({required this.fraction, this.compact = false});

  final double fraction;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final isFlat = fraction.abs() < 0.0005;
    final isUp = fraction > 0;

    final color =
        isFlat
            ? AppColors.textMuted
            : isUp
            ? AppColors.success
            : AppColors.error;
    final background =
        isFlat
            ? AppColors.borderSubtle
            : isUp
            ? AppColors.accentEmeraldContainer
            : const Color(0xFFFEE2E2);
    final icon =
        isFlat
            ? Icons.remove_rounded
            : isUp
            ? Icons.arrow_upward_rounded
            : Icons.arrow_downward_rounded;

    final percent = (fraction * 100).abs();
    final label =
        isFlat
            ? 'Flat'
            : '${isUp ? '+' : '-'}${percent.toStringAsFixed(percent >= 10 ? 0 : 1)}%';

    return Container(
      padding: EdgeInsets.symmetric(
        horizontal: compact ? 6 : AppSpacing.sm,
        vertical: compact ? 2 : 4,
      ),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(AppRadius.full),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: compact ? 12 : 14, color: color),
          const SizedBox(width: 3),
          Text(
            label,
            style: TextStyle(
              fontSize: compact ? 11 : 12,
              fontWeight: FontWeight.w700,
              color: color,
              fontFeatures: const [FontFeature.tabularFigures()],
            ),
          ),
        ],
      ),
    );
  }
}

/// Day / Week / Month / Year presets plus a calendar-picked custom range.
class _RangeFilter extends ConsumerWidget {
  const _RangeFilter({required this.range});

  final HomeTrendRange range;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final controller = ref.read(homeTrendRangeControllerProvider.notifier);

    return Wrap(
      spacing: AppSpacing.xs,
      children: [
        for (final granularity in SalesTrendGranularity.values)
          _RangeChip(
            label: granularity.label,
            selected: !range.isCustom && range.granularity == granularity,
            onTap: () => controller.selectPreset(granularity),
          ),
        _RangeChip(
          label: 'Custom',
          icon: Icons.calendar_today_rounded,
          selected: range.isCustom,
          onTap: () async {
            final now = DateTime.now();
            final picked = await showDateRangePicker(
              context: context,
              firstDate: DateTime(now.year - 5),
              lastDate: now,
              initialDateRange: DateTimeRange(start: range.from, end: range.to),
              helpText: 'Select a reporting range',
            );
            if (picked != null) {
              controller.selectCustom(picked.start, picked.end);
            }
          },
        ),
      ],
    );
  }
}

class _RangeChip extends StatelessWidget {
  const _RangeChip({
    required this.label,
    required this.selected,
    required this.onTap,
    this.icon,
  });

  final String label;
  final bool selected;
  final VoidCallback onTap;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected ? AppColors.brandPrimaryContainer : Colors.transparent,
      borderRadius: BorderRadius.circular(AppRadius.sm),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.sm),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.md,
            vertical: 7,
          ),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadius.sm),
            border: Border.all(
              color: selected ? AppColors.brandPrimary : AppColors.border,
            ),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (icon != null) ...[
                Icon(
                  icon,
                  size: 13,
                  color:
                      selected
                          ? AppColors.brandPrimary
                          : AppColors.textSecondary,
                ),
                const SizedBox(width: 5),
              ],
              Text(
                label,
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: selected ? FontWeight.w700 : FontWeight.w600,
                  color:
                      selected
                          ? AppColors.brandPrimary
                          : AppColors.textSecondary,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// 3. Revenue cards with period-over-period indicators
// ---------------------------------------------------------------------------

class _RevenueSummarySection extends ConsumerWidget {
  const _RevenueSummarySection();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final dashboardAsync = ref.watch(salesDashboardProvider());

    return dashboardAsync.when(
      loading:
          () => const Row(
            children: [
              Expanded(child: _RevenueCardSkeleton()),
              SizedBox(width: AppSpacing.md),
              Expanded(child: _RevenueCardSkeleton()),
              SizedBox(width: AppSpacing.md),
              Expanded(child: _RevenueCardSkeleton()),
            ],
          ),
      error:
          (error, _) => Container(
            padding: const EdgeInsets.all(AppSpacing.lg),
            decoration: BoxDecoration(
              color: AppColors.card,
              borderRadius: AppRadius.lgBorder,
              border: Border.all(color: AppColors.error.withValues(alpha: 0.3)),
            ),
            child: Row(
              children: [
                const Icon(
                  Icons.error_outline_rounded,
                  size: 20,
                  color: AppColors.error,
                ),
                const SizedBox(width: AppSpacing.md),
                Expanded(
                  child: Text(
                    'Revenue totals unavailable: ${describeError(error)}',
                    style: AppTypography.bodySm,
                  ),
                ),
                TextButton(
                  onPressed: () => ref.invalidate(salesDashboardProvider),
                  child: const Text('Try Again'),
                ),
              ],
            ),
          ),
      data: (dashboard) {
        return Row(
          children: [
            Expanded(
              child: _RevenueCard(
                label: 'Today',
                amount: dashboard.revenueToday,
                change: _periodChange(dashboard.trend, days: 1),
                isPrimary: true,
              ),
            ),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: _RevenueCard(
                label: 'Last 7 Days',
                amount: dashboard.revenueLast7Days,
                change: _periodChange(dashboard.trend, days: 7),
              ),
            ),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: _RevenueCard(
                label: 'Last 30 Days',
                amount: dashboard.revenueLast30Days,
                change: _periodChange(dashboard.trend, days: 30),
              ),
            ),
          ],
        );
      },
    );
  }
}

/// Current-vs-prior change for a rolling `days`-long window, computed from
/// the dashboard's own daily series.
///
/// Returns null — and the card then says "no prior data" rather than showing
/// a number — whenever the series doesn't reach back far enough to cover a
/// full prior window, or the prior window was zero. A percentage change from
/// zero revenue is not a real figure and the page won't print one.
double? _periodChange(List<DailyRevenuePoint> trend, {required int days}) {
  if (trend.isEmpty) {
    return null;
  }

  final today = DateTime.now();
  final endOfCurrent = DateTime(today.year, today.month, today.day);
  final startOfCurrent = endOfCurrent.subtract(Duration(days: days - 1));
  final startOfPrior = startOfCurrent.subtract(Duration(days: days));

  var earliest = DateTime(9999);
  var current = 0.0;
  var prior = 0.0;

  for (final point in trend) {
    final day = DateTime(point.date.year, point.date.month, point.date.day);
    if (day.isBefore(earliest)) {
      earliest = day;
    }
    if (!day.isBefore(startOfCurrent) && !day.isAfter(endOfCurrent)) {
      current += point.revenue;
    } else if (!day.isBefore(startOfPrior) && day.isBefore(startOfCurrent)) {
      prior += point.revenue;
    }
  }

  if (earliest.isAfter(startOfPrior) || prior == 0) {
    return null;
  }
  return (current - prior) / prior;
}

class _RevenueCard extends StatelessWidget {
  const _RevenueCard({
    required this.label,
    required this.amount,
    required this.change,
    this.isPrimary = false,
  });

  final String label;
  final double amount;
  final double? change;
  final bool isPrimary;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.lg),
      decoration: BoxDecoration(
        color: isPrimary ? AppColors.brandPrimaryContainer : AppColors.card,
        borderRadius: AppRadius.lgBorder,
        border: Border.all(
          color:
              isPrimary
                  ? AppColors.brandPrimary.withValues(alpha: 0.25)
                  : AppColors.border,
        ),
        boxShadow: isPrimary ? null : AppShadows.subtle,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            label.toUpperCase(),
            style: AppTypography.sectionLabel.copyWith(
              fontSize: 11,
              color:
                  isPrimary ? AppColors.brandPrimary : AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: Alignment.centerLeft,
            child: Text(
              ChartTheme.peso(amount),
              style: AppTypography.priceHero.copyWith(
                fontSize: 24,
                color:
                    isPrimary ? AppColors.brandPrimary : AppColors.textPrimary,
              ),
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          if (change == null)
            Text(
              'No prior period to compare',
              maxLines: 2,
              style: AppTypography.bodySm.copyWith(fontSize: 11),
            )
          else
            Row(
              children: [
                _DeltaPill(fraction: change!, compact: true),
                const SizedBox(width: AppSpacing.xs),
                Flexible(
                  child: Text(
                    'vs prior',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: AppTypography.bodySm.copyWith(fontSize: 11),
                  ),
                ),
              ],
            ),
        ],
      ),
    );
  }
}

class _RevenueCardSkeleton extends StatelessWidget {
  const _RevenueCardSkeleton();

  @override
  Widget build(BuildContext context) {
    Widget bar(double width, double height) => Container(
      width: width,
      height: height,
      decoration: BoxDecoration(
        color: AppColors.borderSubtle,
        borderRadius: BorderRadius.circular(4),
      ),
    );

    return Container(
      padding: const EdgeInsets.all(AppSpacing.lg),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: AppRadius.lgBorder,
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          bar(52, 9),
          const SizedBox(height: AppSpacing.md),
          bar(96, 20),
          const SizedBox(height: AppSpacing.md),
          bar(64, 9),
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// 4. Quick actions
// ---------------------------------------------------------------------------

class _QuickActions extends ConsumerWidget {
  const _QuickActions({
    required this.paymentReminders,
    required this.showExports,
  });

  final int paymentReminders;
  final bool showExports;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Column(
      children: [
        _NewSaleCard(onTap: () => context.push('/home/new-sale')),
        const SizedBox(height: AppSpacing.md),
        Row(
          children: [
            Expanded(
              child: _QuickActionButton(
                icon: Icons.point_of_sale_outlined,
                label: 'Shift / Drawer',
                onTap: () => context.push('/home/shift'),
              ),
            ),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: _QuickActionButton(
                icon: Icons.notifications_active_outlined,
                label: 'Payment Reminders',
                badgeCount: paymentReminders,
                onTap: () => context.push('/home/payment-reminders'),
              ),
            ),
            if (showExports) ...[
              const SizedBox(width: AppSpacing.md),
              Expanded(
                child: _LowStockExportButton(),
              ),
            ],
          ],
        ),
      ],
    );
  }
}

/// The low-stock/reorder CSV export, moved off the deleted Inventory Reports
/// screen. It's a utility action, not a view, so it lives as a button.
class _LowStockExportButton extends ConsumerWidget {
  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(lowStockExportControllerProvider);

    ref.listen(lowStockExportControllerProvider, (previous, next) {
      if (next.isLoading) {
        return;
      }
      if (next.hasError) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Export failed: ${describeError(next.error!)}'),
            backgroundColor: AppColors.error,
          ),
        );
        return;
      }
      final csv = next.valueOrNull;
      if (csv != null) {
        _showCsvDialog(context, csv);
      }
    });

    return _QuickActionButton(
      icon: Icons.file_download_outlined,
      label: 'Reorder CSV',
      busy: state.isLoading,
      onTap:
          () =>
              ref.read(lowStockExportControllerProvider.notifier).generate(),
    );
  }

  void _showCsvDialog(BuildContext context, String csv) {
    final rows = csv.trim().isEmpty ? 0 : csv.trim().split('\n').length - 1;

    showDialog<void>(
      context: context,
      builder:
          (context) => AlertDialog(
            shape: const RoundedRectangleBorder(
              borderRadius: AppRadius.lgBorder,
            ),
            title: const Text('Low-stock reorder export'),
            content: SizedBox(
              width: 520,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    rows == 0
                        ? 'Nothing is below its reorder threshold right now — the export is empty.'
                        : '$rows item${rows == 1 ? '' : 's'} below threshold. Copy the rows below into your supplier sheet.',
                    style: AppTypography.body,
                  ),
                  if (rows > 0) ...[
                    const SizedBox(height: AppSpacing.md),
                    Container(
                      constraints: const BoxConstraints(maxHeight: 260),
                      padding: const EdgeInsets.all(AppSpacing.md),
                      decoration: BoxDecoration(
                        color: AppColors.background,
                        borderRadius: AppRadius.mdBorder,
                        border: Border.all(color: AppColors.border),
                      ),
                      child: SingleChildScrollView(
                        child: SelectableText(
                          csv,
                          style: const TextStyle(
                            fontSize: 12,
                            height: 1.5,
                            fontFeatures: [FontFeature.tabularFigures()],
                          ),
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(context).pop(),
                child: const Text('Close'),
              ),
            ],
          ),
    );
  }
}

class _NewSaleCard extends StatelessWidget {
  const _NewSaleCard({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.brandPrimary,
      borderRadius: AppRadius.lgBorder,
      child: InkWell(
        borderRadius: AppRadius.lgBorder,
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.all(AppSpacing.xl),
          decoration: const BoxDecoration(
            borderRadius: AppRadius.lgBorder,
            boxShadow: AppShadows.tactileButton,
          ),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      'New Sale',
                      style: AppTypography.headlineSm.copyWith(
                        color: Colors.white,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'Ring up an order',
                      style: AppTypography.body.copyWith(
                        color: Colors.white.withValues(alpha: 0.85),
                      ),
                    ),
                  ],
                ),
              ),
              Container(
                width: 52,
                height: 52,
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.16),
                  borderRadius: AppRadius.mdBorder,
                ),
                child: const Icon(
                  Icons.arrow_forward_rounded,
                  color: Colors.white,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _QuickActionButton extends StatelessWidget {
  const _QuickActionButton({
    required this.icon,
    required this.label,
    required this.onTap,
    this.badgeCount = 0,
    this.busy = false,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final int badgeCount;
  final bool busy;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.card,
      borderRadius: AppRadius.lgBorder,
      child: InkWell(
        borderRadius: AppRadius.lgBorder,
        onTap: busy ? null : onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(
            vertical: AppSpacing.lg,
            horizontal: AppSpacing.md,
          ),
          decoration: BoxDecoration(
            borderRadius: AppRadius.lgBorder,
            border: Border.all(color: AppColors.border),
          ),
          child: Column(
            children: [
              SizedBox(
                height: 26,
                child: Stack(
                  clipBehavior: Clip.none,
                  children: [
                    if (busy)
                      const SizedBox(
                        width: 22,
                        height: 22,
                        child: CircularProgressIndicator(
                          strokeWidth: 2.5,
                          color: AppColors.brandPrimary,
                        ),
                      )
                    else
                      Icon(icon, color: AppColors.brandPrimary, size: 26),
                    if (badgeCount > 0)
                      Positioned(
                        right: -8,
                        top: -6,
                        child: Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 6,
                            vertical: 2,
                          ),
                          decoration: BoxDecoration(
                            color: AppColors.error,
                            borderRadius: BorderRadius.circular(AppRadius.full),
                          ),
                          child: Text(
                            badgeCount > 99 ? '99+' : '$badgeCount',
                            style: const TextStyle(
                              color: Colors.white,
                              fontSize: 11,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: AppSpacing.sm),
              Text(
                label,
                textAlign: TextAlign.center,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: AppTypography.labelMd,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// 5. Movement summary
// ---------------------------------------------------------------------------

/// Movement types carry meaning, so they carry fixed colors: inbound is
/// brand, losses are error/amber, neutral bookkeeping is slate. The same
/// mapping is used anywhere movement types are charted.
Color _movementColor(MovementType type) => switch (type) {
  MovementType.stockIn => AppColors.brandPrimary,
  MovementType.stockOut => const Color(0xFF2563EB),
  MovementType.consumption => const Color(0xFF0891B2),
  MovementType.spoiled => AppColors.accentWarm,
  MovementType.damaged => AppColors.error,
  MovementType.forReturn => const Color(0xFF7C3AED),
  MovementType.transfer => const Color(0xFF64748B),
  MovementType.adjustment => AppColors.accentEmerald,
};

class _MovementSummaryChart extends ConsumerWidget {
  const _MovementSummaryChart({required this.range});

  final HomeTrendRange range;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final summaryAsync = ref.watch(
      movementSummaryProvider(fromDate: range.from, toDate: range.to),
    );

    return ChartAsyncCard(
      title: 'Stock Movement',
      subtitle: 'Quantity moved by reason · ${range.description}',
      height: 200,
      value: summaryAsync,
      isEmpty: (summary) => summary.byType.isEmpty,
      emptyIcon: Icons.swap_vert_rounded,
      emptyTitle: 'No stock movements in this range.',
      emptyDescription:
          'Stock-ins, spoilage and adjustments show up here as they are recorded.',
      onRetry:
          () => ref.invalidate(
            movementSummaryProvider(fromDate: range.from, toDate: range.to),
          ),
      builder: (context, summary) {
        final sorted = [...summary.byType]
          ..sort((a, b) => b.totalQuantity.compareTo(a.totalQuantity));
        return GroupedBarChart(
          labels: [for (final entry in sorted) entry.type.label],
          maxLabelLines: 2,
          colorsPerGroup: [
            for (final entry in sorted) _movementColor(entry.type),
          ],
          series: [
            ChartSeries(
              name: 'Quantity',
              color: ChartTheme.primarySeries,
              values: [for (final entry in sorted) entry.totalQuantity],
            ),
          ],
        );
      },
      footerBuilder: (context, summary) {
        final sorted = [...summary.byType]
          ..sort((a, b) => b.totalQuantity.compareTo(a.totalQuantity));
        return ChartLegend(
          entries: [
            for (final entry in sorted.take(4))
              ChartLegendEntry(
                label: entry.type.label,
                color: _movementColor(entry.type),
                value:
                    '${ChartTheme.compactCount(entry.totalQuantity)} · ${entry.movementCount} record${entry.movementCount == 1 ? '' : 's'}',
              ),
          ],
        );
      },
    );
  }
}

// ---------------------------------------------------------------------------
// 6. Low stock
// ---------------------------------------------------------------------------

class _LowStockChart extends ConsumerWidget {
  const _LowStockChart();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final dashboardAsync = ref.watch(inventoryDashboardNotifierProvider);

    return ChartAsyncCard(
      title: 'Running Low',
      subtitle: 'Stock on hand against its reorder threshold',
      height: 200,
      value: dashboardAsync,
      isEmpty: (dashboard) => dashboard.lowStockItems.isEmpty,
      emptyIcon: Icons.check_circle_outline_rounded,
      emptyTitle: 'Nothing is running low right now.',
      emptyDescription:
          'Items drop in here once stock on hand falls to their threshold.',
      onRetry:
          () => ref.read(inventoryDashboardNotifierProvider.notifier).refresh(),
      builder: (context, dashboard) {
        // Most urgent first: the smallest fraction of its own threshold.
        final items = [...dashboard.lowStockItems]..sort((a, b) {
          final aRatio =
              a.lowStockThreshold <= 0 ? 0 : a.stockOnHand / a.lowStockThreshold;
          final bRatio =
              b.lowStockThreshold <= 0 ? 0 : b.stockOnHand / b.lowStockThreshold;
          return aRatio.compareTo(bRatio);
        });

        return RankedBarList(
          data: [
            for (final item in items.take(8))
              ChartDatum(
                label: item.itemName,
                value:
                    item.lowStockThreshold <= 0
                        ? 0
                        : (item.stockOnHand / item.lowStockThreshold).clamp(
                          0.0,
                          1.0,
                        ),
                color:
                    item.stockOnHand <= 0
                        ? AppColors.error
                        : AppColors.accentWarm,
                tooltipLabel: item.itemName,
              ),
          ],
          formatValue: (value) => '${(value * 100).toStringAsFixed(0)}%',
          trailingBuilder: (datum) {
            final item = items.firstWhere((i) => i.itemName == datum.label);
            return '${item.stockOnHand.toStringAsFixed(0)} left · alert at ${item.lowStockThreshold.toStringAsFixed(0)}';
          },
        );
      },
    );
  }
}

// ---------------------------------------------------------------------------
// 7 & 8. Staff
// ---------------------------------------------------------------------------

class _SalesPerCashierChart extends ConsumerWidget {
  const _SalesPerCashierChart({required this.range});

  final HomeTrendRange range;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final reportAsync = ref.watch(
      staffPerformanceProvider(fromDate: range.from, toDate: range.to),
    );

    return ChartAsyncCard(
      title: 'Sales per Cashier',
      subtitle: 'Revenue rung up · ${range.description}',
      height: 200,
      value: reportAsync,
      isEmpty: (report) => report.sales.isEmpty,
      emptyIcon: Icons.badge_outlined,
      emptyTitle: 'No staff sales in this range.',
      emptyDescription:
          'Each cashier appears here once they complete a sale in the window.',
      onRetry:
          () => ref.invalidate(
            staffPerformanceProvider(fromDate: range.from, toDate: range.to),
          ),
      builder: (context, report) {
        final sales = [...report.sales]
          ..sort((a, b) => b.totalSales.compareTo(a.totalSales));
        final top = sales.take(6).toList();

        return GroupedBarChart(
          labels: [for (final entry in top) _firstName(entry.staffName)],
          formatAxisValue: ChartTheme.compactPeso,
          series: [
            ChartSeries(
              name: 'Revenue',
              color: ChartTheme.primarySeries,
              values: [for (final entry in top) entry.totalSales],
              formatValue: ChartTheme.peso,
            ),
          ],
        );
      },
      footerBuilder: (context, report) {
        final sales = [...report.sales]
          ..sort((a, b) => b.totalSales.compareTo(a.totalSales));
        final leader = sales.isEmpty ? null : sales.first;
        if (leader == null) {
          return null;
        }
        return Text(
          '${leader.staffName} leads with ${leader.transactionCount} '
          'transaction${leader.transactionCount == 1 ? '' : 's'}.',
          style: AppTypography.bodySm,
        );
      },
    );
  }
}

String _firstName(String fullName) {
  final trimmed = fullName.trim();
  if (trimmed.isEmpty) {
    return '—';
  }
  return trimmed.split(RegExp(r'\s+')).first;
}

class _ShiftAttendanceChart extends ConsumerWidget {
  const _ShiftAttendanceChart({required this.range});

  final HomeTrendRange range;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final reportAsync = ref.watch(
      staffPerformanceProvider(fromDate: range.from, toDate: range.to),
    );

    return ChartAsyncCard(
      title: 'Shift Attendance',
      subtitle: 'Shifts opened, and how many closed with a cash variance',
      height: 200,
      value: reportAsync,
      isEmpty: (report) => report.shiftAttendance.isEmpty,
      emptyIcon: Icons.schedule_rounded,
      emptyTitle: 'No shifts opened in this range.',
      emptyDescription:
          'Opening a drawer on the Shift screen records attendance here.',
      onRetry:
          () => ref.invalidate(
            staffPerformanceProvider(fromDate: range.from, toDate: range.to),
          ),
      builder: (context, report) {
        final attendance = [...report.shiftAttendance]
          ..sort((a, b) => b.shiftsOpened.compareTo(a.shiftsOpened));
        final top = attendance.take(6).toList();

        return GroupedBarChart(
          labels: [for (final entry in top) _firstName(entry.staffName)],
          series: [
            ChartSeries(
              name: 'Shifts opened',
              color: ChartTheme.primarySeries,
              values: [for (final entry in top) entry.shiftsOpened.toDouble()],
            ),
            ChartSeries(
              name: 'With variance',
              color: AppColors.accentWarm,
              values: [
                for (final entry in top) entry.shiftsWithVariance.toDouble(),
              ],
            ),
          ],
        );
      },
      footerBuilder:
          (context, report) => const ChartLegend(
            entries: [
              ChartLegendEntry(
                label: 'Shifts opened',
                color: ChartTheme.primarySeries,
              ),
              ChartLegendEntry(
                label: 'Closed with variance',
                color: AppColors.accentWarm,
              ),
            ],
          ),
    );
  }
}

// ---------------------------------------------------------------------------
// 9 & 10. Sales mix
// ---------------------------------------------------------------------------

class _DepartmentBreakdownChart extends ConsumerWidget {
  const _DepartmentBreakdownChart({required this.range});

  final HomeTrendRange range;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final departmentsAsync = ref.watch(
      departmentSalesProvider(fromDate: range.from, toDate: range.to),
    );

    return ChartAsyncCard(
      title: 'Department Breakdown',
      subtitle: 'Revenue share · ${range.description}',
      height: 200,
      value: departmentsAsync,
      isEmpty: (departments) => departments.isEmpty,
      emptyIcon: Icons.pie_chart_outline_rounded,
      emptyTitle: 'No department revenue in this range.',
      emptyDescription:
          'Assign departments to items in the catalog to split revenue here.',
      onRetry:
          () => ref.invalidate(
            departmentSalesProvider(fromDate: range.from, toDate: range.to),
          ),
      builder: (context, departments) {
        final sorted = [...departments]
          ..sort((a, b) => b.revenue.compareTo(a.revenue));
        return ShareDonutChart(
          data: [
            for (final department in sorted.take(7))
              ChartDatum(
                label: department.departmentName,
                value: department.revenue,
              ),
          ],
        );
      },
    );
  }
}

class _CategorySalesChart extends ConsumerWidget {
  const _CategorySalesChart();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final categoriesAsync = ref.watch(categorySalesProvider());

    return ChartAsyncCard(
      title: 'Category Sales',
      subtitle: 'Top-selling items, grouped by catalog category',
      height: 200,
      value: categoriesAsync,
      isEmpty: (categories) => categories.isEmpty,
      emptyIcon: Icons.category_outlined,
      emptyTitle: 'No category revenue yet.',
      emptyDescription:
          'Assign categories to items in the catalog to split revenue here.',
      onRetry: () => ref.invalidate(categorySalesProvider),
      builder: (context, categories) {
        return ShareDonutChart(
          data: [
            for (final category in categories.take(7))
              ChartDatum(label: category.categoryName, value: category.revenue),
          ],
        );
      },
    );
  }
}
