import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../providers/reports_providers.dart';

/// B6 — split sales-attribution report between concessionaire departments
/// and general (no-department) items, over a date range.
class DepartmentSalesScreen extends ConsumerStatefulWidget {
  const DepartmentSalesScreen({super.key});

  @override
  ConsumerState<DepartmentSalesScreen> createState() =>
      _DepartmentSalesScreenState();
}

class _DepartmentSalesScreenState extends ConsumerState<DepartmentSalesScreen> {
  late DateTime _from;
  late DateTime _to;

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _from = now.subtract(const Duration(days: 30));
    _to = now;
  }

  Future<void> _pickDateRange() async {
    final range = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2020),
      lastDate: DateTime.now(),
      initialDateRange: DateTimeRange(start: _from, end: _to),
    );
    if (range != null) {
      setState(() {
        _from = range.start;
        _to = range.end.add(const Duration(days: 1));
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final reportAsync = ref.watch(
      departmentSalesProvider(fromDate: _from, toDate: _to),
    );

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Department Sales'),
        backgroundColor: AppColors.surface,
        elevation: 0,
        actions: [
          IconButton(
            onPressed: _pickDateRange,
            icon: const Icon(Icons.date_range_outlined),
            tooltip: 'Change date range',
          ),
        ],
      ),
      body: reportAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) => ErrorStateView(
              message: error.toString(),
              onRetry: () => ref.refresh(
                departmentSalesProvider(fromDate: _from, toDate: _to),
              ),
            ),
        data: (rows) {
          if (rows.isEmpty) {
            return EmptyStateView(
              icon: Icons.receipt_long_outlined,
              title: 'No sales in this range yet.',
              description:
                  'Try selecting a different date range or complete sales transactions attributed to departments.',
              actionLabel: 'Change Date Range',
              onAction: _pickDateRange,
            );
          }

          final total = rows.fold<double>(0, (sum, row) => sum + row.revenue);

          return ListView(
            padding: const EdgeInsets.all(AppSpacing.lg),
            children: [
              Card(
                elevation: 0,
                color: AppColors.brandPrimaryContainer,
                shape: const RoundedRectangleBorder(
                  borderRadius: AppRadius.mdBorder,
                  side: BorderSide(color: AppColors.border),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.lg),
                  child: Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.all(AppSpacing.md),
                        decoration: const BoxDecoration(
                          color: Colors.white,
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(Icons.pie_chart_outline, color: AppColors.brandPrimary),
                      ),
                      const SizedBox(width: AppSpacing.md),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Department Attribution Summary',
                            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                              color: AppColors.brandPrimary,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 2),
                          Text(
                            '${rows.length} department categories tracked across selected range',
                            style: Theme.of(context).textTheme.bodySmall?.copyWith(
                              color: AppColors.textSecondary,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: AppSpacing.lg),
              Text(
                'Department Breakdown',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: AppSpacing.sm),
              Card(
                elevation: 0,
                color: AppColors.surface,
                shape: const RoundedRectangleBorder(
                  borderRadius: AppRadius.mdBorder,
                  side: BorderSide(color: AppColors.border),
                ),
                child: Column(
                  children: [
                    for (var i = 0; i < rows.length; i++) ...[
                      if (i > 0) const Divider(height: 1, indent: 16, endIndent: 16),
                      ListTile(
                        leading: Container(
                          padding: const EdgeInsets.all(8),
                          decoration: BoxDecoration(
                            color: rows[i].departmentId != null ? AppColors.brandPrimaryContainer : AppColors.cardHover,
                            borderRadius: AppRadius.smBorder,
                          ),
                          child: Icon(
                            rows[i].departmentId != null ? Icons.store_outlined : Icons.inventory_2_outlined,
                            size: 18,
                            color: rows[i].departmentId != null ? AppColors.brandPrimary : AppColors.textSecondary,
                          ),
                        ),
                        title: Text(
                          rows[i].departmentName,
                          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                            fontWeight: FontWeight.w600,
                            color: AppColors.textPrimary,
                          ),
                        ),
                        trailing: Text(
                          '₱${rows[i].revenue.toStringAsFixed(2)}',
                          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                            fontWeight: FontWeight.w700,
                            color: AppColors.textPrimary,
                            fontFeatures: const [FontFeature.tabularFigures()],
                          ),
                        ),
                      ),
                    ],
                    const Divider(height: 1),
                    ListTile(
                      title: const Text(
                        'Total',
                        style: TextStyle(fontWeight: FontWeight.bold),
                      ),
                      trailing: Text(
                        '₱${total.toStringAsFixed(2)}',
                        style: const TextStyle(
                          fontWeight: FontWeight.bold,
                          fontFeatures: [FontFeature.tabularFigures()],
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
