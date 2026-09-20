import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../../../core/auth/jwt_claims.dart';
import '../../../../core/data/data_refresh.dart';
import '../../../../core/db/db_providers.dart';
import '../../../../core/errors/failure.dart';
import '../../../auth/presentation/providers/auth_providers.dart';
import '../../../catalog/presentation/providers/catalog_providers.dart';
import 'package:connectivity_plus/connectivity_plus.dart';

import '../../data/drift_cart_draft_store.dart';
import '../../data/drift_sale_queue_store.dart';
import '../../data/local_first_pos_repository.dart';
import '../../data/sale_queue.dart';
import '../../data/sale_sync_coordinator.dart';
import '../../data/pos_repository_impl.dart';
import '../../domain/item_promo_models.dart';
import '../../domain/pricing_engine.dart';
import '../../domain/promo_code_models.dart';
import 'item_promo_providers.dart';
import 'promo_code_providers.dart';
import '../../domain/pos_repository.dart';
import '../../domain/transaction_models.dart';

part 'pos_providers.g.dart';

/// The server half of the POS: what the local-first repository and the offline
/// sale sync both talk to.
@Riverpod(keepAlive: true)
PosRepository posRemoteRepository(Ref ref) {
  return PosRepositoryImpl(apiClient: ref.watch(apiClientProvider));
}

/// Durable queue of sales completed while offline (SQLite, tenant + terminal scoped).
@Riverpod(keepAlive: true)
SaleQueueStore saleQueueStore(Ref ref) {
  return DriftSaleQueueStore(
    dao: ref.watch(queuedSaleDaoProvider),
    identityDao: ref.watch(deviceIdentityDaoProvider),
  );
}

/// Sends queued offline sales to the server whenever it can. Started once by
/// the app shell, for the life of the signed-in session.
@Riverpod(keepAlive: true)
SaleSyncCoordinator saleSyncCoordinator(Ref ref) {
  final coordinator = SaleSyncCoordinator(
    store: ref.watch(saleQueueStoreProvider),
    remote: ref.watch(posRemoteRepositoryProvider),
    connectivity: Connectivity(),
    // Whatever just synced changed stock, movements and every sales chart.
    onSynced: () => refreshStockAndSalesData(ref),
  );
  coordinator.start();
  ref.onDispose(coordinator.dispose);
  return coordinator;
}

/// The unsent and rejected offline sales, live — drives the Cashier's banner
/// and the review sheet.
@riverpod
Stream<List<SaleQueueEntry>> offlineSales(Ref ref) {
  return ref.watch(saleQueueStoreProvider).watchReviewable();
}

/// The Cashier's repository is local-first: the cart lives in on-device SQLite
/// and only reaches the server when the sale is paid (see
/// [LocalFirstPosRepository]). [PosRepositoryImpl] is the server half.
@Riverpod(keepAlive: true)
PosRepository posRepository(Ref ref) {
  final identityDao = ref.watch(deviceIdentityDaoProvider);
  final remote = ref.watch(posRemoteRepositoryProvider);
  int? serverFloor;
  return LocalFirstPosRepository(
    remote: remote,
    saleQueue: ref.watch(saleQueueStoreProvider),
    isConnected: () async {
      final results = await Connectivity().checkConnectivity();
      return !results.contains(ConnectivityResult.none);
    },
    drainQueue: () => ref.read(saleSyncCoordinatorProvider).drain(),
    catalog: ref.watch(catalogRepositoryProvider),
    loadItems: () => ref.read(itemListProvider.future),
    currentStaffId: () async {
      final token = await ref.read(secureTokenStorageProvider).readAccessToken();
      return token == null ? null : staffIdFromJwt(token);
    },
    refreshCatalog: () async {
      ref.invalidate(itemListProvider);
      await ref.read(itemListProvider.future);
    },
    loadRules: () async {
      final promos = ref.read(itemPromoRepositoryProvider);
      final results = await Future.wait([
        promos.listBogoPromoRules(),
        promos.listComboPromoRules(),
        promos.listItemDiscountPromoRules(),
        ref.read(promoCodeRepositoryProvider).listPromoCodes(),
      ]);
      return PricingRules(
        bogo: results[0] as List<BogoPromoRule>,
        combo: results[1] as List<ComboPromoRule>,
        itemDiscount: results[2] as List<ItemDiscountPromoRule>,
        promoCodes: results[3] as List<PromoCode>,
      );
    },
    store: DriftCartDraftStore(
      dao: ref.watch(localCartDraftDaoProvider),
      identityDao: identityDao,
    ),
    lastIssuedReceiptNumber: ({bool refresh = false}) async {
      // The server's record is the floor (it survives a reinstall or a wiped
      // app); this device's own counter covers anything it issued since. If
      // the server can't be reached, number from the local counter alone.
      if (refresh || serverFloor == null) {
        try {
          serverFloor = await remote.getLastIssuedReceiptNumber();
        } on Object {
          serverFloor ??= 0;
        }
      }
      final local = (await identityDao.getIdentity())?.lastKnownReceiptNumber ?? 0;
      return local > serverFloor! ? local : serverFloor!;
    },
    recordReceiptNumber: (number) async {
      if (number > (serverFloor ?? 0)) {
        serverFloor = number;
      }
      await identityDao.recordIssuedReceiptNumber(number);
    },
    identity: () async {
      final identity = await identityDao.getIdentity();
      return identity == null
          ? null
          : CartIdentity(
            tenantId: identity.tenantId,
            deviceId: identity.deviceId,
            branchId: identity.branchId,
          );
    },
  );
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
    showLoading: true,
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
      showLoading: true,
    );

    // A completed sale changes stock, movements and every sales chart — drop
    // those caches now so the Dashboard/Inventory show it without a restart.
    if (succeeded) {
      refreshStockAndSalesData(ref);
    }

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

  /// Runs a cart change. Cart edits are local and instant (see
  /// [LocalFirstPosRepository]), so by default the current cart stays on screen
  /// and simply swaps to the new one — flipping to a loading state for every
  /// tap is what made the cart flicker. Only slow, network-bound steps
  /// (payment, voiding a server cart, claiming a kiosk order) ask for
  /// [showLoading] so the UI can show progress and block double taps.
  Future<bool> _mutate(
    Future<Transaction> Function(PosRepository) action, {
    bool showLoading = false,
  }) async {
    final repository = ref.read(posRepositoryProvider);

    if (showLoading) {
      state = const AsyncLoading();
    }
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
