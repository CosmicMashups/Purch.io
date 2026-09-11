import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
      appBar: AppBar(
        title: const Text('Department Sales'),
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
        data: (rows) {
          if (rows.isEmpty) {
            return const Center(child: Text('No sales in this range yet.'));
          }

          final total = rows.fold<double>(0, (sum, row) => sum + row.revenue);

          return ListView(
            children: [
              for (final row in rows)
                ListTile(
                  title: Text(row.departmentName),
                  trailing: Text('₱${row.revenue.toStringAsFixed(2)}'),
                ),
              const Divider(),
              ListTile(
                title: const Text(
                  'Total',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
                trailing: Text(
                  '₱${total.toStringAsFixed(2)}',
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
