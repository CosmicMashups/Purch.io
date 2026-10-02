import 'dart:convert';
import 'dart:math';

import '../../../core/errors/failure.dart';
import '../../catalog/domain/catalog_repository.dart';
import '../../catalog/domain/item_combo_component_models.dart';
import '../../catalog/domain/item_models.dart';
import '../../catalog/domain/item_variant_models.dart';
import '../../catalog/domain/modifier_models.dart';
import '../../catalog/domain/pricing_type.dart';
import '../../pos/data/local_cart_models.dart';
import '../../pos/data/local_first_pos_repository.dart' show CartDraftStore, CartIdentity;
import '../../pos/domain/transaction_models.dart';
import '../domain/kiosk_cart_repository.dart';
import '../domain/place_kiosk_order_request.dart';

/// The kiosk's cart lives entirely on-device (E6 design decision): every add,
/// update and remove is priced and shown instantly from the cached catalog,
/// with no network call, and persisted locally so it survives a restart.
/// Nothing reaches the server until [submitOrder], which builds, prices and
/// submits the whole order in one idempotent call (`PlaceKioskOrderAsync`) —
/// the server's own numbers are the ones actually queued for the kitchen.
///
/// Unlike the Cashier's [LocalFirstPosRepository] this never falls back to a
/// server-backed cart (kiosk orders have no claim step), does not run the
/// automatic item-promo engine locally (the kiosk has no promo-rules feed —
/// see the matching note in web's localCart.ts), and tracks no receipt
/// number or offline sale queue.
class LocalFirstKioskCartRepository implements KioskCartRepository {
  LocalFirstKioskCartRepository({
    required Future<Transaction> Function(PlaceKioskOrderRequest) placeOrder,
    required CatalogRepository catalog,
    required Future<List<Item>> Function() loadItems,
    required CartDraftStore store,
    required Future<CartIdentity?> Function() identity,
    Future<List<ModifierGroup>> Function(String itemId)? modifierGroupsFor,
  }) : _placeOrder = placeOrder,
       _catalog = catalog,
       _loadItems = loadItems,
       _store = store,
       _identity = identity,
       _modifierGroupsLoader = modifierGroupsFor;

  final Future<Transaction> Function(PlaceKioskOrderRequest) _placeOrder;
  final CatalogRepository _catalog;
  final Future<List<Item>> Function() _loadItems;
  final CartDraftStore _store;
  final Future<CartIdentity?> Function() _identity;
  final Future<List<ModifierGroup>> Function(String itemId)?
  _modifierGroupsLoader;

  static final _random = Random.secure();

  final _variants = <String, Future<List<ItemVariant>>>{};
  final _modifierGroups = <String, Future<List<ModifierGroup>>>{};
  final _comboSlots = <String, Future<List<ItemComboComponent>>>{};

  // Every mutation is load -> await -> save from that snapshot, so two
  // overlapping ones (a double tap) each see the previous one's result
  // instead of racing and silently dropping one.
  Future<void> _mutationTail = Future<void>.value();

  Future<T> _serial<T>(Future<T> Function() action) {
    final result = _mutationTail.then((_) => action());
    _mutationTail = result.then<void>((_) {}, onError: (Object _) {});
    return result;
  }

  LocalCart? _cart;

  @override
  Future<Transaction> getOrCreateOpenCart() async => _toTransaction(await _load());

  @override
  Future<void> clear() => _serial(_reset);

  @override
  Future<Transaction> addLine(AddTransactionLineRequest request) =>
      _serial(() => _addLine(request));

  Future<Transaction> _addLine(AddTransactionLineRequest request) async {
    if (request.quantity <= 0) {
      throw _invalid('Quantity must be greater than zero.');
    }
    final cart = await _load();
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
    if (request.quantity <= 0) {
      throw _invalid('Quantity must be greater than zero.');
    }
    final cart = await _load();
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
    return _save(
      cart.copyWith(lines: cart.lines.where((l) => l.id != lineId).toList()),
    );
  }

  @override
  Future<Transaction> setOrderType(SetOrderTypeRequest request) =>
      _serial(() => _setOrderType(request));

  Future<Transaction> _setOrderType(SetOrderTypeRequest request) async {
    if (request.orderType.trim().isEmpty) {
      throw _invalid('Order type is required.', field: 'orderType');
    }
    final cart = await _load();
    return _save(cart.copyWith(orderType: request.orderType.trim()));
  }

