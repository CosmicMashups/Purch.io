import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/inventory_repository_impl.dart';
import '../../domain/inventory_movement_models.dart';
import '../../domain/inventory_repository.dart';

part 'inventory_providers.g.dart';

@Riverpod(keepAlive: true)
InventoryRepository inventoryRepository(Ref ref) {
  return InventoryRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

@riverpod
class InventoryDashboardNotifier extends _$InventoryDashboardNotifier {
  @override
  Future<InventoryDashboard> build() {
    return ref.watch(inventoryRepositoryProvider).getDashboard();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
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
    return !state.hasError;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
