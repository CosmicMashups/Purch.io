import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/bir_reading_models.dart';
import '../providers/bir_reading_providers.dart';

/// F2/FR26 — generates a BIR X-reading (mid-shift, re-runnable any time) or
/// Z-reading (end-of-day, advances the reset counter — confirmed before
/// running since it can't be undone) for this device.
class BirReadingScreen extends ConsumerWidget {
  const BirReadingScreen({super.key});

  Future<void> _confirmAndGenerateZReading(
    BuildContext context,
    WidgetRef ref,
  ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder:
          (context) => AlertDialog(
            shape: const RoundedRectangleBorder(borderRadius: AppRadius.mdBorder),
            title: const Text('Generate Z-reading?'),
            content: const Text(
              'This closes out today\'s sales and advances the reset '
              'counter — it can\'t be undone. Only run this at end of day.',
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(context).pop(false),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: () => Navigator.of(context).pop(true),
                style: FilledButton.styleFrom(
                  backgroundColor: AppColors.error,
                  shape: const RoundedRectangleBorder(borderRadius: AppRadius.smBorder),
                ),
                child: const Text('Generate Z-Reading'),
              ),
            ],
          ),
    );

    if (confirmed == true) {
      await ref
          .read(generateBirReadingControllerProvider.notifier)
          .generateZReading();
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(generateBirReadingControllerProvider);
    final isLoading = state.isLoading;
    final failure =
        ref.read(generateBirReadingControllerProvider.notifier).currentFailure;
    final reading = state.valueOrNull;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('X / Z Reading'),
        backgroundColor: AppColors.surface,
        elevation: 0,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 500),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.xl),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton(
                          onPressed:
                              isLoading
                                  ? null
                                  : () =>
                                      ref
                                          .read(
                                            generateBirReadingControllerProvider
                                                .notifier,
                                          )
                                          .generateXReading(),
                          style: OutlinedButton.styleFrom(
                            padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
                            side: const BorderSide(color: AppColors.brandPrimary),
                            foregroundColor: AppColors.brandPrimary,
                            shape: const RoundedRectangleBorder(borderRadius: AppRadius.smBorder),
                          ),
                          child: const Text('X-Reading'),
                        ),
                      ),
                      const SizedBox(width: AppSpacing.md),
                      Expanded(
                        child: FilledButton(
                          onPressed:
                              isLoading
                                  ? null
                                  : () =>
                                      _confirmAndGenerateZReading(context, ref),
                          style: FilledButton.styleFrom(
                            padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
                            backgroundColor: AppColors.accentWarm,
                            foregroundColor: Colors.white,
                            shape: const RoundedRectangleBorder(borderRadius: AppRadius.smBorder),
                          ),
                          child: const Text('Z-Reading'),
                        ),
                      ),
                    ],
                  ),
                  if (failure != null) ...[
                    const SizedBox(height: AppSpacing.lg),
                    Container(
                      padding: const EdgeInsets.all(AppSpacing.md),
                      decoration: BoxDecoration(
                        color: AppColors.error.withValues(alpha: 0.08),
                        borderRadius: AppRadius.smBorder,
                        border: Border.all(color: AppColors.error.withValues(alpha: 0.2)),
                      ),
                      child: Text(
                        failure.message,
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: AppColors.error,
                        ),
                        textAlign: TextAlign.center,
                      ),
                    ),
                  ],
                  if (isLoading) ...[
                    const SizedBox(height: AppSpacing.xl),
                    const Center(child: CircularProgressIndicator()),
                  ],
                  if (reading != null) ...[
                    const SizedBox(height: AppSpacing.xl),
                    _ReadingReport(reading: reading),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _ReadingReport extends StatelessWidget {
  const _ReadingReport({required this.reading});

  final BirReading reading;

  @override
  Widget build(BuildContext context) {
    final isX = reading.type == BirReadingType.x;
    return Card(
      elevation: 0,
      color: AppColors.surface,
      shape: const RoundedRectangleBorder(
        borderRadius: AppRadius.lgBorder,
        side: BorderSide(color: AppColors.border),
      ),
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.xl),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  isX ? 'X-Reading' : 'Z-Reading',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: (isX ? AppColors.brandPrimary : AppColors.accentWarm).withValues(alpha: 0.12),
                    borderRadius: BorderRadius.circular(AppRadius.full),
                  ),
                  child: Text(
                    isX ? 'MID-SHIFT' : 'FINAL RESET',
                    style: Theme.of(context).textTheme.labelSmall?.copyWith(
                      fontWeight: FontWeight.w700,
                      color: isX ? AppColors.brandPrimary : AppColors.accentWarm,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              'MIN: ${reading.machineIdentificationNumber}',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.textMuted),
            ),
            Text(
              'Generated: ${reading.generatedAt.toLocal()}',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.textMuted),
            ),
            const Divider(height: 24),
            _Row(
              'OR# range',
              reading.beginningReceiptNumber == null
                  ? 'No transactions'
                  : '${reading.beginningReceiptNumber} - ${reading.endingReceiptNumber}',
            ),
            _Row('Transaction count', '${reading.transactionCount}'),
            const Divider(height: 24),
            _Row('Gross sales', '₱${reading.grossSales.toStringAsFixed(2)}'),
            _Row(
              'VATable sales',
              '₱${reading.vatableSales.toStringAsFixed(2)}',
            ),
            _Row('VAT (12%)', '₱${reading.vatAmount.toStringAsFixed(2)}'),
            _Row(
              'Senior/PWD discounts',
              '₱${reading.seniorPwdDiscountTotal.toStringAsFixed(2)}',
            ),
            _Row(
              'Promo discounts',
              '₱${reading.promoDiscountTotal.toStringAsFixed(2)}',
            ),
            _Row(
              'Total discounts',
              '₱${reading.totalDiscounts.toStringAsFixed(2)}',
            ),
            _Row(
              'Net sales',
              '₱${reading.netSales.toStringAsFixed(2)}',
              emphasize: true,
            ),
            const Divider(height: 24),
            _Row('Voided count', '${reading.voidedCount}'),
            _Row(
              'Voided amount',
              '₱${reading.voidedAmount.toStringAsFixed(2)}',
            ),
            const Divider(height: 24),
            _Row(
              'Old grand accumulated sales',
              '₱${reading.oldGrandAccumulatedSales.toStringAsFixed(2)}',
            ),
            _Row(
              'New grand accumulated sales',
              '₱${reading.newGrandAccumulatedSales.toStringAsFixed(2)}',
            ),
            _Row('Reset counter', '${reading.resetCounter}'),
          ],
        ),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row(this.label, this.value, {this.emphasize = false});

  final String label;
  final String value;
  final bool emphasize;

  @override
  Widget build(BuildContext context) {
    final style =
        emphasize
            ? Theme.of(
              context,
            ).textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w700,
              color: AppColors.brandPrimary,
              fontFeatures: const [FontFeature.tabularFigures()],
            )
            : Theme.of(context).textTheme.bodyMedium?.copyWith(
              fontFeatures: const [FontFeature.tabularFigures()],
            );

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Expanded(
            child: Text(
              label,
              style: emphasize
                  ? style?.copyWith(color: AppColors.textPrimary)
                  : Theme.of(context).textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary),
            ),
          ),
          const SizedBox(width: AppSpacing.sm),
          Text(value, style: style),
        ],
      ),
    );
  }
}
