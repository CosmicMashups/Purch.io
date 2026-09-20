import 'dart:convert';
import 'dart:math';

import '../../../core/errors/failure.dart';
import '../../catalog/domain/catalog_repository.dart';
import '../../catalog/domain/item_combo_component_models.dart';
import '../../catalog/domain/item_models.dart';
import '../../catalog/domain/item_variant_models.dart';
import '../../catalog/domain/modifier_models.dart';
import '../../catalog/domain/pricing_type.dart';
import '../domain/offline_limits.dart';
import '../domain/payment_method.dart';
import '../domain/pos_repository.dart';
import '../domain/pricing_engine.dart';
import '../domain/transaction_models.dart';
import 'local_cart_models.dart';
import 'sale_queue.dart';

/// Where the in-progress cart is persisted between clicks and app restarts.
abstract class CartDraftStore {
  Future<String?> read();
  Future<void> write(String json);
  Future<void> clear();
}

/// Who this device is, for stamping the local cart view.
class CartIdentity {
  const CartIdentity({
    required this.tenantId,
    required this.deviceId,
    required this.branchId,
  });

  final String tenantId;
  final String deviceId;
  final String branchId;
}

/// In-memory [CartDraftStore] — used by tests and as a fallback.
class MemoryCartDraftStore implements CartDraftStore {
  String? _json;

  @override
  Future<String?> read() async => _json;

  @override
  Future<void> write(String json) async => _json = json;

  @override
  Future<void> clear() async => _json = null;
}

/// The Cashier's [PosRepository]: add/edit/remove lines, discounts, promo codes
/// and order type all happen on the device — instantly, with no network — and
/// the cart is persisted locally after every change. Nothing is sent to the
/// server until the sale is paid, at which point the whole sale goes up in one
/// idempotent checkout call (the server re-prices it authoritatively and
/// records the payment).
///
/// Claimed kiosk orders are the exception: they already live on the server as
/// that device's open cart, so while one is active every call is forwarded to
/// [_remote] unchanged.
class LocalFirstPosRepository implements PosRepository {
  LocalFirstPosRepository({
    required PosRepository remote,
    required CatalogRepository catalog,
    required Future<List<Item>> Function() loadItems,
    required Future<PricingRules> Function() loadRules,
    required CartDraftStore store,
    required Future<CartIdentity?> Function() identity,
    required Future<int> Function({bool refresh}) lastIssuedReceiptNumber,
    required Future<void> Function(int) recordReceiptNumber,
    SaleQueueStore? saleQueue,
    OfflineLimits offlineLimits = const OfflineLimits(),
    Future<bool> Function()? isConnected,
    Future<void> Function()? drainQueue,
    DateTime Function()? clock,
  }) : _remote = remote,
       _catalog = catalog,
       _loadItems = loadItems,
       _loadRules = loadRules,
       _store = store,
       _identity = identity,
       _lastIssuedReceiptNumber = lastIssuedReceiptNumber,
       _recordReceiptNumber = recordReceiptNumber,
       _saleQueue = saleQueue,
       _offlineLimits = offlineLimits,
       _isConnected = isConnected,
       _drainQueue = drainQueue,
       _clock = clock ?? DateTime.now;

  final PosRepository _remote;
  final CatalogRepository _catalog;
  final Future<List<Item>> Function() _loadItems;
  final Future<PricingRules> Function() _loadRules;
  final CartDraftStore _store;
  final Future<CartIdentity?> Function() _identity;

  /// The highest receipt number known for this terminal: the larger of what this
  /// device has issued and what the server has recorded. [refresh] re-reads the
  /// server's figure instead of using a cached one.
  final Future<int> Function({bool refresh}) _lastIssuedReceiptNumber;

  /// Persists that this device has now issued (or must treat as used) a number,
  /// so its next sale is numbered after it.
  final Future<void> Function(int) _recordReceiptNumber;

