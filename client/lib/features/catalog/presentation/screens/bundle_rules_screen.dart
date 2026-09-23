import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/formatting/money.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/empty_state_view.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../providers/catalog_providers.dart';
import 'add_bundle_rule_screen.dart';
import '../../../../core/errors/failure.dart';

/// B2b — bundle promo rules. Only reachable for items whose pricingType is
/// bundle (see ItemListScreen), matching the backend's own rejection of
/// bundle rules against any other pricing type.
class BundleRulesScreen extends ConsumerWidget {
  const BundleRulesScreen({
    super.key,
    required this.itemId,
    required this.itemName,
  });

  final String itemId;
  final String itemName;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final rulesAsync = ref.watch(bundleRuleListProvider(itemId));

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text('Bundle Rules: $itemName'),
        backgroundColor: AppColors.surface,
        elevation: 0,
      ),
      body: rulesAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error:
            (error, stackTrace) => ErrorStateView(
              message: 'Could not load bundle rules: ${describeError(error)}',
              onRetry:
                  () =>
                      ref.read(bundleRuleListProvider(itemId).notifier).refresh(),
            ),
        data: (rules) {
          if (rules.isEmpty) {
            return EmptyStateView(
              icon: Icons.inventory_2_outlined,
              title: 'No bundle rules yet — tap + to add one.',
              description:
                  'Offer bulk savings (e.g. Buy 3 for ₱100) calculated automatically at checkout.',
              actionLabel: 'Add Bundle Rule',
              onAction:
                  () => Navigator.of(context).push<void>(
                    MaterialPageRoute(
                      builder:
                          (_) => AddBundleRuleScreen(
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
                    ref.read(bundleRuleListProvider(itemId).notifier).refresh(),
            child: ListView.builder(
              padding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg, vertical: AppSpacing.md),
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
                    contentPadding: const EdgeInsets.symmetric(horizontal: AppSpacing.lg, vertical: AppSpacing.xs),
                    leading: Container(
                      padding: const EdgeInsets.all(8),
                      decoration: const BoxDecoration(
                        color: AppColors.brandPrimaryContainer,
                        shape: BoxShape.circle,
                      ),
                      child: const Icon(Icons.local_offer, size: 18, color: AppColors.brandPrimary),
                    ),
                    title: Text(
                      rule.description,
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    subtitle: Text(
                      'Buy ${rule.triggerQuantity} for ${formatCurrency(rule.bundlePrice)}'
                      '${rule.isActive ? '' : ' · inactive'}',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: AppColors.textSecondary,
                        fontFeatures: const [FontFeature.tabularFigures()],
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
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(
                builder:
                    (_) =>
                        AddBundleRuleScreen(itemId: itemId, itemName: itemName),
              ),
            ),
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: Colors.white,
        tooltip: 'Add bundle rule',
        child: const Icon(Icons.add),
      ),
    );
  }
}
