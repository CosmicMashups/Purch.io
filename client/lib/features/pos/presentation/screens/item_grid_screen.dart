import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../catalog/domain/pricing_type.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';
import 'cart_screen.dart';

/// D1 — the POS item grid. Only PricingType.unit items are addable directly;
/// weight/volume, bundle, service, combo, and variant-matrix items need
/// their own customization sheets (D2/D3, tingi entry, etc.) which aren't
/// built yet, so tapping one just explains that instead of guessing a price.
class ItemGridScreen extends ConsumerWidget {
  const ItemGridScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final itemsAsync = ref.watch(itemListProvider);
    final cartAsync = ref.watch(cartNotifierProvider);
    final itemCount = cartAsync.valueOrNull?.itemCount ?? 0;

    return Scaffold(
      appBar: AppBar(
        title: const Text('New Sale'),
        actions: [
          IconButton(
            onPressed:
                () => Navigator.of(context).push<void>(
                  MaterialPageRoute(builder: (_) => const CartScreen()),
                ),
            icon: Badge(
              label: Text('$itemCount'),
              isLabelVisible: itemCount > 0,
              child: const Icon(Icons.shopping_cart),
            ),
            tooltip: 'View cart',
          ),
        ],
      ),
      body: itemsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load items: $error')),
        data: (items) {
          final activeItems = items.where((item) => item.isActive).toList();
          if (activeItems.isEmpty) {
            return const Center(child: Text('No active items to sell yet.'));
          }

          return GridView.builder(
            padding: const EdgeInsets.all(12),
            gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: 3,
              mainAxisSpacing: 12,
              crossAxisSpacing: 12,
              childAspectRatio: 1.1,
            ),
            itemCount: activeItems.length,
            itemBuilder: (context, index) {
              final item = activeItems[index];
              final isSellableHere = item.pricingType == PricingType.unit;

              return Card(
                child: InkWell(
                  onTap: () async {
                    if (!isSellableHere) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        SnackBar(
                          content: Text(
                            '${item.name} needs a customization step that '
                            'isn\'t built yet — only regular unit-priced '
                            'items can be added directly for now.',
                          ),
                        ),
                      );
                      return;
                    }

                    final controller = ref.read(cartNotifierProvider.notifier);
                    final succeeded = await controller.addLine(
                      AddTransactionLineRequest(itemId: item.id, quantity: 1),
                    );
                    if (succeeded && context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        SnackBar(content: Text('Added ${item.name}')),
                      );
                    }
                  },
                  child: Padding(
                    padding: const EdgeInsets.all(8),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(
                          isSellableHere
                              ? Icons.inventory_2
                              : Icons.inventory_2_outlined,
                          size: 32,
                        ),
                        const SizedBox(height: 8),
                        Text(
                          item.name,
                          textAlign: TextAlign.center,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                        ),
                        const SizedBox(height: 4),
                        Text('₱${item.basePrice.toStringAsFixed(2)}'),
                      ],
                    ),
                  ),
                ),
              );
            },
          );
        },
      ),
    );
  }
}