  /// Where sales completed offline wait to be sent. Null disables offline
  /// selling: a failed payment then simply fails.
  final SaleQueueStore? _saleQueue;
  final OfflineLimits _offlineLimits;

  /// A cheap "is there any connection at all" check, so a terminal that is
  /// plainly offline doesn't make the cashier wait out a request timeout.
  final Future<bool> Function()? _isConnected;

  /// Sends whatever is waiting in the offline queue now (best effort).
  final Future<void> Function()? _drainQueue;
  final DateTime Function() _clock;

  /// After a request fails to get through, go straight to the offline path for
  /// a short while instead of making every sale wait out its own timeout.
  DateTime? _offlineUntil;
  static const _offlineBackoff = Duration(seconds: 30);

  /// Only payments that are confirmed by the cashier at the counter can be
  /// completed offline. Credit (utang) needs the customer's limit checked on
  /// the server, and gateway payments need the gateway.
  static const _offlineMethods = {
    PaymentMethod.cash,
    PaymentMethod.bankTransfer,
    PaymentMethod.manualGcashQr,
  };

  static final _random = Random.secure();

  // Small per-session memo of the per-item catalog sub-resources, so re-adding
  // the same variant/modifier/combo item doesn't refetch them.
  final _variants = <String, Future<List<ItemVariant>>>{};
  final _modifierGroups = <String, Future<List<ModifierGroup>>>{};
  final _comboSlots = <String, Future<List<ItemComboComponent>>>{};

  // Every cart mutation is load -> await -> save from that snapshot, so two
  // overlapping ones (a scanner beeping twice, a double tap) would each start
  // from the same cart and the later save would silently drop the earlier edit.
  // Running them one at a time makes each see the previous one's result.
  Future<void> _mutationTail = Future<void>.value();

  Future<T> _serial<T>(Future<T> Function() action) {
    final result = _mutationTail.then((_) => action());
    _mutationTail = result.then<void>((_) {}, onError: (Object _) {});
    return result;
  }

  LocalCart? _cart;
  PricingRules? _rules;
  PricingRules? _lastRules;

  // ---------------------------------------------------------------------
  // PosRepository
  // ---------------------------------------------------------------------

  @override
  Future<Transaction> getOrCreateOpenCart() async {
    final cart = await _load();
    if (cart.serverBacked) {
      return _remote.getOrCreateOpenCart();
    }
    return _toTransaction(cart);
  }

  @override
  Future<Transaction> addLine(AddTransactionLineRequest request) =>
      _serial(() => _addLine(request));

  Future<Transaction> _addLine(AddTransactionLineRequest request) async {
    final cart = await _load();
    if (cart.serverBacked) {
      return _remote.addLine(request);
    }
    if (request.quantity <= 0) {
      throw _invalid('Quantity must be greater than zero.');
    }

    final line = await _resolveLine(request);
    final lines = [...cart.lines];
    final existing =
        line.isMergeable
            ? lines.indexWhere(
              (l) =>
                  l.isMergeable &&
                  l.request.itemId == line.request.itemId &&
                  l.request.itemVariantId == line.request.itemVariantId,
            )
            : -1;

    if (existing >= 0) {
      lines[existing] = lines[existing].withQuantity(
        lines[existing].quantity + line.quantity,
      );
    } else {
      lines.add(line);
    }
    return _save(cart.copyWith(lines: lines));
  }

  @override
  Future<Transaction> updateLine(
    String lineId,
    UpdateTransactionLineRequest request,
  ) => _serial(() => _updateLine(lineId, request));

  Future<Transaction> _updateLine(
    String lineId,
    UpdateTransactionLineRequest request,
  ) async {
    final cart = await _load();
    if (cart.serverBacked) {
      return _remote.updateLine(lineId, request);
    }
    if (request.quantity <= 0) {
      throw _invalid('Quantity must be greater than zero.');
    }
    final index = cart.lines.indexWhere((l) => l.id == lineId);
    if (index < 0) {
      throw const NotFoundFailure('That line is no longer in the cart.');
    }
    final lines = [...cart.lines];
    lines[index] = lines[index].withQuantity(request.quantity);
    return _save(cart.copyWith(lines: lines));
  }

