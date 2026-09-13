import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../../core/widgets/purch_image.dart';
import '../../../catalog/domain/pricing_type.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../../catalog/presentation/screens/add_item_screen.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';
import 'cart_screen.dart';
import 'combo_customization_screen.dart';
import 'variant_picker_screen.dart';

/// D1 — the POS item grid. PricingType.unit items are addable directly;
/// PricingType.combo (D2) and PricingType.variantMatrix (D3) open
/// their own customization sheet.
class ItemGridScreen extends ConsumerWidget {
  const ItemGridScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final itemsAsync = ref.watch(itemListProvider);
    final cartAsync = ref.watch(cartNotifierProvider);
    final itemCount = cartAsync.valueOrNull?.itemCount ?? 0;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('New Sale'),
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 8),
            child: IconButton(
              onPressed:
                  () => Navigator.of(context).push<void>(
                    MaterialPageRoute(builder: (_) => const CartScreen()),
                  ),
              icon: Badge(
                label: Text(
                  '$itemCount',
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
                backgroundColor: AppColors.accentWarm,
                textColor: Colors.white,
                isLabelVisible: itemCount > 0,
                child: const Icon(Icons.shopping_cart_rounded),
              ),
              tooltip: 'View cart',
            ),
          ),
        ],
      ),
      body: itemsAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load items: $error',
          onRetry: () => ref.read(itemListProvider.notifier).refresh(),
        ),
        data: (items) {
          final activeItems = items.where((item) => item.isActive).toList();
          if (activeItems.isEmpty) {
            return EmptyStateView(
              icon: Icons.inventory_2_outlined,
              title: 'No active items to sell yet.',
              description:
                  'Items will appear here for checkout once they are created and set to active in the catalog.',
              actionLabel: 'Add Items',
              onAction: () => Navigator.of(context).push<void>(
                MaterialPageRoute(builder: (_) => const AddItemScreen()),
              ),
            );
          }

          return GridView.builder(
            padding: const EdgeInsets.all(16),
            gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: 3,
              mainAxisSpacing: 14,
              crossAxisSpacing: 14,
              childAspectRatio: 1.15,
            ),
            itemCount: activeItems.length,
            itemBuilder: (context, index) {
              final item = activeItems[index];
              final isDirectlySellable = item.pricingType == PricingType.unit;
              final needsCustomization =
                  item.pricingType == PricingType.combo ||
                  item.pricingType == PricingType.variantMatrix;

              return Container(
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: AppRadius.mdBorder,
                  boxShadow: AppShadows.subtle,
                  border: Border.all(color: AppColors.border),
                ),
                clipBehavior: Clip.antiAlias,
                child: Material(
                  color: Colors.transparent,
                  child: InkWell(
                    onTap: () async {
                      if (item.pricingType == PricingType.variantMatrix) {
                        await Navigator.of(context).push<void>(
                          MaterialPageRoute(
                            builder: (_) => VariantPickerScreen(item: item),
                          ),
                        );
                        return;
                      }

                      if (item.pricingType == PricingType.combo) {
                        await Navigator.of(context).push<void>(
                          MaterialPageRoute(
                            builder: (_) => ComboCustomizationScreen(item: item),
                          ),
                        );
                        return;
                      }

                      if (!isDirectlySellable) {
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
                      padding: const EdgeInsets.all(12),
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Container(
                            width: 44,
                            height: 44,
                            decoration: BoxDecoration(
                              color:
                                  isDirectlySellable || needsCustomization
                                      ? AppColors.brandPrimaryContainer
                                      : AppColors.cardHover,
                              borderRadius: BorderRadius.circular(AppRadius.sm),
                            ),
                            child: item.imageUrl != null &&
                                    item.imageUrl!.isNotEmpty
                                ? PurchImage(
                                    imageUrlOrPath: item.imageUrl,
                                    width: 44,
                                    height: 44,
                                    borderRadius:
                                        BorderRadius.circular(AppRadius.sm),
                                    fit: BoxFit.cover,
                                  )
                                : Icon(
                                    isDirectlySellable || needsCustomization
                                        ? Icons.inventory_2_rounded
                                        : Icons.inventory_2_outlined,
                                    size: 22,
                                    color:
                                        isDirectlySellable || needsCustomization
                                            ? AppColors.brandPrimary
                                            : AppColors.textMuted,
                                  ),
                          ),
                          const SizedBox(height: 10),
                          Text(
                            item.name,
                            textAlign: TextAlign.center,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              fontSize: 14,
                              fontWeight: FontWeight.w600,
                              color: AppColors.textPrimary,
                              height: 1.2,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            '₱${item.basePrice.toStringAsFixed(2)}',
                            style: AppTypography.priceBadge,
                          ),
                        ],
                      ),
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
