import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../providers/promo_code_providers.dart';
import 'add_promo_code_screen.dart';

/// Admin/Manager promo code management — the code the cashier types in at
/// D4's cart review. See CashierScreen's cart panel for where they're applied.
class PromoCodeListScreen extends ConsumerWidget {
  const PromoCodeListScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final promoCodesAsync = ref.watch(promoCodeListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Promo Codes'),
        backgroundColor: AppColors.surface,
        elevation: 0,
      ),
      body: promoCodesAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) => ErrorStateView(
              message: error.toString(),
              onRetry: () => ref.read(promoCodeListProvider.notifier).refresh(),
            ),
        data: (promoCodes) {
          if (promoCodes.isEmpty) {
            return EmptyStateView(
              icon: Icons.local_offer_outlined,
              title: 'No promo codes yet — tap + to add one.',
              description:
                  'Create promotional discount codes (percentage or fixed amount) to use during checkout.',
              actionLabel: 'Create Promo Code',
              onAction:
                  () => Navigator.of(context).push<void>(
                    MaterialPageRoute(
                      builder: (_) => const AddPromoCodeScreen(),
                    ),
                  ),
            );
          }

          return RefreshIndicator(
            onRefresh: () => ref.read(promoCodeListProvider.notifier).refresh(),
            child: ListView.builder(
              padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg, vertical: AppSpacing.md),
              itemCount: promoCodes.length,
              itemBuilder: (context, index) {
                final promoCode = promoCodes[index];
                return Card(
                  elevation: 0,
                  color: AppColors.surface,
                  margin: const EdgeInsets.only(bottom: AppSpacing.sm),
                  shape: const RoundedRectangleBorder(
                    borderRadius: AppRadius.mdBorder,
                    side: BorderSide(color: AppColors.border),
                  ),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg, vertical: AppSpacing.xs),
                    leading: Container(
                      padding: const EdgeInsets.all(8),
                      decoration: const BoxDecoration(
                        color: AppColors.brandPrimaryContainer,
                        shape: BoxShape.circle,
                      ),
                      child: const Icon(Icons.local_offer_outlined, size: 18, color: AppColors.brandPrimary),
                    ),
                    title: Text(
                      promoCode.code,
                      style: Theme.of(context).textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                        letterSpacing: 0.5,
                      ),
                    ),
                    subtitle: Text(
                      promoCode.discountLabel,
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
                    ),
                    trailing:
                        promoCode.isActive
                            ? Container(
                              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                              decoration: BoxDecoration(
                                color: AppColors.accentEmeraldContainer,
                                borderRadius: BorderRadius.circular(AppRadius.full),
                              ),
                              child: Text(
                                'Active',
                                style: Theme.of(context).textTheme.labelSmall?.copyWith(
                                  color: AppColors.accentEmerald,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            )
                            : const Chip(label: Text('Inactive')),
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
              MaterialPageRoute(builder: (_) => const AddPromoCodeScreen()),
            ),
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: Colors.white,
        tooltip: 'Add promo code',
        child: const Icon(Icons.add),
      ),
    );
  }
}
