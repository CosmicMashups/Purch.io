import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

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
