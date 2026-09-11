import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';
import 'payment_screen.dart';

/// D1's cart review — quantity adjustment and line removal, plus a way into
/// D5's payment method tabs. Discount auto-recalculation and promo codes
/// aren't modeled yet.
class CartScreen extends ConsumerWidget {
  const CartScreen({super.key});

  Future<void> _confirmVoid(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder:
          (context) => AlertDialog(
            title: const Text('Void cart?'),
            content: const Text(
              'This clears every item in the current sale. This cannot be undone.',
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(context).pop(false),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: () => Navigator.of(context).pop(true),
                child: const Text('Void'),
              ),
            ],
          ),
    );

    if (confirmed == true) {
      await ref.read(cartNotifierProvider.notifier).voidCart();
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cartAsync = ref.watch(cartNotifierProvider);
    final failure = ref.read(cartNotifierProvider.notifier).currentFailure;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Cart'),
        actions: [
          IconButton(
            onPressed:
                cartAsync.valueOrNull == null
                    ? null
                    : () => _confirmVoid(context, ref),
            icon: const Icon(Icons.delete_sweep),
            tooltip: 'Void cart',
          ),
        ],
      ),
      body: cartAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load the cart: $error')),
        data: (cart) {
          if (cart.lines.isEmpty) {
            return const Center(
              child: Text('Cart is empty — go back and add an item.'),
            );
          }

          return Column(
            children: [
              if (failure != null)
                Padding(
                  padding: const EdgeInsets.all(12),
                  child: Text(
                    failure.message,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                    textAlign: TextAlign.center,
                  ),
                ),
              Expanded(
                child: ListView.builder(
                  itemCount: cart.lines.length,
                  itemBuilder: (context, index) {
                    final line = cart.lines[index];
                    return _CartLineTile(line: line);
                  },
                ),
              ),
              const Divider(height: 1),
              Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    _TotalsRow(label: 'Subtotal', amount: cart.subtotal),
                    if (cart.discountAmount > 0)
                      _TotalsRow(
                        label: 'Discount',
                        amount: -cart.discountAmount,
                      ),
                    const SizedBox(height: 4),
                    _TotalsRow(
                      label: 'Total',
                      amount: cart.totalAmount,
                      emphasize: true,
                    ),
                    const SizedBox(height: 16),
                    SizedBox(
                      height: 56,
                      child: FilledButton(
                        onPressed:
                            () => Navigator.of(context).push<void>(
                              MaterialPageRoute(
                                builder:
                                    (_) =>
                                        PaymentScreen(total: cart.totalAmount),
                              ),
                            ),
                        child: const Text('Pay'),
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

class _CartLineTile extends ConsumerWidget {
  const _CartLineTile({required this.line});

  final TransactionLine line;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final controller = ref.read(cartNotifierProvider.notifier);

    return ListTile(
      title: Text(line.itemName),
      subtitle: Text('₱${line.unitPrice.toStringAsFixed(2)} each'),
      leading: IconButton(
        onPressed: () => controller.removeLine(line.id),
        icon: const Icon(Icons.delete_outline),
        tooltip: 'Remove',
      ),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          IconButton(
            onPressed:
                line.quantity > 1
                    ? () => controller.updateLine(
                      line.id,
                      UpdateTransactionLineRequest(quantity: line.quantity - 1),
                    )
                    : null,
            icon: const Icon(Icons.remove_circle_outline),
          ),
          SizedBox(
            width: 32,
            child: Text(
              line.quantity.toStringAsFixed(
                line.quantity.truncateToDouble() == line.quantity ? 0 : 2,
              ),
              textAlign: TextAlign.center,
            ),
          ),
          IconButton(
            onPressed:
                () => controller.updateLine(
                  line.id,
                  UpdateTransactionLineRequest(quantity: line.quantity + 1),
                ),
            icon: const Icon(Icons.add_circle_outline),
          ),
          const SizedBox(width: 8),
          SizedBox(
            width: 64,
            child: Text(
              '₱${line.lineTotal.toStringAsFixed(2)}',
              textAlign: TextAlign.right,
            ),
          ),
        ],
      ),
    );
  }
}

class _TotalsRow extends StatelessWidget {
  const _TotalsRow({
    required this.label,
    required this.amount,
    this.emphasize = false,
  });

  final String label;
  final double amount;
  final bool emphasize;

  @override
  Widget build(BuildContext context) {
    final style =
        emphasize
            ? Theme.of(
              context,
            ).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.bold)
            : Theme.of(context).textTheme.bodyLarge;

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label, style: style),
        Text('₱${amount.toStringAsFixed(2)}', style: style),
      ],
    );
  }
}
