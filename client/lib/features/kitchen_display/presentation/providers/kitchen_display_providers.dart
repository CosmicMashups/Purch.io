import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/kitchen_display_repository_impl.dart';
import '../../domain/kitchen_display_repository.dart';
import '../../../pos/domain/transaction_models.dart';

part 'kitchen_display_providers.g.dart';

@Riverpod(keepAlive: true)
KitchenDisplayRepository kitchenDisplayRepository(Ref ref) {
  return KitchenDisplayRepositoryImpl(
    apiClient: ref.watch(apiClientProvider),
    tokenStorage: ref.watch(secureTokenStorageProvider),
  );
}

/// Drives the pairing screen, same shape as KioskPairingController.
@riverpod
class KitchenDisplayPairingController extends _$KitchenDisplayPairingController {
  @override
  FutureOr<void> build() {}

  Future<void> pair(String devicePairingCode, String pairingPin) async {
    state = const AsyncLoading();
    final repository = ref.read(kitchenDisplayRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.pair(
        devicePairingCode: devicePairingCode,
        pairingPin: pairingPin,
      ),
    );
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// Polled every few seconds by the display screen, same rationale as
/// orderBoardPendingOrders.
@riverpod
Future<List<Transaction>> kitchenDisplayPendingOrders(Ref ref) async {
  final repository = ref.watch(kitchenDisplayRepositoryProvider);
  final branchId = await repository.currentBranchId();
  if (branchId == null) {
    return const [];
  }
  return repository.listPendingOrders(branchId);
}

/// Advances an order's kitchen status, then refreshes the pending list so the
/// board reflects it immediately rather than waiting for the next poll.
@riverpod
class KitchenStatusController extends _$KitchenStatusController {
  @override
  FutureOr<void> build() {}

  Future<bool> advance(String transactionId, KitchenStatus status) async {
    final repository = ref.read(kitchenDisplayRepositoryProvider);

    state = const AsyncLoading();
    final next = await AsyncValue.guard(
      () => repository.updateStatus(transactionId, status),
    );
    state = next;

    if (!next.hasError) {
      ref.invalidate(kitchenDisplayPendingOrdersProvider);
    }
    return !next.hasError;
  }
}