  @override
  Future<Transaction> removeLine(String lineId) =>
      _serial(() => _removeLine(lineId));

  Future<Transaction> _removeLine(String lineId) async {
    final cart = await _load();
    if (cart.serverBacked) {
      return _remote.removeLine(lineId);
    }
    return _save(
      cart.copyWith(lines: cart.lines.where((l) => l.id != lineId).toList()),
    );
  }

  @override
  Future<Transaction> voidCart() => _serial(_voidCart);

  Future<Transaction> _voidCart() async {
    final cart = await _load();
    if (cart.serverBacked) {
      final voided = await _remote.voidCart();
      await _reset();
      return voided;
    }

    final voided = await _toTransaction(cart, status: TransactionStatus.voided);
    // A payment attempt may have reached the server, which would then have
    // recorded the sale under this number even though we never saw the reply.
    // The number must not go to a different sale, so retire it (a visible gap in
    // the sequence, which is the safe direction) instead of reusing it.
    final reserved = cart.receiptNumber;
    if (cart.checkoutAttempted && reserved != null) {
      await _recordReceiptNumber(reserved);
    }
    await _reset();
    return voided;
  }

  @override
  Future<Transaction> applySeniorPwdDiscount(
    ApplySeniorPwdDiscountRequest request,
  ) => _serial(() => _applySeniorPwdDiscount(request));

  Future<Transaction> _applySeniorPwdDiscount(
    ApplySeniorPwdDiscountRequest request,
  ) async {
    final cart = await _load();
    if (cart.serverBacked) {
      return _remote.applySeniorPwdDiscount(request);
    }
    return _save(cart.copyWith(seniorPwdApplied: request.apply));
  }

  @override
  Future<Transaction> applyPromoCode(ApplyPromoCodeRequest request) =>
      _serial(() => _applyPromoCode(request));

  Future<Transaction> _applyPromoCode(ApplyPromoCodeRequest request) async {
    final cart = await _load();
    if (cart.serverBacked) {
      return _remote.applyPromoCode(request);
    }

    final code = request.code?.trim();
    if (code == null || code.isEmpty) {
      return _save(cart.copyWith(promoCode: null));
    }

    // Validate against the current promo codes up front, like the server does
    // when a code is entered — an invalid code is rejected here instead of
    // silently ignored until checkout.
    // Strict here (unlike pricing): if the codes can't be loaded we can't tell a
    // typo from an outage, so say so instead of accepting or rejecting blindly.
    final PricingRules rules;
    try {
      rules = _rules ?? (_rules = _lastRules = await _loadRules());
    } on Object {
      throw const NetworkFailure(
        "Couldn't check that promo code right now. Check the connection and try again.",
      );
    }
    final promo = rules.findPromoCode(code);
    final expired =
        promo?.expiresAt != null && !promo!.expiresAt!.isAfter(DateTime.now());
    if (promo == null || !promo.isActive || expired) {
      throw _invalid("This promo code isn't valid.", field: 'code');
    }
    return _save(cart.copyWith(promoCode: promo.code));
  }

  @override
  Future<Transaction> setOrderType(SetOrderTypeRequest request) =>
      _serial(() => _setOrderType(request));

  Future<Transaction> _setOrderType(SetOrderTypeRequest request) async {
    final cart = await _load();
    if (cart.serverBacked) {
      return _remote.setOrderType(request);
    }
    if (request.orderType.trim().isEmpty) {
      throw _invalid('Order type is required.', field: 'orderType');
    }
    return _save(cart.copyWith(orderType: request.orderType.trim()));
  }

  @override
  Future<Transaction> checkout(CheckoutRequest request) =>
      _remote.checkout(request);

  @override
  Future<int> getLastIssuedReceiptNumber() =>
      _remote.getLastIssuedReceiptNumber();

