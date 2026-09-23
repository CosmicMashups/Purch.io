import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/hardware/barcode_scanner_screen.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/formatting/money.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../../core/widgets/skeleton_loader.dart';
import '../../../../core/widgets/status_badge.dart';
import '../../domain/item_models.dart';
import '../../domain/pricing_type.dart';
import '../providers/catalog_providers.dart';
import 'add_item_screen.dart';
import 'assign_department_screen.dart';
import 'bundle_rules_screen.dart';
import 'combo_components_screen.dart';
import 'edit_item_screen.dart';
import 'item_batches_screen.dart';
import 'item_modifier_groups_screen.dart';
import 'low_stock_threshold_screen.dart';
import 'service_duration_screen.dart';
import 'tingi_config_screen.dart';
import 'variants_screen.dart';
import '../../../../core/errors/failure.dart';

enum _ItemAction {
  edit,
  batches,
  bundleRules,
  variants,
  customization,
  tingi,
  serviceDuration,
  comboComponents,
  department,
  lowStockThreshold,
}

/// B1's item catalog list.
class ItemListScreen extends ConsumerWidget {
  const ItemListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final itemsAsync = ref.watch(itemListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Items'),
        actions: [
          IconButton(
            onPressed: () => _scanAndLookUp(context, ref),
            icon: const Icon(Icons.qr_code_scanner),
            tooltip: 'Scan barcode',
          ),
        ],
      ),
      body: itemsAsync.when(
        loading: () => const ItemListSkeleton(),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load items: ${describeError(error)}',
          onRetry: () => ref.read(itemListProvider.notifier).refresh(),
        ),
        data: (items) {
          if (items.isEmpty) {
            return EmptyStateView(
              icon: Icons.inventory_2_outlined,
              title: 'No items yet — tap + to add your first one.',
              description:
                  'Build your catalog with unit prices, combos, tingi portions, or variants.',
              actionLabel: 'Add Item',
              onAction: () => Navigator.of(context).push<void>(
                MaterialPageRoute(builder: (_) => const AddItemScreen()),
              ),
            );
          }

          return RefreshIndicator(
            onRefresh: () => ref.read(itemListProvider.notifier).refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
              itemCount: items.length,
              separatorBuilder: (_, __) => const SizedBox(height: 8),
              itemBuilder: (context, index) {
                final item = items[index];
                final isWeightVolume =
                    item.pricingType == PricingType.weightVolume;
                final isBundle = item.pricingType == PricingType.bundle;
                final isVariantMatrix =
                    item.pricingType == PricingType.variantMatrix;
                final isService = item.pricingType == PricingType.service;
                final isCombo = item.pricingType == PricingType.combo;

                return Container(
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: AppRadius.mdBorder,
                    boxShadow: AppShadows.subtle,
                    border: Border.all(color: AppColors.border),
                  ),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 6,
                    ),
                    onTap: () => Navigator.of(context).push<void>(
                      MaterialPageRoute(
                        builder: (_) => EditItemScreen(item: item),
                      ),
                    ),
                    leading: Container(
                      width: 44,
                      height: 44,
                      decoration: BoxDecoration(
                        color:
                            item.isActive
                                ? AppColors.brandPrimaryContainer
                                : AppColors.neutralContainer,
                        borderRadius: BorderRadius.circular(AppRadius.sm),
                        border: Border.all(
                          color: item.isActive
                              ? AppColors.brandPrimary.withValues(alpha: 0.2)
                              : AppColors.neutralBorder,
                        ),
                      ),
                      child: Icon(
                        item.isActive
                            ? Icons.inventory_2_rounded
                            : Icons.inventory_2_outlined,
                        color:
                            item.isActive
                                ? AppColors.brandPrimary
                                : AppColors.textMuted,
                        size: 22,
                      ),
                    ),
                    title: Text(
                      item.name,
                      style: const TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    subtitle: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const SizedBox(height: 2),
                        Text(
                          isWeightVolume
                              ? '${formatCurrency(item.basePrice)} · ${item.stockOnHand} in stock'
                              : '${formatCurrency(item.basePrice)}${item.sku != null && item.sku!.isNotEmpty ? ' · SKU: ${item.sku}' : ''}',
                          style: const TextStyle(
                            fontSize: 13,
                            fontWeight: FontWeight.w600,
                            color: AppColors.textSecondary,
                            fontFeatures: [FontFeature.tabularFigures()],
                          ),
                        ),
                        const SizedBox(height: 6),
                        Wrap(
                          spacing: 6,
                          runSpacing: 4,
                          children: [
                            item.isActive
                                ? StatusBadge.active(isSmall: true)
                                : StatusBadge.inactive(isSmall: true),
                            StatusBadge.stockLevel(
                              stockOnHand: item.stockOnHand,
                              lowStockThreshold: item.lowStockThreshold,
                              isSmall: true,
                            ),
                            StatusBadge(
                              label: item.pricingType.label,
                              type: StatusBadgeType.info,
                              isSmall: true,
                            ),
                          ],
                        ),
                      ],
                    ),
                    trailing: PopupMenuButton<_ItemAction>(
                      icon: const Icon(
                        Icons.more_vert_rounded,
                        color: AppColors.textSecondary,
                      ),
                      tooltip: 'Item actions',
                      onSelected: (action) {
                        switch (action) {
                          case _ItemAction.edit:
                            Navigator.of(context).push<void>(
                              MaterialPageRoute(
                                builder: (_) => EditItemScreen(item: item),
                              ),
                            );
                            break;
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
                          case _ItemAction.serviceDuration:
                            Navigator.of(context).push<void>(
                              MaterialPageRoute(
                                builder:
                                    (_) => ServiceDurationScreen(item: item),
                              ),
                            );
                            break;
                          case _ItemAction.comboComponents:
                            Navigator.of(context).push<void>(
                              MaterialPageRoute(
                                builder:
                                    (_) => ComboComponentsScreen(
                                      itemId: item.id,
                                      itemName: item.name,
                                    ),
                              ),
                            );
                            break;
                          case _ItemAction.department:
                            Navigator.of(context).push<void>(
                              MaterialPageRoute(
                                builder:
                                    (_) => AssignDepartmentScreen(item: item),
                              ),
                            );
                            break;
                          case _ItemAction.lowStockThreshold:
                            Navigator.of(context).push<void>(
                              MaterialPageRoute(
                                builder:
                                    (_) => LowStockThresholdScreen(item: item),
                              ),
                            );
                            break;
                        }
                      },
                      itemBuilder:
                          (context) => [
                            const PopupMenuItem(
                              value: _ItemAction.edit,
                              child: Row(
                                children: [
                                  Icon(
                                    Icons.edit_outlined,
                                    size: 18,
                                    color: AppColors.brandPrimary,
                                  ),
                                  SizedBox(width: 8),
                                  Text('Edit details'),
                                ],
                              ),
                            ),
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
                            if (isService)
                              const PopupMenuItem(
                                value: _ItemAction.serviceDuration,
                                child: Text('Service duration'),
                              ),
                            if (isCombo)
                              const PopupMenuItem(
                                value: _ItemAction.comboComponents,
                                child: Text('Combo slots'),
                              ),
                            const PopupMenuItem(
                              value: _ItemAction.customization,
                              child: Text('Customization'),
                            ),
                            const PopupMenuItem(
                              value: _ItemAction.department,
                              child: Text('Assign department'),
                            ),
                            const PopupMenuItem(
                              value: _ItemAction.lowStockThreshold,
                              child: Text('Low-stock threshold'),
                            ),
                          ],
                    ),
                  ),
                );
              },
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: AppColors.onBrandPrimary,
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

/// Scans a barcode and looks it up against the already-loaded item list —
/// no separate lookup endpoint needed since the list already carries each
/// item's barcode.
Future<void> _scanAndLookUp(BuildContext context, WidgetRef ref) async {
  final scanned = await Navigator.of(context).push<String>(
    MaterialPageRoute(builder: (_) => const BarcodeScannerScreen()),
  );
  if (scanned == null || !context.mounted) {
    return;
  }

  final items = ref.read(itemListProvider).valueOrNull ?? [];
  Item? match;
  for (final item in items) {
    if (item.barcode == scanned) {
      match = item;
      break;
    }
  }

  ScaffoldMessenger.of(context).showSnackBar(
    SnackBar(
      content: Text(
        match != null
            ? 'Found: ${match.name} (${formatCurrency(match.basePrice)})'
            : 'No item found with barcode "$scanned".',
      ),
    ),
  );
}
