import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/error_state_view.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../providers/inventory_providers.dart';
import 'movement_log_screen.dart';
import 'record_movement_screen.dart';

/// C1 — stock overview cards + the low-stock alert list with a reorder
/// shortcut straight into C3's record-movement form. "Pending movements"
/// from PAGES.md isn't shown yet — it belongs to C4/C5, neither built yet.
class InventoryDashboardScreen extends ConsumerWidget {
  const InventoryDashboardScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final dashboardAsync = ref.watch(inventoryDashboardNotifierProvider);
    final itemsAsync = ref.watch(itemListProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Inventory Dashboard'),
        actions: [
          IconButton(
            onPressed:
                () => Navigator.of(context).push<void>(
                  MaterialPageRoute(builder: (_) => const MovementLogScreen()),
                ),
            icon: const Icon(Icons.history),
            tooltip: 'Movement log',
          ),
        ],
      ),
      body: dashboardAsync.when(
        loading: () => const Center(
          child: CircularProgressIndicator(color: AppColors.brandPrimary),
        ),
        error:
            (error, stackTrace) => ErrorStateView(
              message: error.toString(),
              onRetry:
                  () =>
                      ref
                          .read(inventoryDashboardNotifierProvider.notifier)
                          .refresh(),
            ),
        data: (dashboard) {
          return RefreshIndicator(
            color: AppColors.brandPrimary,
            onRefresh:
                () =>
                    ref
                        .read(inventoryDashboardNotifierProvider.notifier)
                        .refresh(),
            child: ListView(
              padding: const EdgeInsets.all(AppSpacing.lg),
              children: [
                Row(
                  children: [
                    Expanded(
                      child: _StatCard(
                        label: 'Total SKUs',
                        value: '${dashboard.totalSkus}',
                        icon: Icons.inventory_2,
                        accentColor: AppColors.brandPrimary,
                        containerColor: AppColors.brandPrimaryContainer,
                      ),
                    ),
                    const SizedBox(width: AppSpacing.md),
                    Expanded(
                      child: _StatCard(
                        label: 'Low Stock',
                        value: '${dashboard.lowStockCount}',
                        icon: Icons.warning_amber_rounded,
                        accentColor: AppColors.accentWarm,
                        containerColor: AppColors.accentWarmContainer,
                      ),
                    ),
                    const SizedBox(width: AppSpacing.md),
                    Expanded(
                      child: _StatCard(
                        label: 'Out of Stock',
                        value: '${dashboard.outOfStockCount}',
                        icon: Icons.remove_shopping_cart,
                        accentColor: AppColors.error,
                        containerColor: AppColors.cardHover,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: AppSpacing.xl),
                Text(
                  'Low Stock Alerts',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: AppSpacing.sm),
                if (dashboard.lowStockItems.isEmpty)
                  Container(
                    padding: const EdgeInsets.all(AppSpacing.xl),
                    decoration: BoxDecoration(
                      color: AppColors.surface,
                      borderRadius: AppRadius.lgBorder,
                      border: Border.all(color: AppColors.border),
                      boxShadow: AppShadows.subtle,
                    ),
                    child: const Center(
                      child: Text(
                        'Nothing is running low right now.',
                        style: TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w500,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ),
                  )
                else
                  for (final alert in dashboard.lowStockItems)
                    Container(
                      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
                      decoration: BoxDecoration(
                        color: AppColors.surface,
                        borderRadius: AppRadius.lgBorder,
                        border: Border.all(color: AppColors.border),
                        boxShadow: AppShadows.subtle,
                      ),
                      child: ListTile(
                        contentPadding: const EdgeInsets.symmetric(
                          horizontal: AppSpacing.md,
                          vertical: AppSpacing.xs,
                        ),
                        leading: Container(
                          width: 40,
                          height: 40,
                          decoration: BoxDecoration(
                            color: AppColors.accentWarmContainer,
                            borderRadius: BorderRadius.circular(AppRadius.sm),
                          ),
                          child: const Icon(
                            Icons.warning_amber_rounded,
                            color: AppColors.accentWarm,
                            size: 20,
                          ),
                        ),
                        title: Text(
                          alert.itemName,
                          style: const TextStyle(
                            fontWeight: FontWeight.w700,
                            color: AppColors.textPrimary,
                          ),
                        ),
                        subtitle: Text(
                          '${alert.stockOnHand.toStringAsFixed(0)} left · '
                          'alert at ${alert.lowStockThreshold.toStringAsFixed(0)}',
                          style: const TextStyle(
                            color: AppColors.textSecondary,
                            fontSize: 13,
                          ),
                        ),
                        trailing: OutlinedButton(
                          style: OutlinedButton.styleFrom(
                            side: const BorderSide(color: AppColors.brandPrimary),
                            foregroundColor: AppColors.brandPrimary,
                            shape: RoundedRectangleBorder(
                              borderRadius: AppRadius.mdBorder,
                            ),
                          ),
                          onPressed: () {
                            final items = itemsAsync.valueOrNull ?? const [];
                            Item? presetItem;
                            for (final item in items) {
                              if (item.id == alert.itemId) {
                                presetItem = item;
                                break;
                              }
                            }
                            Navigator.of(context).push<void>(
                              MaterialPageRoute(
                                builder:
                                    (_) => RecordMovementScreen(
                                      presetItem: presetItem,
                                    ),
                              ),
                            );
                          },
                          child: const Text('Reorder'),
                        ),
                      ),
                    ),
              ],
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(builder: (_) => const RecordMovementScreen()),
            ),
        tooltip: 'Record movement',
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: AppColors.onBrandPrimary,
        child: const Icon(Icons.add),
      ),
    );
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({
    required this.label,
    required this.value,
    required this.icon,
    required this.accentColor,
    required this.containerColor,
  });

  final String label;
  final String value;
  final IconData icon;
  final Color accentColor;
  final Color containerColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.lgBorder,
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.subtle,
      ),
      padding: const EdgeInsets.symmetric(
        vertical: AppSpacing.lg,
        horizontal: AppSpacing.md,
      ),
      child: Column(
        children: [
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: containerColor,
              borderRadius: BorderRadius.circular(AppRadius.full),
            ),
            child: Icon(icon, color: accentColor, size: 22),
          ),
          const SizedBox(height: AppSpacing.sm),
          Text(
            value,
            style: Theme.of(context).textTheme.headlineSmall?.copyWith(
              fontWeight: FontWeight.w800,
              color: AppColors.textPrimary,
              fontFeatures: const [FontFeature.tabularFigures()],
            ),
          ),
          const SizedBox(height: 4),
          Text(
            label,
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w500,
              color: AppColors.textSecondary,
            ),
          ),
        ],
      ),
    );
  }
}
