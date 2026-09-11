import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers/catalog_providers.dart';
import 'add_variant_screen.dart';

/// B3 — variant matrix. Only reachable for items whose pricingType is
/// variantMatrix (see ItemListScreen), matching the backend's own rejection
/// of variants against any other pricing type.
class VariantsScreen extends ConsumerWidget {
  const VariantsScreen({
    super.key,
    required this.itemId,
    required this.itemName,
  });

  final String itemId;
  final String itemName;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final variantsAsync = ref.watch(itemVariantListProvider(itemId));

    return Scaffold(
      appBar: AppBar(title: Text('Variants: $itemName')),
      body: variantsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load variants: $error')),
        data: (variants) {
          if (variants.isEmpty) {
            return const Center(
              child: Text('No variants yet — tap + to add one.'),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () =>
                    ref
                        .read(itemVariantListProvider(itemId).notifier)
                        .refresh(),
            child: ListView.builder(
              itemCount: variants.length,
              itemBuilder: (context, index) {
                final variant = variants[index];
                return ListTile(
                  leading: const CircleAvatar(child: Icon(Icons.style)),
                  title: Text(variant.attributesLabel),
                  subtitle: Text(
                    [
                      if (variant.sku != null) 'SKU: ${variant.sku}',
                      '${variant.stockOnHand} in stock',
                      if (variant.priceOverride != null)
                        '₱${variant.priceOverride!.toStringAsFixed(2)}',
                    ].join(' · '),
                  ),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(
                builder:
                    (_) => AddVariantScreen(itemId: itemId, itemName: itemName),
              ),
            ),
        tooltip: 'Add variant',
        child: const Icon(Icons.add),
      ),
    );
  }
}