  @override
  Future<List<Transaction>> listPendingKioskOrders(String branchId) =>
      _remote.listPendingKioskOrders(branchId);

  @override
  Future<Transaction> claimKioskOrder(String transactionId) async {
    final cart = await _load();
    if (cart.lines.isNotEmpty) {
      throw _invalid(
        'Finish or void your current cart before claiming a kiosk order.',
      );
    }
    final claimed = await _remote.claimKioskOrder(transactionId);
    await _persist(cart.copyWith(serverBacked: true));
    return claimed;
  }

  @override
  Future<Transaction> recordPayment(RecordPaymentRequest request) async {
    final cart = await _load();
    if (cart.serverBacked) {
      final paid = await _remote.recordPayment(request);
      await _reset();
      return paid;
    }
    if (cart.lines.isEmpty) {
      throw _invalid(
        'The cart is empty — add an item before recording a payment.',
      );
    }

    final local = await _toTransaction(cart);

    // Number the sale on this device, from the terminal's own sequence, at the
    // first payment attempt — and keep that number with the draft so every retry
    // sends the same one. (A receipt must never carry two different numbers.)
    var working = cart;
    final int number;
    if (working.receiptNumber case final reserved?) {
      number = reserved;
    } else {
      number = await _lastIssuedReceiptNumber(refresh: false) + 1;
      working = working.copyWith(receiptNumber: number);
    }
    await _persist(working.copyWith(checkoutAttempted: true));

    final canGoOffline =
        _saleQueue != null && _offlineMethods.contains(request.method);

    // Sales must reach the server in the order they were rung up: receipt
    // numbers are sequential per terminal, and a Z-reading covers a run of
    // them. If earlier sales are still waiting to sync, a new online sale
    // would jump ahead of them, so send those first — and if they can't be
    // sent, this one waits behind them instead of overtaking.
    var behindQueuedSales = false;
    if (_saleQueue != null && await _hasUnsyncedSales()) {
      // Only try if there is any chance of getting through: a terminal that is
      // plainly offline (or has just failed to connect) must not make the
      // cashier wait out another timeout before the sale can be queued.
      if (!await _knownOffline()) {
        await _drainQueue?.call();
      }
      behindQueuedSales = await _hasUnsyncedSales();
      if (behindQueuedSales) {
        _offlineUntil = _clock().add(_offlineBackoff);
      }
    }
    if (behindQueuedSales && !canGoOffline) {
      await _persist(working.copyWith(checkoutAttempted: false));
      throw const NetworkFailure(
        'Earlier sales completed offline have not synced yet, and this '
        'payment method needs the server. Reconnect so they can sync, then '
        'try again.',
      );
    }

    if (canGoOffline && (behindQueuedSales || await _knownOffline())) {
      try {
        return await _completeOffline(cart, number, request, local);
      } on Failure {
        // Nothing was sent, so the reserved number is still unused.
        await _persist(working.copyWith(checkoutAttempted: false));
        rethrow;
      }
    }

    final Transaction paid;
    try {
      // One call: the server builds the cart from these lines, prices it with
      // its own rules, and records the payment atomically. The same saleId is
      // sent on every attempt, so if a response is lost the retry gets the sale
      // that already went through back instead of charging the customer twice.
      paid = await _remote.checkout(
        CheckoutRequest(
          saleId: cart.saleId,
          lines: [for (final line in cart.lines) line.request],
          seniorPwdDiscountApplied: cart.seniorPwdApplied,
          promoCode: cart.promoCode,
          orderType: cart.orderType,
          payment: request,
          expectedTotal: local.totalAmount,
          receiptNumber: number,
        ),
      );
    } on Failure catch (failure) {
      // The request never got a clear answer (no connection, server down). The
      // customer is standing at the counter, so finish the sale on this device
      // and send it later — under the same sale id and receipt number, so if the
      // server did receive it after all, the later send just returns that sale.
      if (canGoOffline && _isAmbiguous(failure)) {
        _offlineUntil = _clock().add(_offlineBackoff);
        return _completeOffline(cart, number, request, local);
      }

      // A definitive rejection means nothing was recorded, so the number is
      // still ours; only a lost or unanswered request leaves that in doubt.
      if (!_isAmbiguous(failure)) {
        _rules = null; // most often "prices or promos changed" — reload them
        var reset = working.copyWith(checkoutAttempted: false);
        if (failure is ConflictFailure &&
            failure.message.contains('Receipt number')) {
          // Another install or a re-pair already used it: take a fresh number,
          // starting from the server's own record, on the next attempt.
          await _lastIssuedReceiptNumber(refresh: true);
          reset = reset.copyWith(receiptNumber: null);
        }
        await _persist(reset);
      }
      rethrow;
    }

    await _recordReceiptNumber(paid.receiptNumber ?? number);

    // The sale is recorded; drop the draft so the next sale starts clean.
    await _reset();
    _rules = null; // pick up any promo changes before the next sale
    return paid;
  }

