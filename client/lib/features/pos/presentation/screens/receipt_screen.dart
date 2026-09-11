import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/pos_providers.dart';
import 'item_grid_screen.dart';

/// D6, minimal slice — shows the just-completed sale's sequential BIR
/// receipt number and a summary. No printing or Z/X-reading formatting yet;
/// this only exists so a cashier has confirmation the sale went through and
/// a way to start the next one.
class ReceiptScreen extends ConsumerWidget {
  const ReceiptScreen({super.key});

  Future<void> _startNewSale(BuildContext context, WidgetRef ref) async {
    await ref.read(cartNotifierProvider.notifier).startNewSale();
    if (!context.mounted) {
      return;
    }
    await Navigator.of(context).pushAndRemoveUntil<void>(
      MaterialPageRoute(builder: (_) => const ItemGridScreen()),
      (route) => route.isFirst,
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cart = ref.watch(cartNotifierProvider).valueOrNull;

    return PopScope(
      canPop: false,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Receipt'),
          automaticallyImplyLeading: false,
        ),
        body: SafeArea(
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child:
                  cart == null
                      ? const CircularProgressIndicator()
                      : SingleChildScrollView(
                        padding: const EdgeInsets.all(24),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            const Icon(
                              Icons.check_circle,
                              color: Colors.green,
                              size: 64,
                            ),
                            const SizedBox(height: 16),
                            Text(
                              'Receipt No. ${cart.receiptNumber ?? '—'}',
                              style: Theme.of(context).textTheme.headlineSmall,
                              textAlign: TextAlign.center,
                            ),
                            const SizedBox(height: 24),
                            for (final line in cart.lines)
                              Padding(
                                padding: const EdgeInsets.symmetric(
                                  vertical: 4,
                                ),
                                child: Row(
                                  mainAxisAlignment:
                                      MainAxisAlignment.spaceBetween,
                                  children: [
                                    Expanded(
                                      child: Text(
                                        '${line.itemName} ×${line.quantity.toStringAsFixed(line.quantity.truncateToDouble() == line.quantity ? 0 : 2)}',
                                      ),
                                    ),
                                    Text(
                                      '₱${line.lineTotal.toStringAsFixed(2)}',
                                    ),
                                  ],
                                ),
                              ),
                            const Divider(),
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                const Text('Total'),
                                Text('₱${cart.totalAmount.toStringAsFixed(2)}'),
                              ],
                            ),
                            if (cart.payments.isNotEmpty) ...[
                              const SizedBox(height: 8),
                              for (final payment in cart.payments)
                                if (payment.changeGiven != null)
                                  Row(
                                    mainAxisAlignment:
                                        MainAxisAlignment.spaceBetween,
                                    children: [
                                      const Text('Change'),
                                      Text(
                                        '₱${payment.changeGiven!.toStringAsFixed(2)}',
                                      ),
                                    ],
                                  ),
                            ],
                            const SizedBox(height: 32),
                            SizedBox(
                              height: 56,
                              child: FilledButton(
                                onPressed: () => _startNewSale(context, ref),
                                child: const Text('New Sale'),
                              ),
                            ),
                          ],
                        ),
                      ),
            ),
          ),
        ),
      ),
    );
  }
}
