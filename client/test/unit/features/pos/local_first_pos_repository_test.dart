import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/item_variant_models.dart';
import 'package:purch_client/features/catalog/domain/modifier_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/pos/data/local_first_pos_repository.dart';
import 'package:purch_client/features/pos/domain/item_promo_models.dart';
import 'package:purch_client/features/pos/domain/payment_method.dart';
import 'package:purch_client/features/pos/domain/pos_repository.dart';
import 'package:purch_client/features/pos/domain/pricing_engine.dart';
import 'package:purch_client/features/pos/domain/promo_code_models.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';

import '../../../helpers/fake_catalog_repository.dart';

Item _item(
  String id,
  String name,
  double price, {
  PricingType type = PricingType.unit,
  bool active = true,
}) => Item(
  id: id,
  name: name,
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: price,
  imageUrl: null,
  pricingType: type,
  stockOnHand: 10,
  isActive: active,
  tingiMode: TingiMode.values.first,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: const [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

/// Records every call so tests can assert the server is left alone until
/// checkout, and lets a test decide what total the "server" prices the cart at.
class _RecordingRemote implements PosRepository {
  final calls = <String>[];
  String openCartId = 'server-cart-1';
  int openCartLines = 0;
  double? serverTotal;
  Object? payFailure;
  int _cartCounter = 1;

  Transaction _tx({
    double total = 0,
    int lines = 0,
    TransactionStatus status = TransactionStatus.open,
    int? receipt,
  }) => Transaction(
    id: openCartId,
    branchId: 'b',
    deviceId: 'd',
    status: status,
    lines: [
      for (var i = 0; i < lines; i++)
        TransactionLine(
          id: 'l$i',
          itemId: 'x',
          itemName: 'x',
          itemVariantId: null,
          quantity: 1,
          unitPrice: 1,
          lineTotal: 1,
          comboSelections: const [],
        ),
    ],
    subtotal: total,
    discountAmount: 0,
    seniorPwdDiscountApplied: false,
    promoCode: null,
    promoDiscountAmount: 0,
    totalAmount: total,
    receiptNumber: receipt,
    payments: const [],
  );

  /// What the fake server did with each checkout, keyed by sale id — models the
  /// real endpoint's idempotency (same saleId -> the sale already recorded).
  final completedBySaleId = <String, Transaction>{};
  final usedReceiptNumbers = <int>{};
  int serverLastReceipt = 0;

  @override
  Future<int> getLastIssuedReceiptNumber() async => serverLastReceipt;

  final checkoutRequests = <CheckoutRequest>[];
  Object? checkoutFailure;
  int recordedSales = 0;

  @override
  Future<Transaction> checkout(CheckoutRequest request) async {
    calls.add('checkout:${request.lines.length}');
    checkoutRequests.add(request);
    if (checkoutFailure != null) {
      throw checkoutFailure!;
    }
    final replay = completedBySaleId[request.saleId];
    if (replay != null) {
      return replay;
    }
    final number = request.receiptNumber ?? 41 + recordedSales + 1;
    if (!usedReceiptNumbers.add(number)) {
      throw ConflictFailure('Receipt number $number was already issued to this terminal.');
    }
    serverLastReceipt = number > serverLastReceipt ? number : serverLastReceipt;
    recordedSales++;
    return completedBySaleId[request.saleId] = _tx(
      total: serverTotal ?? request.expectedTotal ?? 0,
      status: TransactionStatus.completed,
      receipt: number,
    );
  }

  @override
  Future<Transaction> getOrCreateOpenCart() async {
    calls.add('getCart');
    return _tx(lines: openCartLines);
  }

  @override
  Future<Transaction> addLine(AddTransactionLineRequest request) async {
    calls.add('addLine:${request.itemId}x${request.quantity}');
    openCartLines++;
    return _tx(total: serverTotal ?? 0, lines: openCartLines);
  }

  @override
  Future<Transaction> voidCart() async {
    calls.add('void');
    openCartLines = 0;
    openCartId = 'server-cart-${++_cartCounter}';
    return _tx(status: TransactionStatus.voided);
  }

  @override
  Future<Transaction> applySeniorPwdDiscount(
    ApplySeniorPwdDiscountRequest request,
  ) async {
    calls.add('senior:${request.apply}');
    return _tx(total: serverTotal ?? 0, lines: openCartLines);
  }

  @override
  Future<Transaction> applyPromoCode(ApplyPromoCodeRequest request) async {
    calls.add('promo:${request.code}');
    return _tx(total: serverTotal ?? 0, lines: openCartLines);
  }

  @override
  Future<Transaction> setOrderType(SetOrderTypeRequest request) async {
    calls.add('orderType:${request.orderType}');
    return _tx(total: serverTotal ?? 0, lines: openCartLines);
  }

  @override
  Future<Transaction> recordPayment(RecordPaymentRequest request) async {
    calls.add('pay');
    if (payFailure != null) {
      throw payFailure!;
    }
    final paid = _tx(
      total: serverTotal ?? 0,
      lines: openCartLines,
      status: TransactionStatus.completed,
      receipt: 42,
    );
    openCartLines = 0;
    openCartId = 'server-cart-${++_cartCounter}';
    return paid;
  }

  @override
  Future<Transaction> updateLine(
    String lineId,
    UpdateTransactionLineRequest request,
  ) => throw UnimplementedError();

  @override
  Future<Transaction> removeLine(String lineId) => throw UnimplementedError();

  @override
  Future<List<Transaction>> listPendingKioskOrders(String branchId) async =>
      const [];

  @override
  Future<Transaction> claimKioskOrder(String transactionId) async {
    calls.add('claim');
    return _tx(total: 5, lines: 1);
  }
}

void main() {
  late _RecordingRemote remote;
  late MemoryCartDraftStore store;
  late FakeCatalogRepository catalog;
  late List<Item> items;
  late PricingRules rules;
  late int localCounter;
  late List<int> retiredNumbers;
  late int floorFromServer;

  Future<int> lastIssued({bool refresh = false}) async {
    if (refresh) {
      floorFromServer = await remote.getLastIssuedReceiptNumber();
    }
    return localCounter > floorFromServer ? localCounter : floorFromServer;
  }

  Future<void> record(int number) async {
    retiredNumbers.add(number);
    if (number > localCounter) {
      localCounter = number;
    }
  }

  LocalFirstPosRepository build() => LocalFirstPosRepository(
    lastIssuedReceiptNumber: lastIssued,
    recordReceiptNumber: record,
    remote: remote,
    catalog: catalog,
    loadItems: () async => items,
    loadRules: () async => rules,
    store: store,
    identity:
        () async => const CartIdentity(
          tenantId: 't',
          deviceId: 'd',
          branchId: 'b',
        ),
  );

  AddTransactionLineRequest add(String id, [double qty = 1, String? variant]) =>
      AddTransactionLineRequest(
        itemId: id,
        quantity: qty,
        itemVariantId: variant,
      );

  setUp(() {
    remote = _RecordingRemote();
    store = MemoryCartDraftStore();
    localCounter = 0;
    floorFromServer = 0;
    retiredNumbers = [];
    items = [
      _item('coffee', 'Coffee', 100),
      _item('shirt', 'Shirt', 200, type: PricingType.variantMatrix),
      _item('latte', 'Latte', 120),
      _item('off', 'Retired', 5, active: false),
    ];
    rules = PricingRules.empty;
    catalog = FakeCatalogRepository(
      initialVariants: const [
        ItemVariant(
          id: 'v-l',
          attributes: {'Size': 'L'},
          sku: null,
          stockOnHand: 3,
          priceOverride: 250,
          imageUrl: null,
        ),
      ],
      initialItemModifierGroups: {
        'latte': const [
          ModifierGroup(
            id: 'g',
            name: 'Milk',
            allowMultipleSelection: false,
            isRequired: true,
            modifiers: [
              ItemModifierOption(id: 'm-oat', name: 'Oat', priceDelta: 15),
              ItemModifierOption(id: 'm-soy', name: 'Soy', priceDelta: 10),
            ],
          ),
        ],
      },
    );
  });

  test('cart edits are local: no server calls, totals update instantly', () async {
    final repo = build();
    var cart = await repo.getOrCreateOpenCart();
    expect(cart.lines, isEmpty);

    cart = await repo.addLine(add('coffee', 2));
    expect(cart.totalAmount, 200);
    cart = await repo.addLine(add('coffee'));
    expect(cart.lines, hasLength(1), reason: 'same item merges into one line');
    expect(cart.lines.single.quantity, 3);
    expect(cart.totalAmount, 300);

    cart = await repo.updateLine(cart.lines.single.id, const UpdateTransactionLineRequest(quantity: 1));
    expect(cart.totalAmount, 100);
    cart = await repo.removeLine(cart.lines.single.id);
    expect(cart.lines, isEmpty);

    expect(remote.calls, isEmpty);
  });

  test('overlapping adds (a double scan) are all kept, none overwrites another', () async {
    final repo = build();

    // Fired without awaiting in between, like a scanner or a double tap.
    final results = await Future.wait([
      repo.addLine(add('coffee')),
      repo.addLine(add('coffee')),
      repo.addLine(add('coffee', 2)),
    ]);

    expect(results.last.lines.single.quantity, 4);
    final cart = await repo.getOrCreateOpenCart();
    expect(cart.lines.single.quantity, 4);
    expect(cart.totalAmount, 400);
  });

  test('the draft survives an app restart', () async {
    await build().addLine(add('coffee', 2));

    final restarted = await build().getOrCreateOpenCart();
    expect(restarted.lines.single.quantity, 2);
    expect(restarted.totalAmount, 200);
  });

  test('a corrupt saved draft starts a fresh cart instead of crashing', () async {
    await store.write('{not json');
    final cart = await build().getOrCreateOpenCart();
    expect(cart.lines, isEmpty);
  });

  test('variant price overrides the base price and lines stay separate from plain ones', () async {
    final repo = build();
    final cart = await repo.addLine(add('shirt', 1, 'v-l'));
    expect(cart.lines.single.unitPrice, 250);
    expect(cart.lines.single.itemVariantId, 'v-l');
    expect(cart.lines.single.variantAttributesLabel, 'Size: L');
  });

  test('a variant item without a variant, or an inactive item, is rejected', () async {
    final repo = build();
    await expectLater(repo.addLine(add('shirt')), throwsA(isA<ValidationFailure>()));
    await expectLater(repo.addLine(add('off')), throwsA(isA<ValidationFailure>()));
    await expectLater(repo.addLine(add('coffee', 0)), throwsA(isA<ValidationFailure>()));
  });

  test('required modifier groups are enforced and priced into the unit price', () async {
    final repo = build();
    await expectLater(
      repo.addLine(add('latte')),
      throwsA(isA<ValidationFailure>()),
    );

    final cart = await repo.addLine(
      const AddTransactionLineRequest(
        itemId: 'latte',
        quantity: 2,
        selectedModifierIds: ['m-oat'],
      ),
    );
    expect(cart.lines.single.unitPrice, 135);
    expect(cart.totalAmount, 270);
    expect(cart.lines.single.modifierSelections.single.modifierName, 'Oat');
  });

  test('lines with different modifiers do not merge', () async {
    final repo = build();
    await repo.addLine(
      const AddTransactionLineRequest(
        itemId: 'latte',
        quantity: 1,
        selectedModifierIds: ['m-oat'],
      ),
    );
    final cart = await repo.addLine(
      const AddTransactionLineRequest(
        itemId: 'latte',
        quantity: 1,
        selectedModifierIds: ['m-soy'],
      ),
    );
    expect(cart.lines, hasLength(2));
  });

  test('item promos, Senior/PWD and promo codes are priced locally', () async {
    rules = PricingRules(
      bogo: [
        BogoPromoRule(
          id: 'r',
          name: 'B1T1',
          triggerItemId: 'coffee',
          triggerQuantity: 1,
          freeItemId: 'coffee',
          freeQuantity: 1,
          startsAt: null,
          endsAt: null,
          isActive: true,
        ),
      ],
      promoCodes: [
        PromoCode(
          id: 'p',
          code: 'TEN',
          discountType: PromoDiscountType.percentage,
          discountValue: 10,
          isActive: true,
          expiresAt: null,
        ),
      ],
    );
    final repo = build();
    var cart = await repo.addLine(add('coffee', 2));
    expect(cart.itemPromoDiscountAmount, 100);
    expect(cart.lines.single.appliedPromoLabel, 'B1T1');
    expect(cart.totalAmount, 100);

    cart = await repo.applySeniorPwdDiscount(
      const ApplySeniorPwdDiscountRequest(apply: true),
    );
    expect(cart.totalAmount, 80);

    cart = await repo.applyPromoCode(const ApplyPromoCodeRequest(code: 'ten'));
    expect(cart.promoCode, 'TEN');
    expect(cart.totalAmount, 70);

    await expectLater(
      repo.applyPromoCode(const ApplyPromoCodeRequest(code: 'NOPE')),
      throwsA(isA<ValidationFailure>()),
    );
    expect(remote.calls, isEmpty);
  });

  test('promo rules failing to load never breaks the cart', () async {
    final repo = LocalFirstPosRepository(
      lastIssuedReceiptNumber: lastIssued,
      recordReceiptNumber: record,
      remote: remote,
      catalog: catalog,
      loadItems: () async => items,
      loadRules: () async => throw const NetworkFailure('offline'),
      store: store,
      identity: () async => null,
    );
    final cart = await repo.addLine(add('coffee', 2));
    expect(cart.totalAmount, 200);
  });

  test('paying sends the whole sale in one checkout call and clears the draft', () async {
    rules = PricingRules(
      promoCodes: [
        PromoCode(
          id: 'p',
          code: 'TEN',
          discountType: PromoDiscountType.percentage,
          discountValue: 10,
          isActive: true,
          expiresAt: null,
        ),
      ],
    );
    final repo = build();
    await repo.addLine(add('coffee', 2));
    await repo.addLine(add('latte', 1).copyWithModifiers(['m-soy']));
    await repo.applyPromoCode(const ApplyPromoCodeRequest(code: 'ten'));
    await repo.setOrderType(const SetOrderTypeRequest(orderType: 'Dine In'));
    expect(remote.calls, isEmpty);

    final paid = await repo.recordPayment(
      const RecordPaymentRequest(
        method: PaymentMethod.cash,
        amountTendered: 500,
      ),
    );

    expect(paid.receiptNumber, 1);
    expect(remote.calls, ['checkout:2'], reason: 'one call, not one per line');

    final sent = remote.checkoutRequests.single;
    expect(sent.lines.map((l) => l.itemId), ['coffee', 'latte']);
    expect(sent.lines.last.selectedModifierIds, ['m-soy']);
    expect(sent.promoCode, 'TEN');
    expect(sent.orderType, 'Dine In');
    expect(sent.payment.amountTendered, 500);
    // coffee 200 + latte 130 = 330, less 10% promo = 297.
    expect(sent.expectedTotal, closeTo(297, 0.001));
    expect(sent.toJson()['saleId'], sent.saleId);
    expect(sent.saleId, matches(RegExp(r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$')));

    // Next sale starts empty.
    expect((await repo.getOrCreateOpenCart()).lines, isEmpty);
    expect(await store.read(), isNull);
  });

  test('a price change reported by the server surfaces, keeps the cart, and reloads promo rules', () async {
    var ruleLoads = 0;
    final repo = LocalFirstPosRepository(
      lastIssuedReceiptNumber: lastIssued,
      recordReceiptNumber: record,
      remote: remote,
      catalog: catalog,
      loadItems: () async => items,
      loadRules: () async {
        ruleLoads++;
        return rules;
      },
      store: store,
      identity: () async => null,
    );
    remote.checkoutFailure = const ConflictFailure(
      'Prices or promos changed: the total is now 180.00',
    );
    await repo.addLine(add('coffee', 2));
    final loadsBeforePay = ruleLoads;

    await expectLater(
      repo.recordPayment(
        const RecordPaymentRequest(method: PaymentMethod.cash, amountTendered: 200),
      ),
      throwsA(isA<ConflictFailure>()),
    );

    expect((await repo.getOrCreateOpenCart()).lines, hasLength(1));
    expect(ruleLoads, greaterThan(loadsBeforePay), reason: 'rules refetched for the retry');
  });

  test('retrying after a lost response reuses the sale id, so the customer is charged once', () async {
    final repo = build();
    await repo.addLine(add('coffee', 2));
    const payment = RecordPaymentRequest(
      method: PaymentMethod.cash,
      amountTendered: 200,
    );

    remote.checkoutFailure = const NetworkFailure('response lost');
    await expectLater(repo.recordPayment(payment), throwsA(isA<NetworkFailure>()));

    // The request actually reached the server and was recorded before the
    // connection dropped — model that, then retry.
    remote.checkoutFailure = null;
    final sale = await repo.recordPayment(payment);
    final again = remote.checkoutRequests;

    expect(again, hasLength(2));
    expect(again.first.saleId, again.last.saleId);
    expect(sale.receiptNumber, 1);
    expect(remote.recordedSales, 1);
  });

  test('the sale id survives an app restart mid-payment', () async {
    await build().addLine(add('coffee'));
    remote.checkoutFailure = const NetworkFailure('offline');
    const payment = RecordPaymentRequest(
      method: PaymentMethod.cash,
      amountTendered: 100,
    );
    await expectLater(build().recordPayment(payment), throwsA(isA<NetworkFailure>()));

    remote.checkoutFailure = null;
    await build().recordPayment(payment);

    expect(remote.checkoutRequests.first.saleId, remote.checkoutRequests.last.saleId);
    expect(remote.recordedSales, 1);
  });

  test('each new sale gets its own sale id', () async {
    final repo = build();
    const payment = RecordPaymentRequest(
      method: PaymentMethod.cash,
      amountTendered: 100,
    );
    await repo.addLine(add('coffee'));
    await repo.recordPayment(payment);
    await repo.addLine(add('coffee'));
    await repo.recordPayment(payment);

    final ids = remote.checkoutRequests.map((r) => r.saleId).toSet();
    expect(ids, hasLength(2));
    expect(remote.recordedSales, 2);
  });

  group('device-issued receipt numbers', () {
    const payment = RecordPaymentRequest(
      method: PaymentMethod.cash,
      amountTendered: 100,
    );

    test('each sale is numbered by the device, in sequence, after the highest known number', () async {
      floorFromServer = 41; // the server already recorded 41 sales for this terminal
      final repo = build();

      await repo.addLine(add('coffee'));
      final first = await repo.recordPayment(payment);
      await repo.addLine(add('coffee'));
      final second = await repo.recordPayment(payment);

      expect(first.receiptNumber, 42);
      expect(second.receiptNumber, 43);
      expect(remote.checkoutRequests.map((r) => r.receiptNumber), [42, 43]);
      expect(localCounter, 43);
    });

    test('a retry after a lost response sends the SAME number, and it is used once', () async {
      final repo = build();
      await repo.addLine(add('coffee'));

      remote.checkoutFailure = const NetworkFailure('response lost');
      await expectLater(repo.recordPayment(payment), throwsA(isA<NetworkFailure>()));
      remote.checkoutFailure = null;
      final sale = await repo.recordPayment(payment);

      expect(remote.checkoutRequests.map((r) => r.receiptNumber), [1, 1]);
      expect(sale.receiptNumber, 1);
      expect(remote.usedReceiptNumbers, {1});
    });

    test('the reserved number survives an app restart mid-payment', () async {
      await build().addLine(add('coffee'));
      remote.checkoutFailure = const NetworkFailure('offline');
      await expectLater(build().recordPayment(payment), throwsA(isA<NetworkFailure>()));

      remote.checkoutFailure = null;
      final sale = await build().recordPayment(payment);
      expect(sale.receiptNumber, 1);
    });

    test('a definitive rejection does not use up the number', () async {
      final repo = build();
      await repo.addLine(add('coffee'));
      remote.checkoutFailure = const ConflictFailure('Prices or promos changed: 5.00');
      await expectLater(repo.recordPayment(payment), throwsA(isA<ConflictFailure>()));
      expect(retiredNumbers, isEmpty);

      remote.checkoutFailure = null;
      final sale = await repo.recordPayment(payment);
      expect(sale.receiptNumber, 1, reason: 'still the first number, no gap');
    });

    test('voiding after an unanswered payment attempt retires the number instead of reusing it', () async {
      final repo = build();
      await repo.addLine(add('coffee'));
      remote.checkoutFailure = const NetworkFailure('no answer');
      await expectLater(repo.recordPayment(payment), throwsA(isA<NetworkFailure>()));

      await repo.voidCart();
      expect(retiredNumbers, [1], reason: 'the server may have recorded number 1');

      remote.checkoutFailure = null;
      await repo.addLine(add('coffee'));
      final next = await repo.recordPayment(payment);
      expect(next.receiptNumber, 2);
    });

    test('voiding a cart that never attempted payment leaves the number available', () async {
      final repo = build();
      await repo.addLine(add('coffee'));
      await repo.voidCart();
      expect(retiredNumbers, isEmpty);

      await repo.addLine(add('coffee'));
      final sale = await repo.recordPayment(payment);
      expect(sale.receiptNumber, 1);
    });

    test('a number the server says is already taken is replaced from the server floor', () async {
      // Another install of this terminal already used 1-5, which this device never saw.
      remote.usedReceiptNumbers.addAll([1, 2, 3, 4, 5]);
      remote.serverLastReceipt = 5;
      final repo = build();
      await repo.addLine(add('coffee'));

      await expectLater(repo.recordPayment(payment), throwsA(isA<ConflictFailure>()));

      final sale = await repo.recordPayment(payment);
      expect(sale.receiptNumber, 6);
    });

    test('numbering falls back to the local counter when the server floor is unreachable', () async {
      localCounter = 9;
      final repo = LocalFirstPosRepository(
        remote: remote,
        catalog: catalog,
        loadItems: () async => items,
        loadRules: () async => rules,
        store: store,
        identity: () async => null,
        lastIssuedReceiptNumber: ({bool refresh = false}) async => localCounter,
        recordReceiptNumber: record,
      );
      await repo.addLine(add('coffee'));
      final sale = await repo.recordPayment(payment);
      expect(sale.receiptNumber, 10);
    });
  });

  test('an empty cart cannot be paid', () async {
    await expectLater(
      build().recordPayment(
        const RecordPaymentRequest(method: PaymentMethod.cash, amountTendered: 1),
      ),
      throwsA(isA<ValidationFailure>()),
    );
    expect(remote.calls, isEmpty);
  });

  test('voiding clears the local draft without touching the server', () async {
    final repo = build();
    await repo.addLine(add('coffee'));
    final voided = await repo.voidCart();
    expect(voided.status, TransactionStatus.voided);
    expect((await repo.getOrCreateOpenCart()).lines, isEmpty);
    expect(remote.calls, isEmpty);
  });

  test('a claimed kiosk order is served from the server, not the local draft', () async {
    final repo = build();
    await repo.claimKioskOrder('kiosk-1');
    expect(remote.calls, ['claim']);

    remote.calls.clear();
    await repo.getOrCreateOpenCart();
    expect(remote.calls, ['getCart']);

    // ...and it survives a restart, since the flag is persisted.
    remote.calls.clear();
    await build().getOrCreateOpenCart();
    expect(remote.calls, ['getCart']);
  });
}

extension on AddTransactionLineRequest {
  AddTransactionLineRequest copyWithModifiers(List<String> ids) =>
      AddTransactionLineRequest(
        itemId: itemId,
        itemVariantId: itemVariantId,
        quantity: quantity,
        comboSelections: comboSelections,
        selectedModifierIds: ids,
      );
}
