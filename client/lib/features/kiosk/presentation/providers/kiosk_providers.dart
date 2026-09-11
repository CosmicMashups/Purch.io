import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../../pos/domain/transaction_models.dart';
import '../../data/kiosk_cart_repository_impl.dart';
import '../../data/kiosk_session_repository_impl.dart';
import '../../domain/kiosk_cart_repository.dart';
import '../../domain/kiosk_session_repository.dart';

part 'kiosk_providers.g.dart';

@Riverpod(keepAlive: true)
KioskSessionRepository kioskSessionRepository(Ref ref) {
  return KioskSessionRepositoryImpl(
    apiClient: ref.watch(apiClientProvider),
    tokenStorage: ref.watch(secureTokenStorageProvider),
  );
}

@Riverpod(keepAlive: true)
KioskCartRepository kioskCartRepository(Ref ref) {
  return KioskCartRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

/// Drives the pairing screen: call `pair(...)`, watch this provider's
/// AsyncValue for loading/error state. Mirrors LoginController's shape.
@riverpod
class KioskPairingController extends _$KioskPairingController {
  @override
  FutureOr<void> build() {}

  Future<void> pair(String devicePairingCode) async {
    state = const AsyncLoading();
    final repository = ref.read(kioskSessionRepositoryProvider);

    state = await AsyncValue.guard(
      () => repository.pair(devicePairingCode: devicePairingCode),
    );
  }

  Failure? get currentFailure {
    final error = state.error;
    return error is Failure ? error : null;
  }
}

/// The kiosk terminal's single in-progress order — same shape as CartNotifier
/// on the POS side, minus everything (void/payment/discount/promo) a kiosk
/// order can't do.
@riverpod
class KioskCartNotifier extends _$KioskCartNotifier {
  @override
  Future<Transaction> build() {
    return ref.watch(kioskCartRepositoryProvider).getOrCreateOpenCart();
  }

  Future<bool> addLine(AddTransactionLineRequest request) =>
      _mutate((repository) => repository.addLine(request));

  Future<bool> updateLine(
    String lineId,
    UpdateTransactionLineRequest request,
  ) => _mutate((repository) => repository.updateLine(lineId, request));

  Future<bool> removeLine(String lineId) =>
      _mutate((repository) => repository.removeLine(lineId));

  Future<bool> setOrderType(String orderType) => _mutate(
    (repository) =>
        repository.setOrderType(SetOrderTypeRequest(orderType: orderType)),
  );

  /// Submits the order (E6), then starts a fresh cart for the next customer.
  /// Returns the just-submitted order (with its prep number) on success, or
  /// null on failure — the caller shows the failure via [currentFailure].
  Future<Transaction?> submitOrder() async {
    final repository = ref.read(kioskCartRepositoryProvider);

    state = const AsyncLoading();
    final submitted = await AsyncValue.guard(() => repository.submitOrder());
    if (submitted.hasError) {
      state = submitted;
      return null;
    }

    ref.invalidateSelf();
    await future;
    return submitted.value;
  }

  Future<bool> _mutate(
    Future<Transaction> Function(KioskCartRepository) action,
  ) async {
    final repository = ref.read(kioskCartRepositoryProvider);

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
