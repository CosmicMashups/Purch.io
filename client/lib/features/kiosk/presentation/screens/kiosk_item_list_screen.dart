import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../../core/widgets/purch_image.dart';
import '../../../catalog/domain/category_models.dart';
import '../../../catalog/domain/pricing_type.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../../pos/domain/transaction_models.dart';
import '../../../pos/presentation/screens/combo_customization_screen.dart';
import '../../../pos/presentation/screens/variant_picker_screen.dart';
import '../providers/kiosk_providers.dart';
import 'kiosk_cart_screen.dart';

/// Bridges E2 (category carousel) to E3 (item customize) — reuses the exact
/// same VariantPickerScreen/ComboCustomizationScreen D2/D3 logic the cashier
/// POS uses, just wired to add into the kiosk's own cart instead.
class KioskItemListScreen extends ConsumerWidget {
  const KioskItemListScreen({super.key, required this.category});

  final Category category;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final itemsAsync = ref.watch(itemListProvider);
    final cartAsync = ref.watch(kioskCartNotifierProvider);
    final itemCount = cartAsync.valueOrNull?.itemCount ?? 0;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text(category.name),
        backgroundColor: AppColors.surface,
        centerTitle: true,
        elevation: 0,
        scrolledUnderElevation: 1,
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 12),
            child: IconButton(
              onPressed:
                  () => Navigator.of(context).push<void>(
                    MaterialPageRoute(builder: (_) => const KioskCartScreen()),
                  ),
              icon: Badge(
                backgroundColor: AppColors.accentWarm,
                textColor: Colors.white,
                label: Text(
                  '',
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
                isLabelVisible: itemCount > 0,
                child: Container(
                  padding: const EdgeInsets.all(8),
                  decoration: const BoxDecoration(
                    color: AppColors.brandPrimaryContainer,
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.shopping_bag_outlined,
                    color: AppColors.brandPrimary,
                    size: 24,
                  ),
                ),
              ),
              tooltip: 'View your order',
            ),
          ),
        ],
      ),
      body: SafeArea(
        child: itemsAsync.when(
          loading: () => const Center(
            child: CircularProgressIndicator(color: AppColors.brandPrimary),
          ),
          error: (error, stackTrace) => ErrorStateView(
            message: 'Could not load menu items: $error',
            onRetry: () => ref.read(itemListProvider.notifier).refresh(),
          ),
          data: (items) {
            final categoryItems =
                items
                    .where(
                      (item) => item.categoryId == category.id && item.isActive,
                    )
                    .toList();

            if (categoryItems.isEmpty) {
              return EmptyStateView(
                icon: Icons.restaurant_menu_outlined,
                title: 'Nothing in this category yet.',
                description:
                    'We are currently preparing fresh items for this section. Please check back shortly or explore other categories.',
                actionLabel: 'Explore Other Categories',
                onAction: () => Navigator.of(context).maybePop(),
              );
            }

            return GridView.builder(
              padding: const EdgeInsets.all(16),
              gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: 2,
                mainAxisSpacing: 16,
                crossAxisSpacing: 16,
                childAspectRatio: 0.85,
              ),
              itemCount: categoryItems.length,
              itemBuilder: (context, index) {
                final item = categoryItems[index];
                final isDirectlySellable = item.pricingType == PricingType.unit;

                String typeBadgeLabel;
                Color typeBadgeColor = AppColors.brandPrimary;
                Color typeBadgeBg = AppColors.brandPrimaryContainer;

                switch (item.pricingType) {
                  case PricingType.combo:
                    typeBadgeLabel = 'COMBO';
                    typeBadgeColor = AppColors.accentWarm;
                    typeBadgeBg = AppColors.accentWarmContainer;
                    break;
                  case PricingType.variantMatrix:
                    typeBadgeLabel = 'VARIANTS';
                    typeBadgeColor = AppColors.brandPrimary;
                    typeBadgeBg = AppColors.brandPrimaryContainer;
                    break;
                  case PricingType.unit:
                  default:
                    typeBadgeLabel = 'ITEM';
                    typeBadgeColor = AppColors.accentEmerald;
                    typeBadgeBg = AppColors.accentEmeraldContainer;
                }

                return Material(
                  color: AppColors.surface,
                  borderRadius: AppRadius.lgBorder,
                  child: InkWell(
                    borderRadius: AppRadius.lgBorder,
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
                            behavior: SnackBarBehavior.floating,
                            shape: const RoundedRectangleBorder(
                              borderRadius: AppRadius.mdBorder,
                            ),
                            content: Text(
                              ' isn\'t available at the kiosk yet '
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
                        ScaffoldMessenger.of(context).hideCurrentSnackBar();
                        ScaffoldMessenger.of(context).showSnackBar(
                          SnackBar(
                            behavior: SnackBarBehavior.floating,
                            backgroundColor: AppColors.textPrimary,
                            shape: const RoundedRectangleBorder(
                              borderRadius: AppRadius.mdBorder,
                            ),
                            duration: const Duration(milliseconds: 1400),
                            content: Row(
                              children: [
                                const Icon(
                                  Icons.check_circle_rounded,
                                  color: AppColors.accentEmerald,
                                  size: 20,
                                ),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: Text('Added '),
                                ),
                              ],
                            ),
                          ),
                        );
                      }
                    },
                    child: Container(
                      decoration: BoxDecoration(
                        borderRadius: AppRadius.lgBorder,
                        border: Border.all(color: AppColors.border, width: 1.2),
                        boxShadow: AppShadows.subtle,
                      ),
                      padding: const EdgeInsets.all(14),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 8,
                                  vertical: 4,
                                ),
                                decoration: BoxDecoration(
                                  color: typeBadgeBg,
                                  borderRadius: BorderRadius.circular(AppRadius.sm),
                                ),
                                child: Text(
                                  typeBadgeLabel,
                                  style: TextStyle(
                                    fontSize: 10,
                                    fontWeight: FontWeight.w700,
                                    letterSpacing: 0.5,
                                    color: typeBadgeColor,
                                  ),
                                ),
                              ),
                              Icon(
                                item.pricingType == PricingType.unit
                                    ? Icons.add_circle_outline_rounded
                                    : Icons.arrow_forward_ios_rounded,
                                size: 20,
                                color: AppColors.brandPrimary,
                              ),
                            ],
                          ),
                          if (item.imageUrl != null &&
                              item.imageUrl!.isNotEmpty) ...[
                            const SizedBox(height: 8),
                            ClipRRect(
                              borderRadius: AppRadius.smBorder,
                              child: AspectRatio(
                                aspectRatio: 16 / 9,
                                child: PurchImage(
                                  imageUrlOrPath: item.imageUrl,
                                  fit: BoxFit.cover,
                                ),
                              ),
                            ),
                          ],
                          const Spacer(),
                          Text(
                            item.name,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              fontSize: 17,
                              fontWeight: FontWeight.w700,
                              color: AppColors.textPrimary,
                              letterSpacing: -0.2,
                            ),
                          ),
                          const SizedBox(height: 8),
                          Text(
                            '₱',
                            style: AppTypography.priceBadge.copyWith(
                              fontSize: 18,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                );
              },
            );
          },
        ),
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
