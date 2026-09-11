import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

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
        loading: () => const Center(child: CircularProgressIndicator()),
        error:
            (error, stackTrace) =>
                Center(child: Text('Could not load the dashboard: $error')),
        data: (dashboard) {
          return RefreshIndicator(
            onRefresh:
                () =>
                    ref
                        .read(inventoryDashboardNotifierProvider.notifier)
                        .refresh(),
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Row(
                  children: [
                    Expanded(
                      child: _StatCard(
                        label: 'Total SKUs',
                        value: '${dashboard.totalSkus}',
                        icon: Icons.inventory_2,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: _StatCard(
                        label: 'Low Stock',
                        value: '${dashboard.lowStockCount}',
                        icon: Icons.warning_amber,
                        color: Colors.orange,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: _StatCard(
                        label: 'Out of Stock',
                        value: '${dashboard.outOfStockCount}',
                        icon: Icons.remove_shopping_cart,
                        color: Colors.red,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 24),
                Text(
                  'Low Stock Alerts',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 8),
                if (dashboard.lowStockItems.isEmpty)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 16),
                    child: Text('Nothing is running low right now.'),
                  )
                else
                  for (final alert in dashboard.lowStockItems)
                    Card(
                      child: ListTile(
                        leading: const Icon(
                          Icons.warning_amber,
                          color: Colors.orange,
                        ),
                        title: Text(alert.itemName),
                        subtitle: Text(
                          '${alert.stockOnHand.toStringAsFixed(0)} left · '
                          'alert at ${alert.lowStockThreshold.toStringAsFixed(0)}',
                        ),
                        trailing: TextButton(
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
    this.color,
  });

  final String label;
  final String value;
  final IconData icon;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 20, horizontal: 12),
        child: Column(
          children: [
            Icon(icon, color: color),
            const SizedBox(height: 8),
            Text(value, style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 4),
            Text(
              label,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ],
        ),
      ),
    );
  }
}
