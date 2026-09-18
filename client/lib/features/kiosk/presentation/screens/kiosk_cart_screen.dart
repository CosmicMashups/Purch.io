import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../pos/domain/transaction_models.dart';
import '../providers/kiosk_providers.dart';
import 'kiosk_fulfillment_screen.dart';

/// Order review before E4's fulfillment choice — not its own lettered PAGES.md
/// step, but a necessary bridge: the customer needs to see what they've
/// picked and adjust quantities before moving on. No discount/promo/payment
/// controls here at all — those don't exist on the kiosk's cart engine.
class KioskCartScreen extends ConsumerWidget {
  const KioskCartScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cartAsync = ref.watch(kioskCartNotifierProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Your Order'),
        backgroundColor: AppColors.surface,
        centerTitle: true,
        elevation: 0,
        scrolledUnderElevation: 1,
      ),
      body: cartAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error:
            (error, stackTrace) => ErrorStateView(
              message: error.toString(),
              onRetry: () => ref.refresh(kioskCartNotifierProvider),
            ),
        data: (cart) {
          if (cart.lines.isEmpty) {
            return EmptyStateView(
              icon: Icons.remove_shopping_cart_outlined,
              title: 'Your order is empty — go back and add something.',
              description:
                  'Explore the kiosk menu and select items to add to your tray.',
              actionLabel: 'Browse Menu',
              onAction: () => Navigator.of(context).pop(),
            );
          }

          return Column(
            children: [
              Expanded(
                child: ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: cart.lines.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 12),
                  itemBuilder: (context, index) {
                    final line = cart.lines[index];
                    return Container(
                      decoration: BoxDecoration(
                        color: AppColors.surface,
                        borderRadius: AppRadius.mdBorder,
                        border: Border.all(color: AppColors.border),
                        boxShadow: AppShadows.subtle,
                      ),
                      child: ListTile(
                        contentPadding: const EdgeInsets.symmetric(
                          horizontal: 16,
                          vertical: 4,
                        ),
                        title: Text(
                          line.itemName,
                          style: const TextStyle(
                            fontSize: 17,
                            fontWeight: FontWeight.w700,
                            color: AppColors.textPrimary,
                          ),
                        ),
                        subtitle: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(
                              '${line.quantity.toStringAsFixed(0)} × ₱${line.unitPrice.toStringAsFixed(2)}',
                              style: const TextStyle(
                                fontSize: 14,
                                color: AppColors.textSecondary,
                              ),
                            ),
                            if (line.itemVariantAttributes.isNotEmpty)
                              Padding(
                                padding: const EdgeInsets.only(top: 2),
                                child: Text(
                                  line.variantAttributesLabel,
                                  style: const TextStyle(
                                    fontSize: 12,
                                    fontWeight: FontWeight.w600,
                                    color: AppColors.textSecondary,
                                    fontStyle: FontStyle.italic,
                                  ),
                                ),
                              ),
                            if (line.comboSelections.isNotEmpty)
                              Padding(
                                padding: const EdgeInsets.only(top: 4),
                                child: Wrap(
                                  spacing: 4,
                                  runSpacing: 2,
                                  children: [
                                    for (final selection in line.comboSelections)
                                      Container(
                                        padding: const EdgeInsets.symmetric(
                                          horizontal: 6,
                                          vertical: 1,
                                        ),
                                        decoration: BoxDecoration(
                                          color: AppColors.accentWarmContainer,
                                          borderRadius: BorderRadius.circular(4),
                                        ),
                                        child: Text(
                                          '${selection.slotLabel}: ${selection.selectedItemName}',
                                          style: const TextStyle(
                                            fontSize: 10,
                                            fontWeight: FontWeight.w600,
                                            color: AppColors.onAccentWarmContainer,
                                          ),
                                        ),
                                      ),
                                  ],
                                ),
                              ),
                            if (line.modifierSelections.isNotEmpty)
                              Padding(
                                padding: const EdgeInsets.only(top: 4),
                                child: Wrap(
                                  spacing: 4,
                                  runSpacing: 2,
                                  children: [
                                    for (final mod in line.modifierSelections)
                                      Container(
                                        padding: const EdgeInsets.symmetric(
                                          horizontal: 6,
                                          vertical: 1,
                                        ),
                                        decoration: BoxDecoration(
                                          color: AppColors.brandPrimaryContainer,
                                          borderRadius: BorderRadius.circular(4),
                                        ),
                                        child: Text(
                                          mod.priceDelta > 0
                                              ? '+ ${mod.modifierName} (₱${mod.priceDelta.toStringAsFixed(2)})'
                                              : '+ ${mod.modifierName}',
                                          style: const TextStyle(
                                            fontSize: 10,
                                            fontWeight: FontWeight.w600,
                                            color: AppColors.brandPrimary,
                                          ),
                                        ),
                                      ),
                                  ],
                                ),
                              ),
                            if (line.appliedPromoLabel != null)
                              Padding(
                                padding: const EdgeInsets.only(top: 4),
                                child: Container(
                                  padding: const EdgeInsets.symmetric(
                                    horizontal: 6,
                                    vertical: 1,
                                  ),
                                  decoration: BoxDecoration(
                                    color: AppColors.accentEmeraldContainer,
                                    borderRadius: BorderRadius.circular(4),
                                  ),
                                  child: Row(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      const Icon(
                                        Icons.local_offer_outlined,
                                        size: 11,
                                        color: AppColors.accentEmerald,
                                      ),
                                      const SizedBox(width: 3),
                                      Text(
                                        line.appliedPromoLabel!,
                                        style: const TextStyle(
                                          fontSize: 10,
                                          fontWeight: FontWeight.w700,
                                          color: AppColors.accentEmerald,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              ),
                          ],
                        ),
                        trailing: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(
                              '₱${line.lineTotal.toStringAsFixed(2)}',
                              style: AppTypography.priceLine.copyWith(
                                fontSize: 17,
                              ),
                            ),
                            const SizedBox(width: 8),
                            IconButton(
                              icon: const Icon(
                                Icons.delete_outline,
                                color: AppColors.error,
                              ),
                              tooltip: 'Remove',
                              onPressed:
                                  () => ref
                                      .read(kioskCartNotifierProvider.notifier)
                                      .removeLine(line.id),
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                ),
              ),
              _TotalBar(cart: cart),
            ],
          );
        },
      ),
    );
  }
}

class _TotalBar extends StatelessWidget {
  const _TotalBar({required this.cart});

  final Transaction cart;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: AppColors.surface,
        border: Border(
          top: BorderSide(color: AppColors.border, width: 1.2),
        ),
        boxShadow: AppShadows.card,
      ),
      child: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text('Total', style: TextStyle(fontSize: 18)),
                  Text(
                    '₱${cart.totalAmount.toStringAsFixed(2)}',
                    style: const TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                      color: AppColors.brandPrimary,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Container(
                decoration: const BoxDecoration(
                  boxShadow: AppShadows.tactileButton,
                  borderRadius: AppRadius.mdBorder,
                ),
                child: SizedBox(
                  height: 56,
                  child: FilledButton(
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.brandPrimary,
                      foregroundColor: AppColors.onBrandPrimary,
                      shape: const RoundedRectangleBorder(
                        borderRadius: AppRadius.mdBorder,
                      ),
                    ),
                    onPressed:
                        cart.lines.isEmpty
                            ? null
                            : () => Navigator.of(context).push<void>(
                              MaterialPageRoute(
                                builder: (_) => const KioskFulfillmentScreen(),
                              ),
                            ),
                    child: const Text('Continue'),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
