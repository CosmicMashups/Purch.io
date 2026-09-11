import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/shift_repository_impl.dart';
import '../../domain/shift_models.dart';
import '../../domain/shift_repository.dart';

part 'shift_providers.g.dart';

@Riverpod(keepAlive: true)
ShiftRepository shiftRepository(Ref ref) {
  return ShiftRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

/// D7 — the current device's cash-drawer session, null when none is open.
/// A closed shift stays in state as a summary (so the cashier sees the
/// reconciliation result) until [acknowledgeClosedShift] fetches the fresh
/// (null) state — same "show the result, then explicitly move on" pattern
/// as CartNotifier's recordPayment/startNewSale.
@riverpod
class CurrentShiftNotifier extends _$CurrentShiftNotifier {
  @override
  Future<Shift?> build() {
    return ref.watch(shiftRepositoryProvider).getCurrentShift();
  }

  Future<bool> openShift(OpenShiftRequest request) =>
      _mutate((repository) => repository.openShift(request));

  Future<bool> closeShift(CloseShiftRequest request) =>
      _mutate((repository) => repository.closeShift(request));

  Future<void> acknowledgeClosedShift() async {
    ref.invalidateSelf();
    await future;
  }

  Future<bool> _mutate(Future<Shift> Function(ShiftRepository) action) async {
    final repository = ref.read(shiftRepositoryProvider);

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