  Future<bool> _hasUnsyncedSales() async {
    final queue = _saleQueue;
    return queue != null &&
        SaleQueueStats.of(await queue.reviewable()).unsynced > 0;
  }

  Future<bool> _knownOffline() async {
    final until = _offlineUntil;
    if (until != null && _clock().isBefore(until)) {
      return true;
    }
    final check = _isConnected;
    return check != null && !await check();
  }

  /// Completes the sale on this device: it is written to the local queue first
  /// (the durable record), then the receipt number is retired and the cart
  /// cleared, and the caller gets a completed sale to print the receipt from.
  Future<Transaction> _completeOffline(
    LocalCart cart,
    int number,
    RecordPaymentRequest request,
    Transaction local,
  ) async {
    final queue = _saleQueue!;
    final now = _clock();

    final stats = SaleQueueStats.of(await queue.reviewable());
    if (stats.level(_offlineLimits, now) == OfflineLevel.blocked) {
      throw NetworkFailure(
        'No connection, and this terminal has reached its offline selling '
        'limit (${_offlineLimits.maxSales} sales or '
        '${_offlineLimits.maxAge.inHours} hours). Reconnect so the waiting '
        'sales can sync, then continue.',
      );
    }

    final total = local.totalAmount;
    double? change;
    if (request.method == PaymentMethod.cash) {
      final tendered = request.amountTendered;
      if (tendered == null || tendered < total) {
        throw _invalid('Cash tendered must cover the total amount.');
      }
      change = tendered - total;
    }

    final checkout = CheckoutRequest(
      saleId: cart.saleId,
      lines: [for (final line in cart.lines) line.request],
      seniorPwdDiscountApplied: cart.seniorPwdApplied,
      promoCode: cart.promoCode,
      orderType: cart.orderType,
      payment: request,
      expectedTotal: total,
      receiptNumber: number,
      offlineSale: true,
      soldAt: now,
    );

    // Durable first: once this returns the sale survives a crash or restart.
    await queue.enqueue(
      SaleQueueEntry(
        saleId: cart.saleId,
        receiptNumber: number,
        totalAmount: total,
        soldAt: now,
        request: checkout,
      ),
    );
    await _recordReceiptNumber(number);
    await _reset();

    return Transaction(
      id: '${Transaction.offlineSaleIdPrefix}${cart.saleId}',
      branchId: local.branchId,
      deviceId: local.deviceId,
      status: TransactionStatus.completed,
      lines: local.lines,
      subtotal: local.subtotal,
      discountAmount: local.discountAmount,
      seniorPwdDiscountApplied: local.seniorPwdDiscountApplied,
      promoCode: local.promoCode,
      promoDiscountAmount: local.promoDiscountAmount,
      itemPromoDiscountAmount: local.itemPromoDiscountAmount,
      totalAmount: total,
      receiptNumber: number,
      orderType: local.orderType,
      payments: [
        Payment(
          id: 'offline-payment-${cart.saleId}',
          method: request.method,
          status: PaymentStatus.confirmed,
          amount: total,
          amountTendered: request.amountTendered,
          changeGiven: change,
        ),
      ],
    );
  }

