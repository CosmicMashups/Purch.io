import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../providers/item_promo_providers.dart';
import 'add_edit_bogo_promo_screen.dart';
import 'promo_status_badge.dart';

class BogoPromoTab extends ConsumerWidget {
  const BogoPromoTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final rulesAsync = ref.watch(bogoPromoRuleListProvider);
    final itemsAsync = ref.watch(itemListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      body: rulesAsync.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) => ErrorStateView(
              message: error.toString(),
              onRetry:
                  () => ref.read(bogoPromoRuleListProvider.notifier).refresh(),
            ),
        data: (rules) {
          if (rules.isEmpty) {
            return EmptyStateView(
              icon: Icons.card_giftcard_outlined,
              title: 'No Buy 1 Take 1 promos yet — tap + to add one.',
              description:
                  'Set up a trigger item and quantity, and a free item to give away automatically at checkout.',
              actionLabel: 'Add Promo',
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
                () => ref.read(bogoPromoRuleListProvider.notifier).refresh(),
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
                        Icons.card_giftcard_outlined,
                        size: 18,
                        color: AppColors.brandPrimary,
                      ),
                    ),
                    title: Text(
                      rule.name,
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                    subtitle: Text(
                      'Buy ${rule.triggerQuantity} ${nameFor(rule.triggerItemId)} → '
                      'Get ${rule.freeQuantity} ${nameFor(rule.freeItemId)} free',
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
                                (_) => AddEditBogoPromoScreen(rule: rule),
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
        tooltip: 'Add Buy 1 Take 1 promo',
        child: const Icon(Icons.add),
      ),
    );
  }

  void _openAdd(BuildContext context) {
    Navigator.of(context).push<void>(
      MaterialPageRoute(builder: (_) => const AddEditBogoPromoScreen()),
    );
  }
}
