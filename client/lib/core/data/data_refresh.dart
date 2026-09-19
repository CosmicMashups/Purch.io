import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../features/catalog/presentation/providers/catalog_providers.dart';
import '../../features/inventory/presentation/providers/inventory_item_providers.dart';
import '../../features/inventory/presentation/providers/inventory_providers.dart';
import '../../features/reports/presentation/providers/reports_providers.dart';

/// Keeps an auto-dispose provider's data cached for [duration] after its last
/// listener leaves (or after it is built, while listened to), then lets it
/// dispose so the next read refetches. A short-lived cache instead of a
/// permanent `keepAlive`: charts scrolling off-screen and back don't refetch,
/// but data never goes stale for more than [duration] once nobody is watching.
extension CacheFor on Ref {
  void cacheFor(Duration duration) {
    final link = keepAlive();
    final timer = Timer(duration, link.close);
    onDispose(timer.cancel);
  }
}

/// Invalidates everything a stock or sales change can make stale, so the
/// Dashboard charts, Inventory lists and the movement log show the new state
/// without restarting the app. Call after any successful write that moves
/// stock or money: a completed sale, a recorded movement, a received purchase
/// order, a branch transfer, or a catalog/stock edit.
void refreshStockAndSalesData(Ref ref) => _invalidateStockAndSales(ref.invalidate);

/// Same as [refreshStockAndSalesData], for widget-level callers (a [WidgetRef]).
void refreshStockAndSalesDataFromWidget(WidgetRef ref) =>
    _invalidateStockAndSales(ref.invalidate);

void _invalidateStockAndSales(void Function(ProviderOrFamily) invalidate) {
  for (final provider in <ProviderOrFamily>[
    salesDashboardProvider,
    salesTrendProvider,
    categorySalesProvider,
    movementSummaryProvider,
    staffPerformanceProvider,
    departmentSalesProvider,
    inventoryDashboardNotifierProvider,
    movementLogProvider,
    itemListProvider, // itemStockCostRows derives from it
    inventoryItemListProvider,
  ]) {
    invalidate(provider);
  }
}