  /// Whether the request may have reached the server despite the failure.
  static bool _isAmbiguous(Failure failure) =>
      failure is NetworkFailure ||
      failure is ServiceUnavailableFailure ||
      failure is UnknownFailure;

  // ---------------------------------------------------------------------
  // Line resolution (mirrors TransactionService.AddLineAsync validation/pricing)
  // ---------------------------------------------------------------------

  Future<LocalCartLine> _resolveLine(AddTransactionLineRequest request) async {
    final items = await _loadItems();
    final item = items.where((i) => i.id == request.itemId).firstOrNull;
    if (item == null) {
      throw NotFoundFailure('Item ${request.itemId} was not found.');
    }
    if (!item.isActive) {
      throw _invalid('This item is not active.', field: 'itemId');
    }
    if (item.pricingType == PricingType.variantMatrix &&
        request.itemVariantId == null) {
      throw _invalid(
        'This item requires choosing a variant.',
        field: 'itemVariantId',
      );
    }

    if (item.pricingType == PricingType.combo) {
      return _resolveComboLine(item, request, items);
    }

    var unitPrice = item.basePrice;
    var variantAttributes = const <String, String>{};
    if (request.itemVariantId case final variantId?) {
      final variants = await _variantsFor(item.id);
      final variant = variants.where((v) => v.id == variantId).firstOrNull;
      if (variant == null) {
        throw NotFoundFailure('Item variant $variantId was not found.');
      }
      unitPrice = variant.priceOverride ?? item.basePrice;
      variantAttributes = variant.attributes;
    }

    final modifiers = await _resolveModifiers(item, request.selectedModifierIds);
    unitPrice += modifiers.fold<double>(0, (sum, m) => sum + m.priceDelta);

    return LocalCartLine(
      id: _newId(),
      request: AddTransactionLineRequest(
        itemId: request.itemId,
        itemVariantId: request.itemVariantId,
        quantity: request.quantity,
        selectedModifierIds:
            modifiers.isEmpty
                ? null
                : [for (final m in modifiers) m.itemModifierId],
      ),
      itemName: item.name,
      unitPrice: unitPrice,
      variantAttributes: variantAttributes,
      modifiers: modifiers,
    );
  }

  Future<LocalCartLine> _resolveComboLine(
    Item item,
    AddTransactionLineRequest request,
    List<Item> items,
  ) async {
    final slots = await _comboSlotsFor(item.id);
    if (slots.isEmpty) {
      throw _invalid('This combo has no configured slots yet.');
    }

    final selections = request.comboSelections ?? const [];
    final slotIds = slots.map((s) => s.id).toSet();
    if (selections.any((s) => !slotIds.contains(s.slotId))) {
      throw _invalid("One of the selections doesn't belong to this combo.");
    }

    final resolved = <TransactionLineComboSelection>[];
    for (final slot in slots) {
      final picks = selections.where((s) => s.slotId == slot.id).toList();
      if (picks.length != slot.quantity) {
        throw _invalid(
          'Choose ${slot.quantity} item(s) for "${slot.slotLabel}".',
        );
      }
      for (final pick in picks) {
        final selected =
            items.where((i) => i.id == pick.selectedItemId).firstOrNull;
        if (selected == null) {
          throw NotFoundFailure('Item ${pick.selectedItemId} was not found.');
        }
        if (!selected.isActive || selected.categoryId != slot.componentCategoryId) {
          throw _invalid(
            '"${selected.name}" isn\'t a valid choice for "${slot.slotLabel}".',
          );
        }
        resolved.add(
          TransactionLineComboSelection(
            slotId: slot.id,
            slotLabel: slot.slotLabel,
            selectedItemId: selected.id,
            selectedItemName: selected.name,
          ),
        );
      }
    }

    final modifiers = await _resolveModifiers(item, request.selectedModifierIds);
    final unitPrice =
        item.basePrice +
        slots.fold<double>(0, (sum, s) => sum + (s.substitutionUpchargeAmount ?? 0)) +
        modifiers.fold<double>(0, (sum, m) => sum + m.priceDelta);

    return LocalCartLine(
      id: _newId(),
      request: AddTransactionLineRequest(
        itemId: request.itemId,
        quantity: request.quantity,
        comboSelections: request.comboSelections,
        selectedModifierIds:
            modifiers.isEmpty
                ? null
                : [for (final m in modifiers) m.itemModifierId],
      ),
      itemName: item.name,
      unitPrice: unitPrice,
      modifiers: modifiers,
      comboSelections: resolved,
    );
  }

