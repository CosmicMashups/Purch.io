import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../inventory/domain/inventory_movement_models.dart';
import '../providers/reports_providers.dart';

/// F3 — stock movement summary by type over a date range, plus the
/// low-stock/reorder CSV export.
class InventoryReportsScreen extends ConsumerStatefulWidget {
  const InventoryReportsScreen({super.key});

  @override
  ConsumerState<InventoryReportsScreen> createState() =>
      _InventoryReportsScreenState();
}

class _InventoryReportsScreenState
    extends ConsumerState<InventoryReportsScreen> {
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
    final summaryAsync = ref.watch(
      movementSummaryProvider(fromDate: _from, toDate: _to),
    );
    final exportState = ref.watch(lowStockExportControllerProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Inventory Reports'),
        actions: [
          IconButton(
            onPressed: _pickDateRange,
            icon: const Icon(Icons.date_range),
            tooltip: 'Change date range',
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Text(
            'Movement Summary: ${_from.month}/${_from.day}/${_from.year} – '
            '${_to.month}/${_to.day}/${_to.year}',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 8),
          summaryAsync.when(
            loading: () => const Center(child: CircularProgressIndicator()),
            error:
                (error, stackTrace) =>
                    Text('Could not load the movement summary: $error'),
            data: (summary) {
              final nonZeroRows =
                  summary.byType.where((row) => row.movementCount > 0).toList();
              if (nonZeroRows.isEmpty) {
                return const Text('No stock movements in this range yet.');
              }
              return Column(
                children: [
                  for (final row in nonZeroRows)
                    ListTile(
                      title: Text(row.type.label),
                      subtitle: Text('${row.movementCount} movement(s)'),
                      trailing: Text(row.totalQuantity.toStringAsFixed(2)),
                    ),
                ],
              );
            },
          ),
          const Divider(height: 32),
          Text(
            'Low-Stock / Reorder Export',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 8),
          FilledButton(
            onPressed:
                exportState.isLoading
                    ? null
                    : () =>
                        ref
                            .read(lowStockExportControllerProvider.notifier)
                            .generate(),
            child:
                exportState.isLoading
                    ? const SizedBox(
                      height: 20,
                      width: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                    : const Text('Generate CSV'),
          ),
          if (exportState.hasValue && exportState.value != null) ...[
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Export ready — copy it below.',
                    style: Theme.of(context).textTheme.bodyMedium,
                  ),
                ),
                TextButton.icon(
                  onPressed: () async {
                    await Clipboard.setData(
                      ClipboardData(text: exportState.value!),
                    );
                    if (context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(content: Text('Copied to clipboard')),
                      );
                    }
                  },
                  icon: const Icon(Icons.copy),
                  label: const Text('Copy'),
                ),
              ],
            ),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                border: Border.all(color: Theme.of(context).dividerColor),
                borderRadius: BorderRadius.circular(4),
              ),
              child: SelectableText(exportState.value!),
            ),
          ],
        ],
      ),
    );
  }
}
