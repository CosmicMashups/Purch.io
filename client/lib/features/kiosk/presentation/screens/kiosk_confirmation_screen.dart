import 'package:flutter/material.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../pos/domain/transaction_models.dart';
import 'kiosk_landing_screen.dart';

/// E6 — the order/prep number confirmation. A dead end by design (PopScope
/// blocks back navigation, same "no way back into a finished cart" pattern
/// as the cashier POS's ReceiptScreen): the only way out is "New Order",
/// which returns to the landing screen for the next customer.
class KioskConfirmationScreen extends StatelessWidget {
  const KioskConfirmationScreen({super.key, required this.order});

  final Transaction order;

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: false,
      child: Scaffold(
        backgroundColor: AppColors.background,
        body: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.xxl),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 540),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Container(
                      width: 96,
                      height: 96,
                      decoration: const BoxDecoration(
                        color: AppColors.accentEmeraldContainer,
                        shape: BoxShape.circle,
                      ),
                      child: const Center(
                        child: Icon(
                          Icons.check_circle_rounded,
                          size: 64,
                          color: AppColors.accentEmerald,
                        ),
                      ),
                    ),
                    const SizedBox(height: AppSpacing.lg),
                    const Text(
                      'Order Submitted!',
                      style: TextStyle(
                        fontSize: 28,
                        fontWeight: FontWeight.bold,
                        color: AppColors.textPrimary,
                        letterSpacing: -0.5,
                      ),
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    const Text(
                      'Please take your receipt and proceed to the counter.',
                      style: TextStyle(
                        fontSize: 14,
                        color: AppColors.textSecondary,
                      ),
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: AppSpacing.xl),
                    // Ticket card
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(AppSpacing.xl),
                      decoration: BoxDecoration(
                        color: AppColors.card,
                        borderRadius: AppRadius.lgBorder,
                        border: Border.all(color: AppColors.border),
                        boxShadow: AppShadows.card,
                      ),
                      child: Column(
                        children: [
                          const Text(
                            'Your order number is',
                            style: TextStyle(
                              fontSize: 14,
                              color: AppColors.textSecondary,
                              fontWeight: FontWeight.w600,
                              letterSpacing: 0.5,
                            ),
                            textAlign: TextAlign.center,
                          ),
                          const SizedBox(height: AppSpacing.md),
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: AppSpacing.xl,
                              vertical: AppSpacing.md,
                            ),
                            decoration: BoxDecoration(
                              color: AppColors.brandPrimaryContainer,
                              borderRadius: AppRadius.mdBorder,
                            ),
                            child: Text(
                              '${order.kioskPrepNumber ?? '—'}',
                              style: const TextStyle(
                                fontSize: 48,
                                fontWeight: FontWeight.w900,
                                color: AppColors.brandPrimary,
                                letterSpacing: 2.0,
                              ),
                              textAlign: TextAlign.center,
                            ),
                          ),
                          const SizedBox(height: AppSpacing.lg),
                          Container(
                            padding: const EdgeInsets.all(AppSpacing.md),
                            decoration: BoxDecoration(
                              color: AppColors.background,
                              borderRadius: AppRadius.smBorder,
                            ),
                            child: Row(
                              children: const [
                                Icon(
                                  Icons.info_outline,
                                  size: 20,
                                  color: AppColors.textMuted,
                                ),
                                SizedBox(width: AppSpacing.sm),
                                Expanded(
                                  child: Text(
                                    'Please show this number and pay at the counter to '
                                    'complete your order.',
                                    style: TextStyle(
                                      fontSize: 13,
                                      color: AppColors.textPrimary,
                                      height: 1.3,
                                    ),
                                    textAlign: TextAlign.left,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: AppSpacing.xxl),
                    SizedBox(
                      height: 64,
                      width: double.infinity,
                      child: FilledButton(
                        style: FilledButton.styleFrom(
                          backgroundColor: AppColors.brandPrimary,
                          foregroundColor: AppColors.onBrandPrimary,
                          shape: const RoundedRectangleBorder(
                            borderRadius: AppRadius.mdBorder,
                          ),
                          elevation: 0,
                        ),
                        onPressed:
                            () =>
                                Navigator.of(context).pushAndRemoveUntil<void>(
                                  MaterialPageRoute(
                                    builder: (_) => const KioskLandingScreen(),
                                  ),
                                  (route) => false,
                                ),
                        child: const Text(
                          'New Order',
                          style: TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w600,
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
