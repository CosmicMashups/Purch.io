import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
      appBar: AppBar(title: const Text('X / Z Reading')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
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
                          child: const Text('X-Reading'),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: FilledButton(
                          onPressed:
                              isLoading
                                  ? null
                                  : () =>
                                      _confirmAndGenerateZReading(context, ref),
                          child: const Text('Z-Reading'),
                        ),
                      ),
                    ],
                  ),
                  if (failure != null) ...[
                    const SizedBox(height: 16),
                    Text(
                      failure.message,
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.error,
                      ),
                      textAlign: TextAlign.center,
                    ),
                  ],
                  if (isLoading) ...[
                    const SizedBox(height: 24),
                    const Center(child: CircularProgressIndicator()),
                  ],
                  if (reading != null) ...[
                    const SizedBox(height: 24),
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
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              reading.type == BirReadingType.x ? 'X-Reading' : 'Z-Reading',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            Text('MIN: ${reading.machineIdentificationNumber}'),
            Text('Generated: ${reading.generatedAt.toLocal()}'),
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
            ).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold)
            : Theme.of(context).textTheme.bodyMedium;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Expanded(child: Text(label, style: style)),
          const SizedBox(width: 8),
          Text(value, style: style),
        ],
      ),
    );
  }
}
