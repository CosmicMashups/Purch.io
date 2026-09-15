import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/routing/nav_destinations.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/charts/chart_theme.dart';
import '../../../catalog/domain/item_models.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../../inventory/domain/inventory_movement_models.dart';
import '../../../inventory/domain/item_stock_cost_models.dart';
import '../../../inventory/presentation/providers/inventory_providers.dart';
import '../../../inventory/presentation/screens/record_movement_screen.dart';

/// Inventory tab: the stock picture first, navigation second.
///
/// The standalone Inventory Dashboard screen was folded in here — its stat
/// cards and low-stock alerts now open this page, followed by the items table
/// (name, category, available stock, average cost). Catalog/Stock/Purchasing
/// stay reachable but demoted to a compact secondary strip, because a manager
/// opening this tab almost always wants to know the stock position, not to
/// navigate.
class InventoryTabScreen extends ConsumerWidget {
  const InventoryTabScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final dashboardAsync = ref.watch(inventoryDashboardNotifierProvider);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Inventory')),
      body: RefreshIndicator(
        color: AppColors.brandPrimary,
        onRefresh: () async {
          await ref.read(inventoryDashboardNotifierProvider.notifier).refresh();
          ref.invalidate(itemStockCostRowsProvider);
        },
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.lg),
          children: [
            _StatCardRow(dashboard: dashboardAsync),
            const SizedBox(height: AppSpacing.xl),
            const _SectionHeader(title: 'Low stock alerts'),
            _LowStockAlerts(dashboard: dashboardAsync),
            const SizedBox(height: AppSpacing.xl),
            const _SectionHeader(title: 'Items'),
            const _ItemsTable(),
            const SizedBox(height: AppSpacing.xl),
            const _SectionHeader(title: 'Manage'),
            const _SecondaryNavStrip(),
            const SizedBox(height: AppSpacing.xl),
          ],
        ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed:
            () => Navigator.of(context).push<void>(
              MaterialPageRoute(builder: (_) => const RecordMovementScreen()),
            ),
        tooltip: 'Record movement',
        backgroundColor: AppColors.brandPrimary,
        foregroundColor: AppColors.onBrandPrimary,
        icon: const Icon(Icons.add),
        label: const Text('Record movement'),
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppSpacing.sm),
      child: Text(title.toUpperCase(), style: AppTypography.sectionLabel),
    );
  }
}

// ---------------------------------------------------------------------------
// Stat cards (folded in from InventoryDashboardScreen)
// ---------------------------------------------------------------------------

class _StatCardRow extends ConsumerWidget {
  const _StatCardRow({required this.dashboard});

  final AsyncValue<InventoryDashboard> dashboard;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final data = dashboard.valueOrNull;

    if (dashboard.hasError && data == null) {
      return _InlineError(
        message:
            'Stock overview unavailable: ${describeError(dashboard.error!)}',
        onRetry:
            () =>
                ref.read(inventoryDashboardNotifierProvider.notifier).refresh(),
      );
    }

