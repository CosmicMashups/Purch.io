import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';

/// D3 — lets the cashier pick which variant (size/color/etc.) of a
/// PricingType.variantMatrix item to add, since the backend prices and
/// tracks stock per variant rather than per item.
class VariantPickerScreen extends ConsumerWidget {
  const VariantPickerScreen({super.key, required this.item, this.addLine});

  final Item item;

  /// Overrides how an add-to-cart is performed — defaults to the POS cart
  /// (cartNotifierProvider) when omitted. The kiosk feature passes its own
  /// kioskCartNotifierProvider-backed callback to reuse this screen as-is.
  final Future<bool> Function(AddTransactionLineRequest)? addLine;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final variantsAsync = ref.watch(itemVariantListProvider(item.id));

    return Scaffold(
      appBar: AppBar(title: Text(item.name)),
      body: variantsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load variants: $error')),
        data: (variants) {
          if (variants.isEmpty) {
            return const Center(
              child: Text(
                'No variants have been configured for this item yet.',
              ),
            );
          }

          return ListView.builder(
            itemCount: variants.length,
            itemBuilder: (context, index) {
              final variant = variants[index];
              final price = variant.priceOverride ?? item.basePrice;

              return ListTile(
                title: Text(variant.attributesLabel),
                subtitle:
                    variant.sku != null ? Text('SKU: ${variant.sku}') : null,
                trailing: Text('₱${price.toStringAsFixed(2)}'),
                onTap: () async {
                  final add =
                      addLine ??
                      (request) => ref
                          .read(cartNotifierProvider.notifier)
                          .addLine(request);
                  final succeeded = await add(
                    AddTransactionLineRequest(
                      itemId: item.id,
                      itemVariantId: variant.id,
                      quantity: 1,
                    ),
                  );
                  if (succeeded && context.mounted) {
                    Navigator.of(context).pop();
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(
                        content: Text(
                          'Added ${item.name} (${variant.attributesLabel})',
                        ),
                      ),
                    );
                  }
                },
              );
            },
          );
        },
      ),
    );
  }
}
