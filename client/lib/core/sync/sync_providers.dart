import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../db/db_providers.dart';
import '../errors/failure.dart';
import '../../features/auth/presentation/providers/auth_providers.dart';
import 'sync_coordinator.dart';
import 'sync_dto.dart';
import 'sync_repository.dart';
import 'sync_repository_impl.dart';

part 'sync_providers.g.dart';

@Riverpod(keepAlive: true)
SyncRepository syncRepository(Ref ref) {
  return SyncRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

@Riverpod(keepAlive: true)
SyncCoordinator syncCoordinator(Ref ref) {
  final coordinator = SyncCoordinator(
    syncQueueDao: ref.watch(syncQueueDaoProvider),
    syncRepository: ref.watch(syncRepositoryProvider),
  );
  coordinator.start();
  ref.onDispose(coordinator.dispose);
  return coordinator;
}

@riverpod
class FlaggedSyncRecords extends _$FlaggedSyncRecords {
  @override
  Future<List<FlaggedSyncRecord>> build() {
    return ref.watch(syncRepositoryProvider).listFlagged();
  }

  Future<void> refresh() async {
    ref.invalidateSelf();
    await future;
  }
}

@riverpod
class AcknowledgeFlaggedController extends _$AcknowledgeFlaggedController {
  @override
  FutureOr<void> build() {}

  Future<bool> acknowledge(String syncedRecordId) async {
    state = const AsyncLoading();
    final repository = ref.read(syncRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.acknowledgeFlagged(syncedRecordId),
    );
    final succeeded = !state.hasError;
    if (succeeded) {
      await ref.read(flaggedSyncRecordsProvider.notifier).refresh();
    }
    return succeeded;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