  Future<List<TransactionLineModifierSelection>> _resolveModifiers(
    Item item,
    List<String>? selectedIds,
  ) async {
    final selected = selectedIds ?? const <String>[];
    final groups = await _modifierGroupsFor(item.id);

    if (groups.isEmpty) {
      if (selected.isNotEmpty) {
        throw _invalid(
          'This item has no modifier groups to select from.',
          field: 'selectedModifierIds',
        );
      }
      return const [];
    }

    final recognized = {
      for (final group in groups) for (final m in group.modifiers) m.id,
    };
    if (selected.any((id) => !recognized.contains(id))) {
      throw _invalid(
        "One of the selected modifiers doesn't belong to this item.",
        field: 'selectedModifierIds',
      );
    }

    final result = <TransactionLineModifierSelection>[];
    for (final group in groups) {
      final picked = group.modifiers.where((m) => selected.contains(m.id)).toList();
      if (group.isRequired && picked.isEmpty) {
        throw _invalid(
          'Choose an option for "${group.name}".',
          field: 'selectedModifierIds',
        );
      }
      if (!group.allowMultipleSelection && picked.length > 1) {
        throw _invalid(
          'Only one option can be chosen for "${group.name}".',
          field: 'selectedModifierIds',
        );
      }
      for (final m in picked) {
        result.add(
          TransactionLineModifierSelection(
            itemModifierId: m.id,
            modifierName: m.name,
            modifierGroupName: group.name,
            priceDelta: m.priceDelta,
          ),
        );
      }
    }
    return result;
  }

  Future<List<ItemVariant>> _variantsFor(String itemId) =>
      _memo(_variants, itemId, () => _catalog.listVariants(itemId));

  Future<List<ModifierGroup>> _modifierGroupsFor(String itemId) => _memo(
    _modifierGroups,
    itemId,
    () => _catalog.listModifierGroupsForItem(itemId),
  );

  Future<List<ItemComboComponent>> _comboSlotsFor(String itemId) =>
      _memo(_comboSlots, itemId, () => _catalog.listComboComponents(itemId));

  /// Memoises a fetch, but never a failed one — a dropped connection on the
  /// first add must not poison every later add of that item.
  Future<T> _memo<T>(
    Map<String, Future<T>> cache,
    String key,
    Future<T> Function() fetch,
  ) async {
    final cached = cache[key];
    if (cached != null) {
      return cached;
    }
    final future = fetch();
    cache[key] = future;
    try {
      return await future;
    } on Object {
      cache.remove(key)?.ignore();
      rethrow;
    }
  }

  // ---------------------------------------------------------------------
  // State + persistence
  // ---------------------------------------------------------------------

  /// The promo rules to price with. Never throws: if they can't be loaded (no
  /// connection) the cart keeps working on the last rules seen — or none —
  /// and the server's re-pricing at checkout is what actually decides.
  Future<PricingRules> _currentRules() async {
    final cached = _rules;
    if (cached != null) {
      return cached;
    }
    try {
      return _rules = _lastRules = await _loadRules();
    } on Object {
      return _lastRules ?? PricingRules.empty;
    }
  }

