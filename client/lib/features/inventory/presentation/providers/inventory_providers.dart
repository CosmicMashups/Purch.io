import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/data/data_refresh.dart';
import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import '../../data/inventory_repository_impl.dart';
import '../../domain/inventory_movement_models.dart';
import '../../domain/inventory_repository.dart';
import '../../domain/item_stock_cost_models.dart';
import 'purchase_order_providers.dart';

part 'inventory_providers.g.dart';

@Riverpod(keepAlive: true)
InventoryRepository inventoryRepository(Ref ref) {
  return InventoryRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

/// Backs the Home dashboard's "Running Low" chart. Short-lived cache (see
/// [CacheFor]) so scrolling the chart off-screen doesn't refetch, without
/// letting it go stale; stock writes invalidate it via refreshStockAndSalesData.
@riverpod
class InventoryDashboardNotifier extends _$InventoryDashboardNotifier {
  @override
  Future<InventoryDashboard> build() {
    ref.cacheFor(const Duration(seconds: 45));
    return ref.watch(inventoryRepositoryProvider).getDashboard();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

/// The Inventory tab's items table — name, category, available stock and
/// average cost. Composed from three already-cached lists rather than a new
/// endpoint; see [buildItemStockCostRows] for the average-cost method and its
/// limits.
@riverpod
Future<List<ItemStockCostRow>> itemStockCostRows(Ref ref) async {
  final items = await ref.watch(itemListProvider.future);
  final categories = await ref.watch(categoryListProvider.future);
  final purchaseOrders = await ref.watch(purchaseOrderListProvider.future);

  return buildItemStockCostRows(
    items: items,
    categories: categories,
    purchaseOrders: purchaseOrders,
  );
}

/// One list per filter combination (Riverpod family, inferred from the
/// named parameters) — C2's movement type filter chips just watch a
/// different instance of this provider rather than re-filtering client-side.
@riverpod
class MovementLog extends _$MovementLog {
  @override
  Future<List<InventoryMovement>> build({
    String? itemId,
    String? branchId,
    MovementType? type,
  }) {
    return ref
        .watch(inventoryRepositoryProvider)
        .listMovements(itemId: itemId, branchId: branchId, type: type);
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class RecordMovementController extends _$RecordMovementController {
  @override
  FutureOr<void> build() {}

  Future<bool> record(RecordMovementRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(inventoryRepositoryProvider);

    state = await AsyncValue.guard(() => repository.recordMovement(request));
    final succeeded = !state.hasError;
    if (succeeded) {
      refreshStockAndSalesData(ref);
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