  @override
  Future<Transaction> submitOrder() => _serial(_submitOrder);

  Future<Transaction> _submitOrder() async {
    final cart = await _load();
    if (cart.lines.isEmpty) {
      throw _invalid('Add at least one item before submitting your order.');
    }
    final orderType = cart.orderType;
    if (orderType == null || orderType.trim().isEmpty) {
      throw _invalid('Order type is required.', field: 'orderType');
    }

    final placed = await _placeOrder(
      PlaceKioskOrderRequest(
        orderId: cart.saleId,
        lines: [for (final line in cart.lines) line.request],
        orderType: orderType,
      ),
    );
    await _reset();
    return placed;
  }

  // ---------------------------------------------------------------------
  // Line resolution (mirrors TransactionService.StageLineAsync validation/pricing)
  // ---------------------------------------------------------------------

  Future<LocalCartLine> _resolveLine(AddTransactionLineRequest request) async {
    final items = await _loadItems();
    final item = items.where((i) => i.id == request.itemId).firstOrNull;
    if (item == null) {
      throw NotFoundFailure('Item ${request.itemId} was not found.');
    }
    if (!item.isActive) {
      throw _invalid('${item.name} is no longer available.', field: 'itemId');
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

    final modifiers = await _resolveModifiers(
      item,
      request.selectedModifierIds,
    );
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
        if (!selected.isActive ||
            selected.categoryId != slot.componentCategoryId) {
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

    final modifiers = await _resolveModifiers(
      item,
      request.selectedModifierIds,
    );
    final unitPrice =
        item.basePrice +
        slots.fold<double>(
          0,
          (sum, s) => sum + (s.substitutionUpchargeAmount ?? 0),
        ) +
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
      for (final group in groups)
        for (final m in group.modifiers) m.id,
    };
    if (selected.any((id) => !recognized.contains(id))) {
      throw _invalid(
        "One of the selected modifiers doesn't belong to this item.",
        field: 'selectedModifierIds',
      );
    }

    final result = <TransactionLineModifierSelection>[];
    for (final group in groups) {
      final picked =
          group.modifiers.where((m) => selected.contains(m.id)).toList();
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
    () => (_modifierGroupsLoader ?? _catalog.listModifierGroupsForItem)(itemId),
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

  Future<LocalCart> _load() async {
    final inMemory = _cart;
    if (inMemory != null) {
      return inMemory;
    }
    final json = await _store.read();
    if (json != null) {
      try {
        final decoded = jsonDecode(json) as Map<String, dynamic>;
        if ((decoded['saleId'] as String?)?.isNotEmpty != true) {
          decoded['saleId'] = _newUuid();
        }
        return _cart = LocalCart.fromJson(decoded);
      } on Object {
        // A corrupt draft must never brick the kiosk — start fresh.
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

  Future<Transaction> _toTransaction(LocalCart cart) async {
    final identity = await _identity();
    return Transaction(
      id: cart.id,
      branchId: identity?.branchId ?? '',
      deviceId: identity?.deviceId ?? '',
      status: TransactionStatus.open,
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
            comboSelections: line.comboSelections,
            modifierSelections: line.modifiers,
          ),
      ],
      subtotal: cart.lines.fold<double>(
        0,
        (sum, line) => sum + line.quantity * line.unitPrice,
      ),
      discountAmount: 0,
      seniorPwdDiscountApplied: false,
      promoCode: null,
      promoDiscountAmount: 0,
      totalAmount: cart.lines.fold<double>(
        0,
        (sum, line) => sum + line.quantity * line.unitPrice,
      ),
      receiptNumber: null,
      orderType: cart.orderType,
      originatedFromKiosk: true,
      payments: const [],
    );
  }

  static String _newId() {
    final micros = DateTime.now().microsecondsSinceEpoch.toRadixString(16);
    final salt = _random.nextInt(1 << 32).toRadixString(16).padLeft(8, '0');
    return 'local-$micros-$salt';
  }

  /// A random (v4) UUID — the server's OrderId is a Guid.
  static String _newUuid() {
    final bytes = List<int>.generate(16, (_) => _random.nextInt(256));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    final hex =
        [for (final b in bytes) b.toRadixString(16).padLeft(2, '0')].join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-'
        '${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
  }

  static ValidationFailure _invalid(String message, {String field = 'cart'}) =>
      ValidationFailure(message, {
        field: [message],
      });
}
