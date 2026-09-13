import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../providers/reports_providers.dart';

/// F4 — sales per cashier + shift attendance summary over a date range.
class StaffPerformanceScreen extends ConsumerStatefulWidget {
  const StaffPerformanceScreen({super.key});

  @override
  ConsumerState<StaffPerformanceScreen> createState() =>
      _StaffPerformanceScreenState();
}

class _StaffPerformanceScreenState
    extends ConsumerState<StaffPerformanceScreen> {
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
      staffPerformanceProvider(fromDate: _from, toDate: _to),
    );

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Staff Performance'),
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
            (error, stackTrace) =>
                Center(child: Text('Could not load the report: $error', style: Theme.of(context).textTheme.bodyMedium)),
        data: (report) {
          return ListView(
            padding: const EdgeInsets.all(AppSpacing.lg),
            children: [
              Text(
                'Sales per Cashier',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: AppSpacing.sm),
              if (report.sales.isEmpty)
                Card(
                  elevation: 0,
                  color: AppColors.surface,
                  shape: const RoundedRectangleBorder(
                    borderRadius: AppRadius.mdBorder,
                    side: BorderSide(color: AppColors.border),
                  ),
                  child: Padding(
                    padding: const EdgeInsets.all(AppSpacing.xl),
                    child: Center(
                      child: Text(
                        'No completed sales in this range yet.',
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary),
                      ),
                    ),
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
                      for (var i = 0; i < report.sales.length; i++) ...[
                        if (i > 0) const Divider(height: 1, indent: 16, endIndent: 16),
                        ListTile(
                          leading: Container(
                            padding: const EdgeInsets.all(8),
                            decoration: const BoxDecoration(
                              color: AppColors.brandPrimaryContainer,
                              shape: BoxShape.circle,
                            ),
                            child: const Icon(Icons.person_outline, size: 18, color: AppColors.brandPrimary),
                          ),
                          title: Text(
                            report.sales[i].staffName,
                            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              fontWeight: FontWeight.w600,
                              color: AppColors.textPrimary,
                            ),
                          ),
                          subtitle: Text(
                            '${report.sales[i].transactionCount} sale(s)',
                            style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.textMuted),
                          ),
                          trailing: Text(
                            '₱${report.sales[i].totalSales.toStringAsFixed(2)}',
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
                'Shift Attendance',
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: AppSpacing.sm),
              if (report.shiftAttendance.isEmpty)
                Card(
                  elevation: 0,
                  color: AppColors.surface,
                  shape: const RoundedRectangleBorder(
                    borderRadius: AppRadius.mdBorder,
                    side: BorderSide(color: AppColors.border),
                  ),
                  child: Padding(
                    padding: const EdgeInsets.all(AppSpacing.xl),
                    child: Center(
                      child: Text(
                        'No closed shifts in this range yet.',
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary),
                      ),
                    ),
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
                      for (var i = 0; i < report.shiftAttendance.length; i++) ...[
                        if (i > 0) const Divider(height: 1, indent: 16, endIndent: 16),
                        ListTile(
                          leading: Container(
                            padding: const EdgeInsets.all(8),
                            decoration: BoxDecoration(
                              color: report.shiftAttendance[i].shiftsWithVariance > 0
                                  ? AppColors.error.withValues(alpha: 0.1)
                                  : AppColors.accentEmeraldContainer,
                              shape: BoxShape.circle,
                            ),
                            child: Icon(
                              report.shiftAttendance[i].shiftsWithVariance > 0
                                  ? Icons.warning_amber_rounded
                                  : Icons.check,
                              size: 18,
                              color: report.shiftAttendance[i].shiftsWithVariance > 0
                                  ? AppColors.error
                                  : AppColors.accentEmerald,
                            ),
                          ),
                          title: Text(
                            report.shiftAttendance[i].staffName,
                            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              fontWeight: FontWeight.w600,
                              color: AppColors.textPrimary,
                            ),
                          ),
                          subtitle: Text(
                            '${report.shiftAttendance[i].shiftsOpened} shift(s) opened',
                            style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.textMuted),
                          ),
                          trailing:
                              report.shiftAttendance[i].shiftsWithVariance > 0
                                  ? Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                                    decoration: BoxDecoration(
                                      color: AppColors.error.withValues(alpha: 0.1),
                                      borderRadius: AppRadius.smBorder,
                                    ),
                                    child: Text(
                                      '${report.shiftAttendance[i].shiftsWithVariance} with variance',
                                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                                        color: AppColors.error,
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                  )
                                  : const Icon(
                                    Icons.check_circle,
                                    color: AppColors.accentEmerald,
                                  ),
                        ),
                      ],
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
