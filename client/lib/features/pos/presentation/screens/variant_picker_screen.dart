import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';

/// D3 — lets the cashier pick which variant (size/color/etc.) of a
/// PricingType.variantMatrix item to add.
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
      backgroundColor: AppColors.background,
      appBar: AppBar(title: Text(item.name)),
      body: variantsAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) => Center(
              child: Text(
                'Could not load variants: $error',
                style: const TextStyle(color: AppColors.error),
              ),
            ),
        data: (variants) {
          if (variants.isEmpty) {
            return Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    width: 64,
                    height: 64,
                    decoration: BoxDecoration(
                      color: AppColors.cardHover,
                      borderRadius: BorderRadius.circular(AppRadius.lg),
                    ),
                    child: const Icon(
                      Icons.style_outlined,
                      size: 32,
                      color: AppColors.textMuted,
                    ),
                  ),
                  const SizedBox(height: 16),
                  const Text(
                    'No variants have been configured for this item yet.',
                    style: TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            );
          }

          return ListView.separated(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
            itemCount: variants.length,
            separatorBuilder: (_, __) => const SizedBox(height: 8),
            itemBuilder: (context, index) {
              final variant = variants[index];
              final price = variant.priceOverride ?? item.basePrice;

              return Container(
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: AppRadius.mdBorder,
                  boxShadow: AppShadows.subtle,
                  border: Border.all(color: AppColors.border),
                ),
                child: ListTile(
                  leading: Container(
                    width: 40,
                    height: 40,
                    decoration: BoxDecoration(
                      color: AppColors.brandPrimaryContainer,
                      borderRadius: BorderRadius.circular(AppRadius.sm),
                    ),
                    child: const Icon(
                      Icons.style_rounded,
                      color: AppColors.brandPrimary,
                      size: 20,
                    ),
                  ),
                  title: Text(
                    variant.attributesLabel,
                    style: const TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  subtitle:
                      variant.sku != null
                          ? Text(
                            'SKU: ${variant.sku}',
                            style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.textSecondary,
                            ),
                          )
                          : null,
                  trailing: Text(
                    '₱${price.toStringAsFixed(2)}',
                    style: AppTypography.priceBadge,
                  ),
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
                ),
              );
            },
          );
        },
      ),
    );
  }
}