  Future<LocalCart> _load() async {
    final inMemory = _cart;
    if (inMemory != null) {
      return inMemory;
    }
    final json = await _store.read();
    if (json != null) {
      try {
        final decoded = jsonDecode(json) as Map<String, dynamic>;
        // Drafts saved before checkout became one call carry no sale id.
        if ((decoded['saleId'] as String?)?.isNotEmpty != true) {
          decoded['saleId'] = _newUuid();
        }
        return _cart = LocalCart.fromJson(decoded);
      } on Object {
        // A corrupt draft must never brick the Cashier — start fresh.
        await _store.clear();
      }
    }
    return _cart = LocalCart(id: _newId(), saleId: _newUuid());
  }

  Future<void> _persist(LocalCart cart) async {
    _cart = cart;
    await _store.write(jsonEncode(cart.toJson()));
  }

  Future<Transaction> _save(LocalCart cart) async {
    await _persist(cart);
    return _toTransaction(cart);
  }

  Future<void> _reset() async {
    _cart = LocalCart(id: _newId(), saleId: _newUuid());
    await _store.clear();
  }

  Future<Transaction> _toTransaction(
    LocalCart cart, {
    TransactionStatus status = TransactionStatus.open,
  }) async {
    final identity = await _identity();
    final rules = cart.lines.isEmpty && cart.promoCode == null
        ? PricingRules.empty
        : await _currentRules();
    final priced = PricingEngine.price(
      lines: [
        for (final line in cart.lines)
          PricingLineInput(
            lineId: line.id,
            itemId: line.request.itemId,
            quantity: line.quantity,
            unitPrice: line.unitPrice,
          ),
      ],
      rules: rules,
      seniorPwdApplied: cart.seniorPwdApplied,
      promoCode: cart.promoCode,
    );

    return Transaction(
      id: cart.id,
      branchId: identity?.branchId ?? '',
      deviceId: identity?.deviceId ?? '',
      status: status,
      lines: [
        for (final line in cart.lines)
          TransactionLine(
            id: line.id,
            itemId: line.request.itemId,
            itemName: line.itemName,
            itemVariantId: line.request.itemVariantId,
            itemVariantAttributes: line.variantAttributes,
            quantity: line.quantity,
            unitPrice: line.unitPrice,
            lineTotal: line.quantity * line.unitPrice,
            promoDiscountAmount: priced.lineDiscounts[line.id]?.discount ?? 0,
            appliedPromoLabel: priced.lineDiscounts[line.id]?.label,
            comboSelections: line.comboSelections,
            modifierSelections: line.modifiers,
          ),
      ],
      subtotal: priced.grossSubtotal,
      discountAmount: priced.discountAmount,
      seniorPwdDiscountApplied: cart.seniorPwdApplied,
      // The valid code stays on the cart even while it isn't discounting (Senior/PWD
      // chosen, or item promos are larger), so switching that off restores it.
      promoCode: priced.retainedPromoCode,
      promoCodeNotApplied: priced.promoCodeNotApplied,
      seniorPwdSavings: priced.seniorPwdSavings,
      promoSavings: priced.promoSavings,
      promoDiscountAmount: priced.promoDiscountAmount,
      itemPromoDiscountAmount: priced.itemPromoDiscountAmount,
      totalAmount: priced.totalAmount,
      receiptNumber: null,
      orderType: cart.orderType,
      payments: const [],
    );
  }

  static String _newId() {
    final micros = DateTime.now().microsecondsSinceEpoch.toRadixString(16);
    final salt = _random.nextInt(1 << 32).toRadixString(16).padLeft(8, '0');
    return 'local-$micros-$salt';
  }

  /// A random (v4) UUID — the server's SaleId is a Guid.
  static String _newUuid() {
    final bytes = List<int>.generate(16, (_) => _random.nextInt(256));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    final hex = [for (final b in bytes) b.toRadixString(16).padLeft(2, '0')].join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-'
        '${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
  }

  static ValidationFailure _invalid(String message, {String field = 'cart'}) =>
      ValidationFailure(message, {
        field: [message],
      });
}
