import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../pos/domain/transaction_models.dart';
import '../providers/kiosk_providers.dart';
import 'kiosk_fulfillment_screen.dart';

/// Order review before E4's fulfillment choice — not its own lettered PAGES.md
/// step, but a necessary bridge: the customer needs to see what they've
/// picked and adjust quantities before moving on. No discount/promo/payment
/// controls here at all — those don't exist on the kiosk's cart engine.
class KioskCartScreen extends ConsumerWidget {
  const KioskCartScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cartAsync = ref.watch(kioskCartNotifierProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Your Order')),
      body: cartAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load your order: $error')),
        data: (cart) {
          if (cart.lines.isEmpty) {
            return const Center(
              child: Text('Your order is empty — go back and add something.'),
            );
          }

          return Column(
            children: [
              Expanded(
                child: ListView.builder(
                  itemCount: cart.lines.length,
                  itemBuilder: (context, index) {
                    final line = cart.lines[index];
                    return ListTile(
                      title: Text(line.itemName),
                      subtitle: Text(
                        '${line.quantity.toStringAsFixed(0)} × ₱${line.unitPrice.toStringAsFixed(2)}',
                      ),
                      trailing: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text('₱${line.lineTotal.toStringAsFixed(2)}'),
                          IconButton(
                            icon: const Icon(Icons.delete_outline),
                            tooltip: 'Remove',
                            onPressed:
                                () => ref
                                    .read(kioskCartNotifierProvider.notifier)
                                    .removeLine(line.id),
                          ),
                        ],
                      ),
                    );
                  },
                ),
              ),
              _TotalBar(cart: cart),
            ],
          );
        },
      ),
    );
  }
}

class _TotalBar extends StatelessWidget {
  const _TotalBar({required this.cart});

  final Transaction cart;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('Total', style: TextStyle(fontSize: 18)),
                Text(
                  '₱${cart.totalAmount.toStringAsFixed(2)}',
                  style: const TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            SizedBox(
              height: 56,
              child: FilledButton(
                onPressed:
                    cart.lines.isEmpty
                        ? null
                        : () => Navigator.of(context).push<void>(
                          MaterialPageRoute(
                            builder: (_) => const KioskFulfillmentScreen(),
                          ),
                        ),
                child: const Text('Continue'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
