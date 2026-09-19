import '../../../../core/data/data_refresh.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/purchase_order_repository_impl.dart';
import '../../domain/purchase_order_models.dart';
import '../../domain/purchase_order_repository.dart';

part 'purchase_order_providers.g.dart';

@Riverpod(keepAlive: true)
PurchaseOrderRepository purchaseOrderRepository(Ref ref) {
  return PurchaseOrderRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

@riverpod
class PurchaseOrderList extends _$PurchaseOrderList {
  @override
  Future<List<PurchaseOrder>> build() {
    return ref.watch(purchaseOrderRepositoryProvider).listPurchaseOrders();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreatePurchaseOrderController extends _$CreatePurchaseOrderController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreatePurchaseOrderRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(purchaseOrderRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.createPurchaseOrder(request),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      refreshStockAndSalesData(ref);
      await ref.read(purchaseOrderListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// One controller per PO (Riverpod family, inferred from the
/// `purchaseOrderId` parameter) — advancing PO A's status can't be confused
/// with an in-flight action on PO B.
@riverpod
class PurchaseOrderActionController extends _$PurchaseOrderActionController {
  @override
  FutureOr<void> build(String purchaseOrderId) {}

  Future<bool> markSent() =>
      _act((repository) => repository.markSent(purchaseOrderId));

  Future<bool> cancel() =>
      _act((repository) => repository.cancel(purchaseOrderId));

  Future<bool> receive(ReceivePurchaseOrderRequest request) =>
      _act((repository) => repository.receive(purchaseOrderId, request));

  Future<bool> _act(
    Future<PurchaseOrder> Function(PurchaseOrderRepository) action,
  ) async {
    state = const AsyncLoading();
    final repository = ref.read(purchaseOrderRepositoryProvider);

    state = await AsyncValue.guard(() => action(repository));
    final succeeded = !state.hasError;
    if (succeeded) {
      refreshStockAndSalesData(ref);
      await ref.read(purchaseOrderListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
