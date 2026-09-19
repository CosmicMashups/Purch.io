import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/bir_reading_repository_impl.dart';
import '../../data/sale_queue.dart';
import '../../domain/bir_reading_models.dart';
import '../../domain/bir_reading_repository.dart';
import 'pos_providers.dart';

part 'bir_reading_providers.g.dart';

@Riverpod(keepAlive: true)
BirReadingRepository birReadingRepository(Ref ref) {
  return BirReadingRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

/// Holds the most recently generated reading, if any — generating is an
/// explicit action (unlike CartNotifier/CurrentShiftNotifier, there's
/// nothing to auto-fetch on first watch).
@riverpod
class GenerateBirReadingController extends _$GenerateBirReadingController {
  @override
  FutureOr<BirReading?> build() => null;

  Future<bool> generateXReading() =>
      _generate((repository) => repository.generateXReading());

  /// The end-of-day reading closes the day's numbers, so it must not run while
  /// this terminal still holds sales that were completed offline and haven't
  /// reached the server — they would be missing from it. Try to send them
  /// first; if any are still waiting, stop and say so.
  Future<bool> generateZReading() async {
    final waiting = await _unsyncedOfflineSales();
    if (waiting > 0) {
      state = AsyncError(
        ConflictFailure(
          '$waiting sale${waiting == 1 ? '' : 's'} completed offline '
          '${waiting == 1 ? "hasn't" : "haven't"} synced to the server yet. '
          'Reconnect and let them sync before running the Z-reading, so they '
          'are included in it.',
        ),
        StackTrace.current,
      );
      return false;
    }
    return _generate((repository) => repository.generateZReading());
  }

  Future<int> _unsyncedOfflineSales() async {
    final store = ref.read(saleQueueStoreProvider);
    try {
      var stats = SaleQueueStats.of(await store.reviewable());
      if (stats.unsynced > 0) {
        await ref.read(saleSyncCoordinatorProvider).drain();
        stats = SaleQueueStats.of(await store.reviewable());
      }
      return stats.unsynced;
    } on StateError {
      // No terminal identity yet (e.g. an admin back-office session): nothing
      // can be queued on this device.
      return 0;
    }
  }

  Future<bool> _generate(
    Future<BirReading> Function(BirReadingRepository) action,
  ) async {
    final repository = ref.read(birReadingRepositoryProvider);

    state = const AsyncLoading();
    final next = await AsyncValue.guard(() => action(repository));
    state = next;

    return !next.hasError;
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}