    return Row(
      children: [
        Expanded(
          child: _StatCard(
            label: 'Total SKUs',
            value: data == null ? null : '${data.totalSkus}',
            icon: Icons.inventory_2,
            accentColor: AppColors.brandPrimary,
            containerColor: AppColors.brandPrimaryContainer,
          ),
        ),
        const SizedBox(width: AppSpacing.md),
        Expanded(
          child: _StatCard(
            label: 'Low Stock',
            value: data == null ? null : '${data.lowStockCount}',
            icon: Icons.warning_amber_rounded,
            accentColor: AppColors.accentWarm,
            containerColor: AppColors.accentWarmContainer,
          ),
        ),
        const SizedBox(width: AppSpacing.md),
        Expanded(
          child: _StatCard(
            label: 'Out of Stock',
            value: data == null ? null : '${data.outOfStockCount}',
            icon: Icons.remove_shopping_cart,
            accentColor: AppColors.error,
            containerColor: AppColors.cardHover,
          ),
        ),
      ],
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

  /// Null renders the loading placeholder — the card keeps its size so the
  /// page doesn't reflow when the numbers land.
  final String? value;
  final String label;
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
          SizedBox(
            height: 30,
            child:
                value == null
                    ? Center(
                      child: Container(
                        width: 40,
                        height: 16,
                        decoration: BoxDecoration(
                          color: AppColors.borderSubtle,
                          borderRadius: BorderRadius.circular(4),
                        ),
                      ),
                    )
                    : Text(
                      value!,
                      style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                        fontWeight: FontWeight.w800,
                        color: AppColors.textPrimary,
                        fontFeatures: const [FontFeature.tabularFigures()],
                      ),
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

// ---------------------------------------------------------------------------
// Low-stock alerts (folded in from InventoryDashboardScreen)
// ---------------------------------------------------------------------------

class _LowStockAlerts extends ConsumerWidget {
  const _LowStockAlerts({required this.dashboard});

  final AsyncValue<InventoryDashboard> dashboard;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final data = dashboard.valueOrNull;
    final itemsAsync = ref.watch(itemListProvider);

    if (data == null) {
      if (dashboard.hasError) {
        return const SizedBox.shrink();
      }
      return const _PanelPlaceholder(height: 96);
    }

    final alerts = data.lowStockItems;
    if (alerts.isEmpty) {
      return _Panel(
        child: Row(
          children: [
            const Icon(
              Icons.check_circle_outline_rounded,
              color: AppColors.accentEmerald,
            ),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: Text(
                'Nothing is running low right now.',
                style: AppTypography.body.copyWith(
                  color: AppColors.textSecondary,
                ),
              ),
            ),
          ],
        ),
      );
    }

    return Column(
      children: [
        for (final alert in alerts)
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
                  shape: const RoundedRectangleBorder(
                    borderRadius: AppRadius.mdBorder,
                  ),
                ),
                onPressed: () {
                  final items = itemsAsync.valueOrNull ?? const <Item>[];
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
                          (_) => RecordMovementScreen(presetItem: presetItem),
                    ),
                  );
                },
                child: const Text('Reorder'),
              ),
            ),
          ),
      ],
    );
  }
}

// ---------------------------------------------------------------------------
// Items table
// ---------------------------------------------------------------------------

class _ItemsTable extends ConsumerWidget {
  const _ItemsTable();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final rowsAsync = ref.watch(itemStockCostRowsProvider);

    return rowsAsync.when(
      loading: () => const _PanelPlaceholder(height: 180),
      error:
          (error, _) => _InlineError(
            message: 'Items unavailable: ${describeError(error)}',
            onRetry: () => ref.invalidate(itemStockCostRowsProvider),
          ),
      data: (rows) {
        if (rows.isEmpty) {
          return _Panel(
            child: Row(
              children: [
                const Icon(
                  Icons.inventory_2_outlined,
                  color: AppColors.textMuted,
                ),
                const SizedBox(width: AppSpacing.md),
                Expanded(
                  child: Text(
                    'No items in the catalog yet. Add one under Manage → Items '
                    'and it will show up here with its stock and cost.',
                    style: AppTypography.bodySm,
                  ),
                ),
              ],
            ),
          );
        }

        return _Panel(
          padding: EdgeInsets.zero,
          child: Column(
            children: [
              const _ItemsTableHeader(),
              for (var i = 0; i < rows.length; i++) ...[
                if (i > 0) const Divider(height: 1, color: AppColors.border),
                _ItemsTableRow(row: rows[i]),
              ],
            ],
          ),
        );
      },
    );
  }
}

const _nameFlex = 4;
const _categoryFlex = 3;
const _stockFlex = 2;
const _costFlex = 3;

class _ItemsTableHeader extends StatelessWidget {
  const _ItemsTableHeader();

  @override
  Widget build(BuildContext context) {
    Widget cell(String label, int flex, {TextAlign align = TextAlign.left}) =>
        Expanded(
          flex: flex,
          child: Text(
            label.toUpperCase(),
            textAlign: align,
            style: AppTypography.sectionLabel.copyWith(fontSize: 11),
          ),
        );

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.lg,
        vertical: AppSpacing.md,
      ),
      decoration: const BoxDecoration(color: AppColors.cardHover),
      child: Row(
        children: [
          cell('Item', _nameFlex),
          cell('Category', _categoryFlex),
          cell('Available', _stockFlex, align: TextAlign.right),
          cell('Avg. cost', _costFlex, align: TextAlign.right),
        ],
      ),
    );
  }
}

class _ItemsTableRow extends StatelessWidget {
  const _ItemsTableRow({required this.row});

  final ItemStockCostRow row;

