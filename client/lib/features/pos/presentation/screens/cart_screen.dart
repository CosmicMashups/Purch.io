import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../domain/transaction_models.dart';
import '../providers/pos_providers.dart';
import 'payment_screen.dart';

/// D1's cart review — quantity adjustment and line removal, plus a way into
/// D5's payment method tabs. Also D4's Senior/PWD toggle and promo code entry.
class CartScreen extends ConsumerStatefulWidget {
  const CartScreen({super.key});

  @override
  ConsumerState<CartScreen> createState() => _CartScreenState();
}

class _CartScreenState extends ConsumerState<CartScreen> {
  final _promoCodeController = TextEditingController();

  @override
  void dispose() {
    _promoCodeController.dispose();
    super.dispose();
  }

  Future<void> _confirmVoid(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder:
          (context) => AlertDialog(
            shape: const RoundedRectangleBorder(
              borderRadius: AppRadius.lgBorder,
            ),
            title: const Text('Void cart?'),
            content: const Text(
              'This clears every item in the current sale. This cannot be undone.',
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(context).pop(false),
                child: const Text('Cancel'),
              ),
              FilledButton(
                style: FilledButton.styleFrom(
                  backgroundColor: AppColors.error,
                  foregroundColor: Colors.white,
                ),
                onPressed: () => Navigator.of(context).pop(true),
                child: const Text('Void'),
              ),
            ],
          ),
    );

    if (confirmed == true) {
      await ref.read(cartNotifierProvider.notifier).voidCart();
    }
  }

  @override
  Widget build(BuildContext context) {
    final cartAsync = ref.watch(cartNotifierProvider);
    final failure = ref.read(cartNotifierProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Cart'),
        actions: [
          IconButton(
            onPressed:
                cartAsync.valueOrNull == null
                    ? null
                    : () => _confirmVoid(context, ref),
            icon: const Icon(Icons.delete_sweep),
            tooltip: 'Void cart',
          ),
        ],
      ),
      body: cartAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error: (error, stackTrace) => ErrorStateView(
          message: 'Could not load the cart: $error',
          onRetry: () => ref.refresh(cartNotifierProvider),
        ),
        data: (cart) {
          if (cart.lines.isEmpty) {
            return EmptyStateView(
              icon: Icons.shopping_basket_outlined,
              title: 'Cart is empty — go back and add an item.',
              description:
                  'Tap items from the POS grid to add them to this order.',
              actionLabel: 'Browse Items',
              onAction: () => Navigator.of(context).maybePop(),
            );
          }

          return Column(
            children: [
              if (failure != null)
                Padding(
                  padding: const EdgeInsets.all(12),
                  child: Container(
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      color: AppColors.error.withAlpha(20),
                      borderRadius: AppRadius.smBorder,
                      border: Border.all(
                        color: AppColors.error.withAlpha(60),
                      ),
                    ),
                    child: Text(
                      failure.message,
                      style: const TextStyle(
                        color: AppColors.error,
                        fontSize: 13,
                        fontWeight: FontWeight.w500,
                      ),
                      textAlign: TextAlign.center,
                    ),
                  ),
                ),
              Expanded(
                child: ListView.separated(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 16,
                    vertical: 12,
                  ),
                  itemCount: cart.lines.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 8),
                  itemBuilder: (context, index) {
                    final line = cart.lines[index];
                    return _CartLineTile(line: line);
                  },
                ),
              ),
              Container(
                decoration: const BoxDecoration(
                  color: AppColors.surface,
                  boxShadow: AppShadows.card,
                  border: Border(top: BorderSide(color: AppColors.border)),
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    SwitchListTile(
                      value: cart.seniorPwdDiscountApplied,
                      activeColor: AppColors.brandPrimary,
                      onChanged:
                          (value) => ref
                              .read(cartNotifierProvider.notifier)
                              .applySeniorPwdDiscount(value),
                      title: const Text(
                        'Senior Citizen/PWD Discount (20%)',
                        style: TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      subtitle: const Text(
                        'Only apply after verifying the customer\'s physical ID.',
                        style: TextStyle(
                          fontSize: 12,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ),
                    const Divider(color: AppColors.border, height: 1),
                    Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 10,
                      ),
                      child:
                          cart.promoCode == null
                              ? Row(
                                children: [
                                  Expanded(
                                    child: TextField(
                                      controller: _promoCodeController,
                                      decoration: const InputDecoration(
                                        labelText: 'Promo code',
                                        isDense: true,
                                        prefixIcon: Icon(
                                          Icons.discount_outlined,
                                          size: 18,
                                        ),
                                      ),
                                      textCapitalization:
                                          TextCapitalization.characters,
                                    ),
                                  ),
                                  const SizedBox(width: 10),
                                  FilledButton(
                                    style: FilledButton.styleFrom(
                                      backgroundColor: AppColors.brandPrimary,
                                      foregroundColor: AppColors.onBrandPrimary,
                                    ),
                                    onPressed:
                                        () => ref
                                            .read(cartNotifierProvider.notifier)
                                            .applyPromoCode(
                                              _promoCodeController.text.trim(),
                                            ),
                                    child: const Text('Apply'),
                                  ),
                                ],
                              )
                              : Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 12,
                                  vertical: 6,
                                ),
                                decoration: BoxDecoration(
                                  color: AppColors.accentEmeraldContainer,
                                  borderRadius: AppRadius.smBorder,
                                ),
                                child: Row(
                                  children: [
                                    const Icon(
                                      Icons.check_circle_rounded,
                                      size: 18,
                                      color: AppColors.accentEmerald,
                                    ),
                                    const SizedBox(width: 8),
                                    Expanded(
                                      child: Text(
                                        'Promo code "${cart.promoCode}" applied',
                                        style: const TextStyle(
                                          color:
                                              AppColors
                                                  .onAccentEmeraldContainer,
                                          fontWeight: FontWeight.w600,
                                          fontSize: 13,
                                        ),
                                      ),
                                    ),
                                    TextButton(
                                      onPressed:
                                          () => ref
                                              .read(
                                                cartNotifierProvider.notifier,
                                              )
                                              .applyPromoCode(null),
                                      child: const Text(
                                        'Remove',
                                        style: TextStyle(
                                          color: AppColors.error,
                                          fontWeight: FontWeight.w600,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                    ),
                    const Divider(color: AppColors.border, height: 1),
                    Padding(
                      padding: const EdgeInsets.fromLTRB(20, 14, 20, 20),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          _TotalsRow(label: 'Subtotal', amount: cart.subtotal),
                          if (cart.discountAmount > 0)
                            _TotalsRow(
                              label: 'Discount',
                              amount: -cart.discountAmount,
                            ),
                          const SizedBox(height: 6),
                          _TotalsRow(
                            label: 'Total',
                            amount: cart.totalAmount,
                            emphasize: true,
                          ),
                          const SizedBox(height: 16),
                          SizedBox(
                            height: 52,
                            child: FilledButton(
                              style: FilledButton.styleFrom(
                                backgroundColor: AppColors.brandPrimary,
                                foregroundColor: AppColors.onBrandPrimary,
                                shape: const RoundedRectangleBorder(
                                  borderRadius: AppRadius.mdBorder,
                                ),
                              ),
                              onPressed:
                                  () => Navigator.of(context).push<void>(
                                    MaterialPageRoute(
                                      builder:
                                          (_) => PaymentScreen(
                                            total: cart.totalAmount,
                                          ),
                                    ),
                                  ),
                              child: const Text(
                                'Pay',
                                style: TextStyle(
                                  fontSize: 18,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _CartLineTile extends ConsumerWidget {
  const _CartLineTile({required this.line});

  final TransactionLine line;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final controller = ref.read(cartNotifierProvider.notifier);

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.mdBorder,
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      child: Row(
        children: [
          IconButton(
            onPressed: () => controller.removeLine(line.id),
            icon: const Icon(Icons.delete_outline, color: AppColors.error),
            tooltip: 'Remove',
          ),
          const SizedBox(width: 4),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  line.itemName,
                  style: const TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
                Text(
                  '₱${line.unitPrice.toStringAsFixed(2)} each',
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
          Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              IconButton(
                onPressed:
                    line.quantity > 1
                        ? () => controller.updateLine(
                          line.id,
                          UpdateTransactionLineRequest(
                            quantity: line.quantity - 1,
                          ),
                        )
                        : null,
                icon: const Icon(Icons.remove_circle_outline),
              ),
              SizedBox(
                width: 32,
                child: Text(
                  line.quantity.toStringAsFixed(
                    line.quantity.truncateToDouble() == line.quantity ? 0 : 2,
                  ),
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 14,
                  ),
                ),
              ),
              IconButton(
                onPressed:
                    () => controller.updateLine(
                      line.id,
                      UpdateTransactionLineRequest(quantity: line.quantity + 1),
                    ),
                icon: const Icon(Icons.add_circle_outline),
              ),
              const SizedBox(width: 8),
              SizedBox(
                width: 72,
                child: Text(
                  '₱${line.lineTotal.toStringAsFixed(2)}',
                  textAlign: TextAlign.right,
                  style: AppTypography.priceLine,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _TotalsRow extends StatelessWidget {
  const _TotalsRow({
    required this.label,
    required this.amount,
    this.emphasize = false,
  });

  final String label;
  final double amount;
  final bool emphasize;

  @override
  Widget build(BuildContext context) {
    final textStyle =
        emphasize
            ? const TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: AppColors.textPrimary,
            )
            : const TextStyle(
              fontSize: 14,
              color: AppColors.textSecondary,
            );

    final amountStyle =
        emphasize
            ? const TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.w800,
              color: AppColors.brandPrimary,
              fontFeatures: [FontFeature.tabularFigures()],
            )
            : TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w600,
              color: amount < 0 ? AppColors.accentEmerald : AppColors.textPrimary,
              fontFeatures: const [FontFeature.tabularFigures()],
            );

    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label, style: textStyle),
        Text(
          amount < 0
              ? '₱-${(-amount).toStringAsFixed(2)}'
              : '₱${amount.toStringAsFixed(2)}',
          style: amountStyle,
        ),
      ],
    );
  }
}

