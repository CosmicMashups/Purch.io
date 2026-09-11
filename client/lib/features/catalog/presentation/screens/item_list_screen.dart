import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/pricing_type.dart';
import '../providers/catalog_providers.dart';
import 'add_item_screen.dart';
import 'bundle_rules_screen.dart';
import 'item_batches_screen.dart';
import 'item_modifier_groups_screen.dart';
import 'tingi_config_screen.dart';
import 'variants_screen.dart';

enum _ItemAction { batches, bundleRules, variants, customization, tingi }

/// B1's item catalog list. Every item can have modifier groups attached for
/// restaurant-style customization (B5, e.g. "No Ice"); pricing-type-specific
/// actions (weight/volume batches + tingi config (B2a), bundle rules (B2b),
/// variants (B3)) appear alongside it via a per-row menu.
class ItemListScreen extends ConsumerWidget {
  const ItemListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final itemsAsync = ref.watch(itemListProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Items')),
      body: itemsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load items: $error')),
        data: (items) {
          if (items.isEmpty) {
            return const Center(
              child: Text('No items yet — tap + to add your first one.'),
            );
          }

          return RefreshIndicator(
            onRefresh: () => ref.read(itemListProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: items.length,
              itemBuilder: (context, index) {
                final item = items[index];
                final isWeightVolume =
                    item.pricingType == PricingType.weightVolume;
                final isBundle = item.pricingType == PricingType.bundle;
                final isVariantMatrix =
                    item.pricingType == PricingType.variantMatrix;

                return ListTile(
                  leading: CircleAvatar(
                    child: Icon(
                      item.isActive
                          ? Icons.inventory_2
                          : Icons.inventory_2_outlined,
                    ),
                  ),
                  title: Text(item.name),
                  subtitle: Text(
                    isWeightVolume
                        ? '₱${item.basePrice.toStringAsFixed(2)} · ${item.stockOnHand} in stock'
                        : '₱${item.basePrice.toStringAsFixed(2)}',
                  ),
                  trailing: PopupMenuButton<_ItemAction>(
                    tooltip: 'Item actions',
                    onSelected: (action) {
                      switch (action) {
                        case _ItemAction.batches:
                          Navigator.of(context).push<void>(
                            MaterialPageRoute(
                              builder:
                                  (_) => ItemBatchesScreen(
                                    itemId: item.id,
                                    itemName: item.name,
                                  ),
                            ),
                          );
                          break;
                        case _ItemAction.bundleRules:
                          Navigator.of(context).push<void>(
                            MaterialPageRoute(
                              builder:
                                  (_) => BundleRulesScreen(
                                    itemId: item.id,
                                    itemName: item.name,
                                  ),
                            ),
                          );
                          break;
                        case _ItemAction.variants:
                          Navigator.of(context).push<void>(
                            MaterialPageRoute(
                              builder:
                                  (_) => VariantsScreen(
                                    itemId: item.id,
                                    itemName: item.name,
                                  ),
                            ),
                          );
                          break;
                        case _ItemAction.customization:
                          Navigator.of(context).push<void>(
                            MaterialPageRoute(
                              builder:
                                  (_) => ItemModifierGroupsScreen(
                                    itemId: item.id,
                                    itemName: item.name,
                                  ),
                            ),
                          );
                          break;
                        case _ItemAction.tingi:
                          Navigator.of(context).push<void>(
                            MaterialPageRoute(
                              builder: (_) => TingiConfigScreen(item: item),
                            ),
                          );
                          break;
                      }
                    },
                    itemBuilder:
                        (context) => [
                          if (isWeightVolume)
                            const PopupMenuItem(
                              value: _ItemAction.batches,
                              child: Text('Batches'),
                            ),
                          if (isWeightVolume)
                            const PopupMenuItem(
                              value: _ItemAction.tingi,
                              child: Text('Tingi selling'),
                            ),
                          if (isBundle)
                            const PopupMenuItem(
                              value: _ItemAction.bundleRules,
                              child: Text('Bundle rules'),
                            ),
                          if (isVariantMatrix)
                            const PopupMenuItem(
                              value: _ItemAction.variants,
                              child: Text('Variants'),
                            ),
                          const PopupMenuItem(
                            value: _ItemAction.customization,
                            child: Text('Customization'),
                          ),
                        ],
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
              MaterialPageRoute(builder: (_) => const AddItemScreen()),
            ),
        tooltip: 'Add item',
        child: const Icon(Icons.add),
      ),
    );
  }
}