  @override
  Widget build(BuildContext context) {
    final isOut = row.availableStock <= 0;

    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.lg,
        vertical: AppSpacing.md,
      ),
      child: Row(
        children: [
          Expanded(
            flex: _nameFlex,
            child: Text(
              row.itemName,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: AppTypography.labelMd,
            ),
          ),
          Expanded(
            flex: _categoryFlex,
            child: Text(
              row.categoryName,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: AppTypography.bodySm,
            ),
          ),
          Expanded(
            flex: _stockFlex,
            child: Text(
              row.availableStock.toStringAsFixed(
                row.availableStock.truncateToDouble() == row.availableStock
                    ? 0
                    : 2,
              ),
              textAlign: TextAlign.right,
              style: AppTypography.priceLine.copyWith(
                fontSize: 14,
                color: isOut ? AppColors.error : AppColors.textPrimary,
              ),
            ),
          ),
          Expanded(
            flex: _costFlex,
            child:
                row.averageCost == null
                    ? Tooltip(
                      message:
                          'No received purchase-order line has quoted a cost '
                          'for this item yet.',
                      child: Text(
                        '— no receipts',
                        textAlign: TextAlign.right,
                        style: AppTypography.bodySm,
                      ),
                    )
                    : Tooltip(
                      message:
                          'Quantity-weighted mean of expected unit cost across '
                          '${row.costSampleCount} received purchase-order '
                          'line${row.costSampleCount == 1 ? '' : 's'}.',
                      child: Text(
                        ChartTheme.peso(row.averageCost!),
                        textAlign: TextAlign.right,
                        style: AppTypography.priceLine.copyWith(fontSize: 14),
                      ),
                    ),
          ),
        ],
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Secondary navigation
// ---------------------------------------------------------------------------

/// Catalog / Stock / Purchasing, demoted from full-width tiles to a compact
/// chip strip now that the stock picture leads the page.
class _SecondaryNavStrip extends StatelessWidget {
  const _SecondaryNavStrip();

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final section in inventorySections) ...[
          Padding(
            padding: const EdgeInsets.only(
              top: AppSpacing.sm,
              bottom: AppSpacing.sm,
            ),
            child: Text(section.title, style: AppTypography.bodySm),
          ),
          Wrap(
            spacing: AppSpacing.sm,
            runSpacing: AppSpacing.sm,
            children: [
              for (final tile in section.tiles)
                _NavChip(
                  icon: tile.icon,
                  label: tile.label,
                  onTap: () => context.push('/inventory/${tile.path}'),
                ),
            ],
          ),
        ],
      ],
    );
  }
}

class _NavChip extends StatelessWidget {
  const _NavChip({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.card,
      borderRadius: BorderRadius.circular(AppRadius.md),
      child: InkWell(
        borderRadius: BorderRadius.circular(AppRadius.md),
        onTap: onTap,
        child: Container(
          // Stays at the 48dp touch-target floor the token system sets.
          constraints: const BoxConstraints(minHeight: 48),
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.lg,
            vertical: AppSpacing.md,
          ),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppRadius.md),
            border: Border.all(color: AppColors.border),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon, size: 18, color: AppColors.brandPrimary),
              const SizedBox(width: AppSpacing.sm),
              Text(label, style: AppTypography.labelMd),
            ],
          ),
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------------------
// Shared small pieces
// ---------------------------------------------------------------------------

class _Panel extends StatelessWidget {
  const _Panel({
    required this.child,
    this.padding = const EdgeInsets.all(AppSpacing.lg),
  });

  final Widget child;
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: padding,
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.lgBorder,
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.subtle,
      ),
      clipBehavior: Clip.antiAlias,
      child: child,
    );
  }
}

class _PanelPlaceholder extends StatelessWidget {
  const _PanelPlaceholder({required this.height});

  final double height;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: height,
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.lgBorder,
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.all(AppSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (var i = 0; i < 3; i++) ...[
            if (i > 0) const SizedBox(height: AppSpacing.md),
            Container(
              height: 12,
              width: i.isEven ? 220 : 160,
              decoration: BoxDecoration(
                color: AppColors.borderSubtle,
                borderRadius: BorderRadius.circular(4),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _InlineError extends StatelessWidget {
  const _InlineError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(AppSpacing.lg),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: AppRadius.lgBorder,
        border: Border.all(color: AppColors.error.withValues(alpha: 0.3)),
      ),
      child: Row(
        children: [
          const Icon(
            Icons.error_outline_rounded,
            size: 20,
            color: AppColors.error,
          ),
          const SizedBox(width: AppSpacing.md),
          Expanded(child: Text(message, style: AppTypography.bodySm)),
          TextButton(onPressed: onRetry, child: const Text('Try Again')),
        ],
      ),
    );
  }
}
