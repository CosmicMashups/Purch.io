import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../inventory/domain/inventory_movement_models.dart';
import '../providers/reports_providers.dart';
import '../../../../core/errors/failure.dart';

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
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Inventory Reports'),
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
      body: ListView(
        padding: const EdgeInsets.all(AppSpacing.lg),
        children: [
          Text(
            'Movement Summary: ${_from.month}/${_from.day}/${_from.year} – '
            '${_to.month}/${_to.day}/${_to.year}',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          summaryAsync.when(
            loading: () => const Center(
              child: Padding(
                padding: EdgeInsets.all(AppSpacing.xl),
                child: CircularProgressIndicator(),
              ),
            ),
            error:
                (error, stackTrace) =>
                    Text('Could not load the movement summary: ${describeError(error)}', style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.error)),
            data: (summary) {
              final nonZeroRows =
                  summary.byType.where((row) => row.movementCount > 0).toList();
              if (nonZeroRows.isEmpty) {
                return Card(
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
                        'No stock movements in this range yet.',
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary),
                      ),
                    ),
                  ),
                );
              }
              return Card(
                elevation: 0,
                color: AppColors.surface,
                shape: const RoundedRectangleBorder(
                  borderRadius: AppRadius.mdBorder,
                  side: BorderSide(color: AppColors.border),
                ),
                child: Column(
                  children: [
                    for (var i = 0; i < nonZeroRows.length; i++) ...[
                      if (i > 0) const Divider(height: 1, indent: 16, endIndent: 16),
                      ListTile(
                        leading: Container(
                          padding: const EdgeInsets.all(8),
                          decoration: BoxDecoration(
                            color: AppColors.brandPrimaryContainer,
                            borderRadius: AppRadius.smBorder,
                          ),
                          child: const Icon(Icons.swap_vert, size: 18, color: AppColors.brandPrimary),
                        ),
                        title: Text(
                          nonZeroRows[i].type.label,
                          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                            fontWeight: FontWeight.w600,
                            color: AppColors.textPrimary,
                          ),
                        ),
                        subtitle: Text(
                          '${nonZeroRows[i].movementCount} movement(s)',
                          style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.textMuted),
                        ),
                        trailing: Text(
                          nonZeroRows[i].totalQuantity.toStringAsFixed(2),
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
              );
            },
          ),
          const SizedBox(height: AppSpacing.xl),
          Text(
            'Low-Stock / Reorder Export',
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
            child: Padding(
              padding: const EdgeInsets.all(AppSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Export current low-stock and reorder levels to a CSV file for suppliers or reporting.',
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary),
                  ),
                  const SizedBox(height: AppSpacing.md),
                  FilledButton.icon(
                    onPressed:
                        exportState.isLoading
                            ? null
                            : () =>
                                ref
                                    .read(lowStockExportControllerProvider.notifier)
                                    .generate(),
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.brandPrimary,
                      shape: const RoundedRectangleBorder(
                        borderRadius: AppRadius.smBorder,
                      ),
                    ),
                    icon: exportState.isLoading
                        ? const SizedBox(
                          height: 18,
                          width: 18,
                          child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                        )
                        : const Icon(Icons.file_download_outlined, size: 18),
                    label: const Text('Generate CSV'),
                  ),
                  if (exportState.hasValue && exportState.value != null) ...[
                    const SizedBox(height: AppSpacing.md),
                    const Divider(),
                    const SizedBox(height: AppSpacing.sm),
                    Row(
                      children: [
                        Expanded(
                          child: Text(
                            'Export ready — copy it below.',
                            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              color: AppColors.accentEmerald,
                              fontWeight: FontWeight.w600,
                            ),
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
                          icon: const Icon(Icons.copy, size: 16),
                          label: const Text('Copy'),
                          style: TextButton.styleFrom(
                            foregroundColor: AppColors.brandPrimary,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(AppSpacing.md),
                      decoration: BoxDecoration(
                        color: AppColors.background,
                        border: Border.all(color: AppColors.border),
                        borderRadius: AppRadius.smBorder,
                      ),
                      child: SelectableText(
                        exportState.value!,
                        style: const TextStyle(
                          fontFamily: 'monospace',
                          fontSize: 12,
                          color: AppColors.textPrimary,
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
