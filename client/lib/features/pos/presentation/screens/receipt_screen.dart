import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_fonts/google_fonts.dart';

import '../../../../core/hardware/hardware_providers.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../onboarding/presentation/providers/onboarding_providers.dart';
import '../providers/pos_providers.dart';
import 'cashier_screen.dart';

/// D6, minimal slice — shows the just-completed sale's sequential BIR
/// receipt number and a summary.
class ReceiptScreen extends ConsumerWidget {
  const ReceiptScreen({super.key});

  Future<void> _startNewSale(BuildContext context, WidgetRef ref) async {
    await ref.read(cartNotifierProvider.notifier).startNewSale();
    if (!context.mounted) {
      return;
    }
    await Navigator.of(context).pushAndRemoveUntil<void>(
      MaterialPageRoute(builder: (_) => const CashierScreen()),
      (route) => route.isFirst,
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final cart = ref.watch(cartNotifierProvider).valueOrNull;

    return PopScope(
      canPop: false,
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(
          title: const Text('Receipt'),
          automaticallyImplyLeading: false,
        ),
        body: SafeArea(
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 440),
              child:
                  cart == null
                      ? const CircularProgressIndicator()
                      : SingleChildScrollView(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 20,
                          vertical: 24,
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            // Receipt paper card
                            Container(
                              decoration: BoxDecoration(
                                color: AppColors.surface,
                                borderRadius: AppRadius.xlBorder,
                                boxShadow: AppShadows.card,
                                border: Border.all(color: AppColors.border),
                              ),
                              padding: const EdgeInsets.all(28),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.stretch,
                                children: [
                                  Center(
                                    child: Container(
                                      width: 56,
                                      height: 56,
                                      decoration: BoxDecoration(
                                        color: AppColors.accentEmeraldContainer,
                                        borderRadius: BorderRadius.circular(
                                          AppRadius.full,
                                        ),
                                      ),
                                      child: const Icon(
                                        Icons.check_circle_rounded,
                                        color: AppColors.accentEmerald,
                                        size: 32,
                                      ),
                                    ),
                                  ),
                                  const SizedBox(height: 16),
                                  Text(
                                    'Receipt No. ${cart.receiptNumber ?? '—'}',
                                    style: GoogleFonts.outfit(
                                      fontSize: 22,
                                      fontWeight: FontWeight.w800,
                                      color: AppColors.textPrimary,
                                      letterSpacing: -0.3,
                                    ),
                                    textAlign: TextAlign.center,
                                  ),
                                  const SizedBox(height: 4),
                                  if (cart.savedOffline) ...[
                                    Container(
                                      margin: const EdgeInsets.only(bottom: 8),
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 12,
                                        vertical: 8,
                                      ),
                                      decoration: BoxDecoration(
                                        color: AppColors.accentWarm.withValues(
                                          alpha: 0.12,
                                        ),
                                        borderRadius: BorderRadius.circular(8),
                                      ),
                                      child: const Text(
                                        'Saved offline — this sale will be '
                                        'recorded when the connection returns.',
                                        textAlign: TextAlign.center,
                                        style: TextStyle(
                                          fontSize: 12,
                                          fontWeight: FontWeight.w600,
                                        ),
                                      ),
                                    ),
                                  ],
                                  Text(
                                    'Transaction Complete',
                                    style: GoogleFonts.plusJakartaSans(
                                      fontSize: 13,
                                      fontWeight: FontWeight.w500,
                                      color: AppColors.textSecondary,
                                    ),
                                    textAlign: TextAlign.center,
                                  ),
                                  const SizedBox(height: 20),
                                  const Divider(
                                    color: AppColors.border,
                                    height: 1,
                                  ),
                                  const SizedBox(height: 14),

                                  // Line items
                                  for (final line in cart.lines)
                                    Padding(
                                      padding: const EdgeInsets.symmetric(
                                        vertical: 5,
                                      ),
                                      child: Column(
                                        crossAxisAlignment: CrossAxisAlignment.start,
                                        children: [
                                          Row(
                                            mainAxisAlignment:
                                                MainAxisAlignment.spaceBetween,
                                            children: [
                                              Expanded(
                                                child: Text(
                                                  '${line.itemName} ×${line.quantity.toStringAsFixed(line.quantity.truncateToDouble() == line.quantity ? 0 : 2)}',
                                                  style: GoogleFonts.plusJakartaSans(
                                                    fontSize: 14,
                                                    fontWeight: FontWeight.w500,
                                                    color: AppColors.textPrimary,
                                                  ),
                                                ),
                                              ),
                                              Text(
                                                '₱${line.lineTotal.toStringAsFixed(2)}',
                                                style: GoogleFonts.jetBrainsMono(
                                                  fontSize: 14,
                                                  fontWeight: FontWeight.w600,
                                                  color: AppColors.textPrimary,
                                                ),
                                              ),
                                            ],
                                          ),
                                          if (line.itemVariantAttributes.isNotEmpty)
                                            Padding(
                                              padding: const EdgeInsets.only(left: 8, top: 2),
                                              child: Text(
                                                '  ${line.variantAttributesLabel}',
                                                style: GoogleFonts.plusJakartaSans(
                                                  fontSize: 12,
                                                  fontStyle: FontStyle.italic,
                                                  color: AppColors.textSecondary,
                                                ),
                                              ),
                                            ),
                                          if (line.comboSelections.isNotEmpty)
                                            Padding(
                                              padding: const EdgeInsets.only(left: 8, top: 2),
                                              child: Column(
                                                crossAxisAlignment: CrossAxisAlignment.start,
                                                children: [
                                                  for (final selection in line.comboSelections)
                                                    Text(
                                                      '  ${selection.slotLabel}: ${selection.selectedItemName}',
                                                      style: GoogleFonts.plusJakartaSans(
                                                        fontSize: 12,
                                                        color: AppColors.textSecondary,
                                                      ),
                                                    ),
                                                ],
                                              ),
                                            ),
                                          if (line.modifierSelections.isNotEmpty)
                                            Padding(
                                              padding: const EdgeInsets.only(left: 8, top: 2),
                                              child: Column(
                                                crossAxisAlignment: CrossAxisAlignment.start,
                                                children: [
                                                  for (final mod in line.modifierSelections)
                                                    Text(
                                                      mod.priceDelta > 0
                                                          ? '  + ${mod.modifierName} (+₱${mod.priceDelta.toStringAsFixed(2)})'
                                                          : '  + ${mod.modifierName}',
                                                      style: GoogleFonts.plusJakartaSans(
                                                        fontSize: 12,
                                                        color: AppColors.textSecondary,
                                                      ),
                                                    ),
                                                ],
                                              ),
                                            ),
                                          if (line.appliedPromoLabel != null)
                                            Padding(
                                              padding: const EdgeInsets.only(left: 8, top: 2),
                                              child: Text(
                                                '  Promo: ${line.appliedPromoLabel}',
                                                style: GoogleFonts.plusJakartaSans(
                                                  fontSize: 12,
                                                  fontWeight: FontWeight.w600,
                                                  color: AppColors.accentEmerald,
                                                ),
                                              ),
                                            ),
                                        ],
                                      ),
                                    ),
                                  const SizedBox(height: 14),
                                  const Divider(
                                    color: AppColors.border,
                                    height: 1,
                                  ),
                                  const SizedBox(height: 14),

                                  // Item promos row
                                  if (cart.itemPromoDiscountAmount > 0) ...[
                                    Row(
                                      mainAxisAlignment:
                                          MainAxisAlignment.spaceBetween,
                                      children: [
                                        Text(
                                          'Item promos',
                                          style: GoogleFonts.plusJakartaSans(
                                            fontSize: 13,
                                            fontWeight: FontWeight.w600,
                                            color: AppColors.textSecondary,
                                          ),
                                        ),
                                        Text(
                                          '-₱${cart.itemPromoDiscountAmount.toStringAsFixed(2)}',
                                          style: GoogleFonts.jetBrainsMono(
                                            fontSize: 13,
                                            fontWeight: FontWeight.w700,
                                            color: AppColors.accentEmerald,
                                          ),
                                        ),
                                      ],
                                    ),
                                    const SizedBox(height: 4),
                                  ],
                                  if (cart.discountAmount > 0) ...[
                                    Row(
                                      mainAxisAlignment:
                                          MainAxisAlignment.spaceBetween,
                                      children: [
                                        Text(
                                          cart.seniorPwdDiscountApplied
                                              ? 'Senior/PWD (20%)'
                                              : (cart.promoCode != null &&
                                                      cart.promoDiscountAmount >
                                                          0)
                                              ? 'Promo code (${cart.promoCode})'
                                              : 'Discount',
                                          style: GoogleFonts.plusJakartaSans(
                                            fontSize: 13,
                                            fontWeight: FontWeight.w600,
                                            color: AppColors.textSecondary,
                                          ),
                                        ),
                                        Text(
                                          '-₱${cart.discountAmount.toStringAsFixed(2)}',
                                          style: GoogleFonts.jetBrainsMono(
                                            fontSize: 13,
                                            fontWeight: FontWeight.w700,
                                            color: AppColors.accentEmerald,
                                          ),
                                        ),
                                      ],
                                    ),
                                    const SizedBox(height: 4),
                                  ],

                                  // Total row
                                  Row(
                                    mainAxisAlignment:
                                        MainAxisAlignment.spaceBetween,
                                    children: [
                                      Text(
                                        'Total',
                                        style: GoogleFonts.plusJakartaSans(
                                          fontSize: 16,
                                          fontWeight: FontWeight.w800,
                                          color: AppColors.textPrimary,
                                        ),
                                      ),
                                      Text(
                                        '₱${cart.totalAmount.toStringAsFixed(2)}',
                                        style: GoogleFonts.jetBrainsMono(
                                          fontSize: 18,
                                          fontWeight: FontWeight.w800,
                                          color: AppColors.brandPrimary,
                                        ),
                                      ),
                                    ],
                                  ),

                                  // Change row
                                  if (cart.payments.isNotEmpty) ...[
                                    const SizedBox(height: 8),
                                    for (final payment in cart.payments)
                                      if (payment.changeGiven != null)
                                        Row(
                                          mainAxisAlignment:
                                              MainAxisAlignment.spaceBetween,
                                          children: [
                                            Text(
                                              'Change',
                                              style: GoogleFonts.plusJakartaSans(
                                                fontSize: 14,
                                                fontWeight: FontWeight.w600,
                                                color: AppColors.textSecondary,
                                              ),
                                            ),
                                            Text(
                                              '₱${payment.changeGiven!.toStringAsFixed(2)}',
                                              style: GoogleFonts.jetBrainsMono(
                                                fontSize: 15,
                                                fontWeight: FontWeight.w700,
                                                color: AppColors.accentEmerald,
                                              ),
                                            ),
                                          ],
                                        ),
                                  ],
                                ],
                              ),
                            ),
                            const SizedBox(height: 20),

                            // Action Buttons (Thermal Print & Open Drawer)
                            Row(
                              children: [
                                Expanded(
                                  child: OutlinedButton.icon(
                                    style: OutlinedButton.styleFrom(
                                      padding: const EdgeInsets.symmetric(vertical: 14),
                                      shape: const RoundedRectangleBorder(
                                        borderRadius: AppRadius.mdBorder,
                                      ),
                                    ),
                                    onPressed: () async {
                                      final tenantSettings =
                                          ref.read(tenantSettingsNotifierProvider).valueOrNull;
                                      final success = await ref
                                          .read(printerServiceProvider)
                                          .printReceipt(
                                            transaction: cart,
                                            tenantSettings: tenantSettings,
                                            cashierName: 'Cashier',
                                            cutPaper: true,
                                          );
                                      if (context.mounted) {
                                        ScaffoldMessenger.of(context).showSnackBar(
                                          SnackBar(
                                            content: Text(
                                              success
                                                  ? 'Receipt sent to thermal printer.'
                                                  : 'Failed to print receipt.',
                                            ),
                                          ),
                                        );
                                      }
                                    },
                                    icon: const Icon(Icons.print_rounded, size: 18),
                                    label: const Text('Print Receipt'),
                                  ),
                                ),
                                const SizedBox(width: 12),
                                OutlinedButton.icon(
                                  style: OutlinedButton.styleFrom(
                                    padding: const EdgeInsets.symmetric(
                                      vertical: 14,
                                      horizontal: 16,
                                    ),
                                    shape: const RoundedRectangleBorder(
                                      borderRadius: AppRadius.mdBorder,
                                    ),
                                  ),
                                  onPressed: () async {
                                    try {
                                      await ref
                                          .read(cashDrawerServiceProvider)
                                          .openManual(
                                            operatorName: 'Cashier',
                                            reason: 'Manual Open after Sale',
                                            isManagerOverride: true,
                                          );
                                    } catch (e) {
                                      if (context.mounted) {
                                        ScaffoldMessenger.of(context).showSnackBar(
                                          SnackBar(content: Text('$e')),
                                        );
                                      }
                                    }
                                  },
                                  icon: const Icon(Icons.point_of_sale_rounded, size: 18),
                                  label: const Text('Drawer'),
                                ),
                              ],
                            ),
                            const SizedBox(height: 14),

                            // New Sale CTA
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
                                onPressed: () => _startNewSale(context, ref),
                                child: const Text(
                                  'New Sale',
                                  style: TextStyle(
                                    fontSize: 16,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
            ),
          ),
        ),
      ),
    );
  }
}

