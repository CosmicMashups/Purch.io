import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
      appBar: AppBar(title: const Text('Sales Dashboard')),
      body: dashboardAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load the dashboard: $error')),
        data: (dashboard) {
          return RefreshIndicator(
            onRefresh: () async => ref.invalidate(salesDashboardProvider),
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Row(
                  children: [
                    Expanded(
                      child: _RevenueCard(
                        label: 'Today',
                        amount: dashboard.revenueToday,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: _RevenueCard(
                        label: 'Last 7 Days',
                        amount: dashboard.revenueLast7Days,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: _RevenueCard(
                        label: 'Last 30 Days',
                        amount: dashboard.revenueLast30Days,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 24),
                Text(
                  'Trend (last 14 days)',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 8),
                SizedBox(
                  height: 120,
                  child: ListView.separated(
                    scrollDirection: Axis.horizontal,
                    itemCount: dashboard.trend.length,
                    separatorBuilder:
                        (context, index) => const SizedBox(width: 8),
                    itemBuilder: (context, index) {
                      final point = dashboard.trend[index];
                      return SizedBox(
                        width: 64,
                        child: Card(
                          child: Padding(
                            padding: const EdgeInsets.all(8),
                            child: Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                Text(
                                  '${point.date.month}/${point.date.day}',
                                  style: Theme.of(context).textTheme.labelSmall,
                                ),
                                const SizedBox(height: 4),
                                Text(
                                  '₱${point.revenue.toStringAsFixed(0)}',
                                  textAlign: TextAlign.center,
                                  style: Theme.of(context).textTheme.bodySmall,
                                ),
                              ],
                            ),
                          ),
                        ),
                      );
                    },
                  ),
                ),
                const SizedBox(height: 24),
                Text(
                  'Top-Selling Items',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                if (dashboard.topSellingItems.isEmpty)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text('No sales in the last 30 days yet.'),
                  )
                else
                  for (final item in dashboard.topSellingItems)
                    ListTile(
                      title: Text(item.itemName),
                      subtitle: Text(
                        '${item.quantitySold.toStringAsFixed(0)} sold',
                      ),
                      trailing: Text('₱${item.revenue.toStringAsFixed(2)}'),
                    ),
                const SizedBox(height: 24),
                Text(
                  'Branch Comparison',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                if (dashboard.branchComparison.isEmpty)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 8),
                    child: Text('No branch data yet.'),
                  )
                else
                  for (final branch in dashboard.branchComparison)
                    ListTile(
                      title: Text(branch.branchName),
                      trailing: Text('₱${branch.revenue.toStringAsFixed(2)}'),
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
  const _RevenueCard({required this.label, required this.amount});

  final String label;
  final double amount;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            Text(label, style: Theme.of(context).textTheme.labelMedium),
            const SizedBox(height: 8),
            Text(
              '₱${amount.toStringAsFixed(2)}',
              style: Theme.of(context).textTheme.titleMedium,
            ),
          ],
        ),
      ),
    );
  }
}
