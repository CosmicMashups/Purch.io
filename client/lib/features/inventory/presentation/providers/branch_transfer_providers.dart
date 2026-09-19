import '../../../../core/data/data_refresh.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/branch_transfer_repository_impl.dart';
import '../../domain/branch_transfer_models.dart';
import '../../domain/branch_transfer_repository.dart';

part 'branch_transfer_providers.g.dart';

@Riverpod(keepAlive: true)
BranchTransferRepository branchTransferRepository(Ref ref) {
  return BranchTransferRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

@riverpod
class BranchTransferList extends _$BranchTransferList {
  @override
  Future<List<BranchTransfer>> build() {
    return ref.watch(branchTransferRepositoryProvider).listTransfers();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class CreateBranchTransferController extends _$CreateBranchTransferController {
  @override
  FutureOr<void> build() {}

  Future<bool> create(CreateBranchTransferRequest request) async {
    state = const AsyncLoading();
    final repository = ref.read(branchTransferRepositoryProvider);

    state = await AsyncValue.guard(() => repository.createTransfer(request));
    final succeeded = !state.hasError;
    if (succeeded) {
      refreshStockAndSalesData(ref);
      await ref.read(branchTransferListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// One controller per transfer (Riverpod family, inferred from the
/// `branchTransferId` parameter) — advancing transfer A's status can't be
/// confused with an in-flight action on transfer B.
@riverpod
class BranchTransferActionController extends _$BranchTransferActionController {
  @override
  FutureOr<void> build(String branchTransferId) {}

  Future<bool> markInTransit() =>
      _act((repository) => repository.markInTransit(branchTransferId));

  Future<bool> markReceived() =>
      _act((repository) => repository.markReceived(branchTransferId));

  Future<bool> _act(
    Future<BranchTransfer> Function(BranchTransferRepository) action,
  ) async {
    state = const AsyncLoading();
    final repository = ref.read(branchTransferRepositoryProvider);

    state = await AsyncValue.guard(() => action(repository));
    final succeeded = !state.hasError;
    if (succeeded) {
      refreshStockAndSalesData(ref);
      await ref.read(branchTransferListProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
