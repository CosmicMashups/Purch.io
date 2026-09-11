import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../data/pos_repository_impl.dart';
import '../../domain/pos_repository.dart';
import '../../domain/transaction_models.dart';

part 'pos_providers.g.dart';

@Riverpod(keepAlive: true)
PosRepository posRepository(Ref ref) {
  return PosRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

/// The current device's single in-progress cart — fetched (and created if
/// none exists yet) on first watch, then mutated in place by add/update/
/// remove/void so the UI never has to juggle a separate "action" provider.
@riverpod
class CartNotifier extends _$CartNotifier {
  @override
  Future<Transaction> build() {
    return ref.watch(posRepositoryProvider).getOrCreateOpenCart();
  }

  Future<bool> addLine(AddTransactionLineRequest request) =>
      _mutate((repository) => repository.addLine(request));

  Future<bool> updateLine(
    String lineId,
    UpdateTransactionLineRequest request,
  ) => _mutate((repository) => repository.updateLine(lineId, request));

  Future<bool> removeLine(String lineId) =>
      _mutate((repository) => repository.removeLine(lineId));

  /// Voids the current cart, then starts a fresh one — the caller never sees
  /// the voided transaction itself, only the empty cart that replaces it.
  Future<bool> voidCart() async {
    final repository = ref.read(posRepositoryProvider);

    state = const AsyncLoading();
    final voided = await AsyncValue.guard(() => repository.voidCart());
    if (voided.hasError) {
      state = voided;
      return false;
    }

    ref.invalidateSelf();
    await future;
    return true;
  }

  /// Toggles the Senior Citizen/PWD 20% discount — only after the cashier
  /// has verified the customer's physical ID themselves.
  Future<bool> applySeniorPwdDiscount(bool apply) => _mutate(
    (repository) => repository.applySeniorPwdDiscount(
      ApplySeniorPwdDiscountRequest(apply: apply),
    ),
  );

  /// Applies (or, with a null code, clears) a cart-level promo code.
  Future<bool> applyPromoCode(String? code) => _mutate(
    (repository) =>
        repository.applyPromoCode(ApplyPromoCodeRequest(code: code)),
  );

  /// Records a full payment. On success the cart moves to Completed with its
  /// receipt number — the caller shows that as a receipt before calling
  /// [startNewSale] to fetch the fresh cart that replaces it.
  Future<bool> recordPayment(RecordPaymentRequest request) =>
      _mutate((repository) => repository.recordPayment(request));

  /// Call once the completed sale's receipt has been shown/acknowledged.
  Future<void> startNewSale() async {
    ref.invalidateSelf();
    await future;
  }

  Future<bool> _mutate(
    Future<Transaction> Function(PosRepository) action,
  ) async {
    final repository = ref.read(posRepositoryProvider);

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
