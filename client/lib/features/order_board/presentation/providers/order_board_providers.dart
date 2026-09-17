import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/order_board_repository_impl.dart';
import '../../domain/order_board_repository.dart';
import '../../../pos/domain/transaction_models.dart';

part 'order_board_providers.g.dart';

@Riverpod(keepAlive: true)
OrderBoardRepository orderBoardRepository(Ref ref) {
  return OrderBoardRepositoryImpl(
    apiClient: ref.watch(apiClientProvider),
    tokenStorage: ref.watch(secureTokenStorageProvider),
  );
}

/// Drives the pairing screen, same shape as KioskPairingController.
@riverpod
class OrderBoardPairingController extends _$OrderBoardPairingController {
  @override
  FutureOr<void> build() {}

  Future<void> pair(String devicePairingCode, String pairingPin) async {
    state = const AsyncLoading();
    final repository = ref.read(orderBoardRepositoryProvider);

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

/// Polled every few seconds by the display screen rather than pushed live
/// (no WebSocket server on this simple read-only surface) — good enough for
/// a board that just needs to stay roughly current.
@riverpod
Future<List<Transaction>> orderBoardPendingOrders(Ref ref) async {
  final repository = ref.watch(orderBoardRepositoryProvider);
  final branchId = await repository.currentBranchId();
  if (branchId == null) {
    return const [];
  }
  return repository.listPendingOrders(branchId);
}
