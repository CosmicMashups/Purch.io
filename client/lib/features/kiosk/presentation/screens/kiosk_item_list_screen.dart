import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../catalog/domain/category_models.dart';
import '../../../catalog/domain/pricing_type.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../../pos/domain/transaction_models.dart';
import '../../../pos/presentation/screens/combo_customization_screen.dart';
import '../../../pos/presentation/screens/variant_picker_screen.dart';
import '../providers/kiosk_providers.dart';

/// Bridges E2 (category carousel) to E3 (item customize) — reuses the exact
/// same VariantPickerScreen/ComboCustomizationScreen D2/D3 logic the cashier
/// POS uses, just wired to add into the kiosk's own cart instead.
class KioskItemListScreen extends ConsumerWidget {
  const KioskItemListScreen({super.key, required this.category});

  final Category category;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final itemsAsync = ref.watch(itemListProvider);

    return Scaffold(
      appBar: AppBar(title: Text(category.name)),
      body: itemsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load items: $error')),
        data: (items) {
          final categoryItems =
              items
                  .where(
                    (item) => item.categoryId == category.id && item.isActive,
                  )
                  .toList();

          if (categoryItems.isEmpty) {
            return const Center(child: Text('Nothing in this category yet.'));
          }

          return GridView.builder(
            padding: const EdgeInsets.all(12),
            gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: 2,
              mainAxisSpacing: 12,
              crossAxisSpacing: 12,
              childAspectRatio: 1.1,
            ),
            itemCount: categoryItems.length,
            itemBuilder: (context, index) {
              final item = categoryItems[index];
              final isDirectlySellable = item.pricingType == PricingType.unit;

              return Card(
                child: InkWell(
                  onTap: () async {
                    if (item.pricingType == PricingType.variantMatrix) {
                      await Navigator.of(context).push<void>(
                        MaterialPageRoute(
                          builder:
                              (_) => VariantPickerScreen(
                                item: item,
                                addLine: _kioskAddLine(ref),
                              ),
                        ),
                      );
                      return;
                    }

                    if (item.pricingType == PricingType.combo) {
                      await Navigator.of(context).push<void>(
                        MaterialPageRoute(
                          builder:
                              (_) => ComboCustomizationScreen(
                                item: item,
                                addLine: _kioskAddLine(ref),
                              ),
                        ),
                      );
                      return;
                    }

                    if (!isDirectlySellable) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        SnackBar(
                          content: Text(
                            '${item.name} isn\'t available at the kiosk yet '
                            '— please order at the counter.',
                          ),
                        ),
                      );
                      return;
                    }

                    final controller = ref.read(
                      kioskCartNotifierProvider.notifier,
                    );
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
                        const Icon(Icons.fastfood, size: 40),
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

  Future<bool> Function(AddTransactionLineRequest) _kioskAddLine(
    WidgetRef ref,
  ) {
    return (request) =>
        ref.read(kioskCartNotifierProvider.notifier).addLine(request);
  }
}
