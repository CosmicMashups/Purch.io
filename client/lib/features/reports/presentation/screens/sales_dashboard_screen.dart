import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../providers/reports_providers.dart';

/// F1 — revenue summary cards, a trend list, top-selling items, and a
/// branch comparison view (only shows more than one row for a Tenant-scoped
/// caller with multiple branches — see IReportScopeResolver on the backend).
class SalesDashboardScreen extends ConsumerWidget {
  const SalesDashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final dashboardAsync = ref.watch(salesDashboardProvider());

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Sales Dashboard'),
        backgroundColor: AppColors.surface,
        elevation: 0,
      ),
      body: dashboardAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) => ErrorStateView(
              message: error.toString(),
              onRetry: () => ref.invalidate(salesDashboardProvider),
            ),
        data: (dashboard) {
          return RefreshIndicator(
            onRefresh: () async => ref.invalidate(salesDashboardProvider),
            child: ListView(
              padding: const EdgeInsets.all(AppSpacing.lg),
              children: [
                Row(
                  children: [
                    Expanded(
                      child: _RevenueCard(
                        label: 'Today',
                        amount: dashboard.revenueToday,
                        isPrimary: true,
                      ),
                    ),
                    const SizedBox(width: AppSpacing.md),
                    Expanded(
                      child: _RevenueCard(
                        label: 'Last 7 Days',
                        amount: dashboard.revenueLast7Days,
                      ),
                    ),
                    const SizedBox(width: AppSpacing.md),
                    Expanded(
                      child: _RevenueCard(
                        label: 'Last 30 Days',
                        amount: dashboard.revenueLast30Days,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: AppSpacing.xl),
                Text(
                  'Trend (last 14 days)',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: AppSpacing.sm),
                SizedBox(
                  height: 96,
                  child: ListView.separated(
                    scrollDirection: Axis.horizontal,
                    itemCount: dashboard.trend.length,
                    separatorBuilder:
                        (context, index) => const SizedBox(width: AppSpacing.sm),
                    itemBuilder: (context, index) {
                      final point = dashboard.trend[index];
                      return SizedBox(
                        width: 72,
                        child: Card(
                          elevation: 0,
                          color: AppColors.surface,
                          shape: const RoundedRectangleBorder(
                            borderRadius: AppRadius.smBorder,
                            side: BorderSide(color: AppColors.border),
                          ),
                          child: Padding(
                            padding: const EdgeInsets.symmetric(horizontal: AppSpacing.xs, vertical: AppSpacing.sm),
                            child: Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                Text(
                                  '${point.date.month}/${point.date.day}',
                                  style: Theme.of(context).textTheme.labelSmall?.copyWith(
                                    color: AppColors.textMuted,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                                const SizedBox(height: 6),
                                Text(
                                  '₱${point.revenue.toStringAsFixed(0)}',
                                  textAlign: TextAlign.center,
                                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                                    fontWeight: FontWeight.w700,
                                    color: AppColors.brandPrimary,
                                    fontFeatures: const [FontFeature.tabularFigures()],
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                      );
                    },
                  ),
                ),
                const SizedBox(height: AppSpacing.xl),
                Text(
                  'Top-Selling Items',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: AppSpacing.xs),
                if (dashboard.topSellingItems.isEmpty)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
                    child: Text(
                      'No sales in the last 30 days yet.',
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary),
                    ),
                  )
                else
                  Card(
                    elevation: 0,
                    color: AppColors.surface,
                    shape: const RoundedRectangleBorder(
                      borderRadius: AppRadius.mdBorder,
                      side: BorderSide(color: AppColors.border),
                    ),
                    child: Column(
                      children: [
                        for (var i = 0; i < dashboard.topSellingItems.length; i++) ...[
                          if (i > 0) const Divider(height: 1, indent: 16, endIndent: 16),
                          ListTile(
                            leading: Container(
                              width: 32,
                              height: 32,
                              decoration: BoxDecoration(
                                color: i == 0 ? AppColors.accentWarmContainer : AppColors.cardHover,
                                shape: BoxShape.circle,
                              ),
                              alignment: Alignment.center,
                              child: Text(
                                '${i + 1}',
                                style: Theme.of(context).textTheme.labelSmall?.copyWith(
                                  fontWeight: FontWeight.w700,
                                  color: i == 0 ? AppColors.accentWarm : AppColors.textSecondary,
                                ),
                              ),
                            ),
                            title: Text(
                              dashboard.topSellingItems[i].itemName,
                              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                fontWeight: FontWeight.w600,
                                color: AppColors.textPrimary,
                              ),
                            ),
                            subtitle: Text(
                              '${dashboard.topSellingItems[i].quantitySold.toStringAsFixed(0)} sold',
                              style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.textMuted),
                            ),
                            trailing: Text(
                              '₱${dashboard.topSellingItems[i].revenue.toStringAsFixed(2)}',
                              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                fontWeight: FontWeight.w700,
                                color: AppColors.brandPrimary,
                                fontFeatures: const [FontFeature.tabularFigures()],
                              ),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                const SizedBox(height: AppSpacing.xl),
                Text(
                  'Branch Comparison',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: AppSpacing.xs),
                if (dashboard.branchComparison.isEmpty)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
                    child: Text(
                      'No branch data yet.',
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary),
                    ),
                  )
                else
                  Card(
                    elevation: 0,
                    color: AppColors.surface,
                    shape: const RoundedRectangleBorder(
                      borderRadius: AppRadius.mdBorder,
                      side: BorderSide(color: AppColors.border),
                    ),
                    child: Column(
                      children: [
                        for (var i = 0; i < dashboard.branchComparison.length; i++) ...[
                          if (i > 0) const Divider(height: 1, indent: 16, endIndent: 16),
                          ListTile(
                            leading: const Icon(Icons.storefront_outlined, size: 20, color: AppColors.brandPrimary),
                            title: Text(
                              dashboard.branchComparison[i].branchName,
                              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                fontWeight: FontWeight.w600,
                                color: AppColors.textPrimary,
                              ),
                            ),
                            trailing: Text(
                              '₱${dashboard.branchComparison[i].revenue.toStringAsFixed(2)}',
                              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                                fontWeight: FontWeight.w700,
                                color: AppColors.textPrimary,
                                fontFeatures: const [FontFeature.tabularFigures()],
                              ),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class _RevenueCard extends StatelessWidget {
  const _RevenueCard({
    required this.label,
    required this.amount,
    this.isPrimary = false,
  });

  final String label;
  final double amount;
  final bool isPrimary;

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 0,
      color: isPrimary ? AppColors.brandPrimaryContainer : AppColors.surface,
      shape: RoundedRectangleBorder(
        borderRadius: AppRadius.mdBorder,
        side: BorderSide(
          color: isPrimary ? AppColors.brandPrimary.withValues(alpha: 0.25) : AppColors.border,
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              label,
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: isPrimary ? AppColors.brandPrimary : AppColors.textMuted,
                fontWeight: FontWeight.w600,
              ),
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              '₱${amount.toStringAsFixed(2)}',
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.w700,
                color: isPrimary ? AppColors.brandPrimary : AppColors.textPrimary,
                fontFeatures: const [FontFeature.tabularFigures()],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
