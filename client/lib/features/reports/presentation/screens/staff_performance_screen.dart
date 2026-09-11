import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
      appBar: AppBar(
        title: const Text('Staff Performance'),
        actions: [
          IconButton(
            onPressed: _pickDateRange,
            icon: const Icon(Icons.date_range),
            tooltip: 'Change date range',
          ),
        ],
      ),
      body: reportAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load the report: $error')),
        data: (report) {
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Text(
                'Sales per Cashier',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              if (report.sales.isEmpty)
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 8),
                  child: Text('No completed sales in this range yet.'),
                )
              else
                for (final row in report.sales)
                  ListTile(
                    title: Text(row.staffName),
                    subtitle: Text('${row.transactionCount} sale(s)'),
                    trailing: Text('₱${row.totalSales.toStringAsFixed(2)}'),
                  ),
              const Divider(height: 32),
              Text(
                'Shift Attendance',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              if (report.shiftAttendance.isEmpty)
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 8),
                  child: Text('No closed shifts in this range yet.'),
                )
              else
                for (final row in report.shiftAttendance)
                  ListTile(
                    title: Text(row.staffName),
                    subtitle: Text('${row.shiftsOpened} shift(s) opened'),
                    trailing:
                        row.shiftsWithVariance > 0
                            ? Text(
                              '${row.shiftsWithVariance} with variance',
                              style: TextStyle(
                                color: Theme.of(context).colorScheme.error,
                              ),
                            )
                            : const Icon(
                              Icons.check_circle,
                              color: Colors.green,
                            ),
                  ),
            ],
          );
        },
      ),
    );
  }
}
