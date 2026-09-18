import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../providers/item_promo_providers.dart';
import 'add_edit_combo_promo_screen.dart';
import 'promo_status_badge.dart';

class ComboPromoTab extends ConsumerWidget {
  const ComboPromoTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final rulesAsync = ref.watch(comboPromoRuleListProvider);
    final itemsAsync = ref.watch(itemListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      body: rulesAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) => ErrorStateView(
              message: error.toString(),
              onRetry:
                  () =>
                      ref.read(comboPromoRuleListProvider.notifier).refresh(),
            ),
        data: (rules) {
          if (rules.isEmpty) {
            return EmptyStateView(
              icon: Icons.set_meal_outlined,
              title: 'No combo deals yet — tap + to add one.',
              description:
                  'Bundle two items together for a fixed total price during checkout.',
              actionLabel: 'Add Combo Deal',
              onAction: () => _openAdd(context),
            );
          }

          final items = itemsAsync.valueOrNull ?? const <Item>[];
          String nameFor(String id) {
            for (final item in items) {
              if (item.id == id) {
                return item.name;
              }
            }
            return 'Unknown item';
          }

          return RefreshIndicator(
            onRefresh:
                () => ref.read(comboPromoRuleListProvider.notifier).refresh(),
            child: ListView.builder(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.lg,
                vertical: AppSpacing.md,
              ),
              itemCount: rules.length,
              itemBuilder: (context, index) {
                final rule = rules[index];
                return Card(
                  elevation: 0,
                  color: AppColors.surface,
                  margin: const EdgeInsets.only(bottom: AppSpacing.sm),
                  shape: const RoundedRectangleBorder(
                    borderRadius: AppRadius.mdBorder,
                    side: BorderSide(color: AppColors.border),
                  ),
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.lg,
                      vertical: AppSpacing.xs,
                    ),
                    leading: Container(
                      padding: const EdgeInsets.all(8),
                      decoration: const BoxDecoration(
                        color: AppColors.brandPrimaryContainer,
                        shape: BoxShape.circle,
                      ),
                      child: const Icon(
                        Icons.set_meal_outlined,
                        size: 18,
                        color: AppColors.brandPrimary,
                      ),
                    ),
                    title: Text(
                      rule.name,
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                    subtitle: Text(
                      '${nameFor(rule.itemAId)} + ${nameFor(rule.itemBId)} = '
                      '₱${rule.comboPrice.toStringAsFixed(2)}',
                      style: const TextStyle(color: AppColors.textSecondary),
                    ),
                    trailing: PromoStatusBadge(
                      isActive: rule.isActive,
                      startsAt: rule.startsAt,
                      endsAt: rule.endsAt,
                    ),
                    onTap:
                        () => Navigator.of(context).push<void>(
                          MaterialPageRoute(
                            builder:
                                (_) => AddEditComboPromoScreen(rule: rule),
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
        onPressed: () => _openAdd(context),
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: Colors.white,
        tooltip: 'Add combo deal',
        child: const Icon(Icons.add),
      ),
    );
  }

  void _openAdd(BuildContext context) {
    Navigator.of(context).push<void>(
      MaterialPageRoute(builder: (_) => const AddEditComboPromoScreen()),
    );
  }
}
