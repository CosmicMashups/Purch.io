import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/db/db_providers.dart';
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

  /// Sets the cart's fulfillment choice (e.g. "Dine In"/"Take Out") — shown
  /// to restaurant/cafe tenants only, but the underlying field is generic.
  Future<bool> setOrderType(String orderType) => _mutate(
    (repository) => repository.setOrderType(SetOrderTypeRequest(orderType: orderType)),
  );

  /// Replaces the current cart with a pending kiosk order the cashier just
  /// claimed, so the normal checkout flow (payment, receipt) picks it up as
  /// if it were this device's own in-progress sale.
  Future<bool> claimKioskOrder(String transactionId) => _mutate(
    (repository) => repository.claimKioskOrder(transactionId),
  );

  /// Records a full payment. On success the cart moves to Completed with its
  /// receipt number — the caller shows that as a receipt before calling
  /// [startNewSale] to fetch the fresh cart that replaces it.
  ///
  /// Also caches the issued receipt number in DeviceIdentity, so this
  /// device's local record of "what number did I last issue" never falls
  /// behind — see that table's doc comment for why that matters for BIR's
  /// sequential-numbering requirement. Fire-and-forget: it's a local cache
  /// update, not part of the sale itself, so it never affects whether this
  /// call reports success or delays showing the receipt.
  Future<bool> recordPayment(RecordPaymentRequest request) async {
    final succeeded = await _mutate(
      (repository) => repository.recordPayment(request),
    );

    final receiptNumber = state.value?.receiptNumber;
    if (succeeded && receiptNumber != null) {
      unawaited(
        ref
            .read(deviceIdentityDaoProvider)
            .recordIssuedReceiptNumber(receiptNumber)
            .catchError((_) {}),
      );
    }

    return succeeded;
  }

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

/// Kiosk orders submitted and awaiting a cashier to collect payment for this
/// device's branch — read fresh each time the Pending Kiosk Orders screen
/// opens (no live push; a manual refresh/reopen is enough for this use case).
@riverpod
Future<List<Transaction>> pendingKioskOrders(Ref ref) async {
  final identity = await ref.watch(deviceIdentityDaoProvider).getIdentity();
  if (identity == null) {
    return const [];
  }
  return ref
      .watch(posRepositoryProvider)
      .listPendingKioskOrders(identity.branchId);
}
