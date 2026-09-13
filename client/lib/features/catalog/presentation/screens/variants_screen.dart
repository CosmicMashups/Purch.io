import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../providers/catalog_providers.dart';
import 'add_variant_screen.dart';

/// B2's variant matrix half.
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
      backgroundColor: AppColors.background,
      appBar: AppBar(title: Text('Variants: $itemName')),
      body: variantsAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load variants: $error',
          onRetry: () =>
              ref.read(itemVariantListProvider(itemId).notifier).refresh(),
        ),
        data: (variants) {
          if (variants.isEmpty) {
            return EmptyStateView(
              icon: Icons.style_outlined,
              title: 'No variants yet — tap + to add one.',
              description:
                  'Set up size, color, or flavor combinations with independent prices and SKUs.',
              actionLabel: 'Add Variant',
              onAction: () => Navigator.of(context).push<void>(
                MaterialPageRoute(
                  builder: (_) => AddVariantScreen(
                    itemId: itemId,
                    itemName: itemName,
                  ),
                ),
              ),
            );
          }

          return RefreshIndicator(
            onRefresh:
                () =>
                    ref
                        .read(itemVariantListProvider(itemId).notifier)
                        .refresh(),
            child: ListView.separated(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
              itemCount: variants.length,
              separatorBuilder: (_, __) => const SizedBox(height: 8),
              itemBuilder: (context, index) {
                final variant = variants[index];
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
                    subtitle: Text(
                      [
                        if (variant.sku != null) 'SKU: ${variant.sku}',
                        '${variant.stockOnHand} in stock',
                        if (variant.priceOverride != null)
                          '₱${variant.priceOverride!.toStringAsFixed(2)}',
                      ].join(' · '),
                      style: const TextStyle(
                        fontSize: 13,
                        color: AppColors.textSecondary,
                        fontFeatures: [FontFeature.tabularFigures()],
                      ),
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

