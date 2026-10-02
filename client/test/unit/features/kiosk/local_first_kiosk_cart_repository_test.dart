import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/item_variant_models.dart';
import 'package:purch_client/features/catalog/domain/modifier_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/kiosk/data/local_first_kiosk_cart_repository.dart';
import 'package:purch_client/features/kiosk/domain/place_kiosk_order_request.dart';
import 'package:purch_client/features/pos/data/local_first_pos_repository.dart';
import 'package:purch_client/features/pos/domain/item_promo_models.dart';
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

Transaction _submitted({int kioskPrepNumber = 42}) => Transaction(
  id: 'server-order-1',
  branchId: 'b',
  deviceId: 'd',
  status: TransactionStatus.awaitingPayment,
  lines: const [],
  subtotal: 0,
  discountAmount: 0,
  seniorPwdDiscountApplied: false,
  promoCode: null,
  promoDiscountAmount: 0,
  totalAmount: 0,
  receiptNumber: null,
  originatedFromKiosk: true,
  kioskPrepNumber: kioskPrepNumber,
  payments: const [],
);

void main() {
  late FakeCatalogRepository catalog;
  late List<Item> items;
  final placeOrderRequests = <PlaceKioskOrderRequest>[];
  Object? placeOrderFailure;
  Future<PricingRules> Function()? loadRules;

  LocalFirstKioskCartRepository build() {
    return LocalFirstKioskCartRepository(
      placeOrder: (request) async {
        placeOrderRequests.add(request);
        if (placeOrderFailure != null) {
          throw placeOrderFailure!;
        }
        return _submitted();
      },
      catalog: catalog,
      loadItems: () async => items,
      loadRules: loadRules,
      store: MemoryCartDraftStore(),
      identity: () async => const CartIdentity(
        tenantId: 't',
        deviceId: 'd',
        branchId: 'b',
      ),
    );
  }

  setUp(() {
    placeOrderRequests.clear();
    placeOrderFailure = null;
    loadRules = null;
    items = [_item('latte', 'Iced Latte', 150)];
    catalog = FakeCatalogRepository(initialItems: items);
  });

  test('getOrCreateOpenCart starts empty with no network call', () async {
    final repo = build();
    final cart = await repo.getOrCreateOpenCart();
    expect(cart.lines, isEmpty);
    expect(cart.totalAmount, 0);
    expect(placeOrderRequests, isEmpty);
  });

  test('addLine prices instantly from the cached catalog', () async {
    final repo = build();
    final cart = await repo.addLine(
      const AddTransactionLineRequest(itemId: 'latte', quantity: 2),
    );
    expect(cart.lines, hasLength(1));
    expect(cart.lines.single.unitPrice, 150);
    expect(cart.totalAmount, 300);
    expect(placeOrderRequests, isEmpty);
  });

  test('totals the cart with the automatic item promotions the server publishes', () async {
    loadRules = () async => const PricingRules(
      itemDiscount: [
        ItemDiscountPromoRule(
          id: 'r1',
          name: 'Merienda',
          itemId: 'latte',
          discountType: PromoDiscountType.percentage,
          discountValue: 10,
          startsAt: null,
          endsAt: null,
          isActive: true,
        ),
      ],
    );
    final repo = build();
    final cart = await repo.addLine(
      const AddTransactionLineRequest(itemId: 'latte', quantity: 2),
    );

    expect(cart.subtotal, 300);
    expect(cart.itemPromoDiscountAmount, 30);
    expect(cart.totalAmount, 270);
    expect(cart.lines.single.appliedPromoLabel, '10% OFF');
  });

  test('falls back to the pre-promo total when the rules cannot be loaded', () async {
    loadRules = () async => throw const NetworkFailure('offline');
    final repo = build();
    final cart = await repo.addLine(
      const AddTransactionLineRequest(itemId: 'latte', quantity: 2),
    );

    expect(cart.totalAmount, 300);
    expect(cart.itemPromoDiscountAmount, 0);
  });

  test('a second plain add of the same item merges into one line', () async {
    final repo = build();
    await repo.addLine(
      const AddTransactionLineRequest(itemId: 'latte', quantity: 1),
    );
    final cart = await repo.addLine(
      const AddTransactionLineRequest(itemId: 'latte', quantity: 1),
    );
    expect(cart.lines, hasLength(1));
    expect(cart.lines.single.quantity, 2);
  });

  test('rejects adding an inactive item before it ever reaches the cart', () async {
    items = [_item('gone', 'Discontinued', 50, active: false)];
    catalog = FakeCatalogRepository(initialItems: items);
    final repo = build();
    await expectLater(
      repo.addLine(const AddTransactionLineRequest(itemId: 'gone', quantity: 1)),
      throwsA(isA<ValidationFailure>()),
    );
  });

  test('prices a variant at its override price', () async {
    items = [_item('shirt', 'Shirt', 100, type: PricingType.variantMatrix)];
    catalog = FakeCatalogRepository(
      initialItems: items,
      initialVariants: [
        ItemVariant(
          id: 'v1',
          attributes: const {'Size': 'Large'},
          sku: null,
          stockOnHand: 5,
          priceOverride: 120,
          imageUrl: null,
        ),
      ],
    );
    final repo = build();
    final cart = await repo.addLine(
      const AddTransactionLineRequest(itemId: 'shirt', itemVariantId: 'v1', quantity: 1),
    );
    expect(cart.lines.single.unitPrice, 120);
    expect(cart.lines.single.itemVariantAttributes, {'Size': 'Large'});
  });

  test('adds modifier price deltas on top of the base price', () async {
    catalog = FakeCatalogRepository(
      initialItems: items,
      initialItemModifierGroups: {
        'latte': [
          ModifierGroup(
            id: 'g1',
            name: 'Extras',
            allowMultipleSelection: true,
            isRequired: false,
            modifiers: const [
              ItemModifierOption(id: 'm1', name: 'Extra shot', priceDelta: 25),
            ],
          ),
        ],
      },
    );
    final repo = build();
    final cart = await repo.addLine(
      const AddTransactionLineRequest(
        itemId: 'latte',
        quantity: 1,
        selectedModifierIds: ['m1'],
      ),
    );
    expect(cart.lines.single.unitPrice, 175);
  });

  test('updateLine changes the quantity locally', () async {
    final repo = build();
    final added = await repo.addLine(
      const AddTransactionLineRequest(itemId: 'latte', quantity: 1),
    );
    final updated = await repo.updateLine(
      added.lines.single.id,
      const UpdateTransactionLineRequest(quantity: 3),
    );
    expect(updated.lines.single.quantity, 3);
    expect(updated.totalAmount, 450);
  });

  test('removeLine drops the line locally', () async {
    final repo = build();
    final added = await repo.addLine(
      const AddTransactionLineRequest(itemId: 'latte', quantity: 1),
    );
    final removed = await repo.removeLine(added.lines.single.id);
    expect(removed.lines, isEmpty);
  });

  test('setOrderType is kept on the draft and requires a value', () async {
    final repo = build();
    final cart = await repo.setOrderType(
      const SetOrderTypeRequest(orderType: 'Take Out'),
    );
    expect(cart.orderType, 'Take Out');
    await expectLater(
      repo.setOrderType(const SetOrderTypeRequest(orderType: '')),
      throwsA(isA<ValidationFailure>()),
    );
  });

  test('submitOrder sends every local line in one call and resets the draft', () async {
    final repo = build();
    await repo.addLine(
      const AddTransactionLineRequest(itemId: 'latte', quantity: 2),
    );
    await repo.setOrderType(const SetOrderTypeRequest(orderType: 'Take Out'));

    final submitted = await repo.submitOrder();

    expect(submitted.kioskPrepNumber, 42);
    expect(placeOrderRequests, hasLength(1));
    expect(placeOrderRequests.single.orderType, 'Take Out');
    expect(placeOrderRequests.single.lines, hasLength(1));
    expect(placeOrderRequests.single.lines.single.quantity, 2);

    final nextCart = await repo.getOrCreateOpenCart();
    expect(nextCart.lines, isEmpty);
  });

  test('submitOrder refuses an empty cart without calling the server', () async {
    final repo = build();
    await expectLater(repo.submitOrder(), throwsA(isA<ValidationFailure>()));
    expect(placeOrderRequests, isEmpty);
  });

  test('submitOrder refuses a cart with no order type chosen yet', () async {
    final repo = build();
    await repo.addLine(
      const AddTransactionLineRequest(itemId: 'latte', quantity: 1),
    );
    await expectLater(repo.submitOrder(), throwsA(isA<ValidationFailure>()));
    expect(placeOrderRequests, isEmpty);
  });

  test('retries the same order id on a retried submit, leaving the cart to fix on failure', () async {
    placeOrderFailure = const NotFoundFailure('An item was unavailable.');
    final repo = build();
    await repo.addLine(
      const AddTransactionLineRequest(itemId: 'latte', quantity: 1),
    );
    await repo.setOrderType(const SetOrderTypeRequest(orderType: 'Take Out'));

    await expectLater(repo.submitOrder(), throwsA(isA<NotFoundFailure>()));

    // The cart was not cleared, so the customer can fix it and retry.
    final cart = await repo.getOrCreateOpenCart();
    expect(cart.lines, hasLength(1));

    placeOrderFailure = null;
    await repo.submitOrder();
    expect(placeOrderRequests, hasLength(2));
    expect(placeOrderRequests[0].orderId, placeOrderRequests[1].orderId);
  });

  test('clear drops the draft without ever calling the server', () async {
    final repo = build();
    await repo.addLine(
      const AddTransactionLineRequest(itemId: 'latte', quantity: 1),
    );

    await repo.clear();

    final cart = await repo.getOrCreateOpenCart();
    expect(cart.lines, isEmpty);
    expect(placeOrderRequests, isEmpty);
  });
}
