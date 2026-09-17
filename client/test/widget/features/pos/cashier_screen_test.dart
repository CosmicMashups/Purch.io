import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/category_models.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';
import 'package:purch_client/features/pos/presentation/providers/pos_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/cashier_screen.dart';
import 'package:purch_client/features/pos/presentation/screens/tingi_weight_dialog.dart';

import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_pos_repository.dart';

/// Replaces the separate item_grid_screen_test / cart_screen_test: the grid
/// and the cart are one screen now, so their behaviour is covered against
/// [CashierScreen]. Every assertion from both original suites is carried
/// over — item tapping, customization routing, quantity edits, void, the
/// senior/PWD discount and promo codes.

const _water = Item(
  id: 'item-1',
  name: 'Bottled Water',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 15,
  imageUrl: null,
  pricingType: PricingType.unit,
  stockOnHand: 0,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

const _tShirt = Item(
  id: 'item-3',
  name: 'T-Shirt',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 200,
  imageUrl: null,
  pricingType: PricingType.variantMatrix,
  stockOnHand: 0,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

const _valueMeal = Item(
  id: 'item-4',
  name: 'Value Meal',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 150,
  imageUrl: null,
  pricingType: PricingType.combo,
  stockOnHand: 0,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

const _riceSack = Item(
  id: 'item-2',
  name: 'Rice (Sack)',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 60,
  imageUrl: null,
  pricingType: PricingType.weightVolume,
  stockOnHand: 50,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

const _cartWithOneLine = Transaction(
  id: 'cart-1',
  branchId: 'branch-1',
  deviceId: 'device-1',
  status: TransactionStatus.open,
  lines: [
    TransactionLine(
      id: 'line-1',
      itemId: 'item-1',
      itemName: 'Bottled Water',
      itemVariantId: null,
      quantity: 2,
      unitPrice: 15,
      lineTotal: 30,
      comboSelections: [],
    ),
  ],
  subtotal: 30,
  discountAmount: 0,
  seniorPwdDiscountApplied: false,
  promoCode: null,
  promoDiscountAmount: 0,
  totalAmount: 30,
  receiptNumber: null,
  payments: [],
);

const _drinksWater = Item(
  id: 'item-10',
  name: 'Bottled Water',
  sku: null,
  barcode: null,
  categoryId: 'cat-drinks',
  basePrice: 15,
  imageUrl: null,
  pricingType: PricingType.unit,
  stockOnHand: 0,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

const _snackChips = Item(
  id: 'item-11',
  name: 'Corn Chips',
  sku: null,
  barcode: null,
  categoryId: 'cat-snacks',
  basePrice: 25,
  imageUrl: null,
  pricingType: PricingType.unit,
  stockOnHand: 0,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

const _categories = [
  Category(id: 'cat-drinks', name: 'Drinks', sortOrder: 0),
  Category(id: 'cat-snacks', name: 'Snacks', sortOrder: 1),
];

/// A catalog whose categories endpoint is down but whose items are fine —
/// the cashier must still be able to sell.
class _CategoriesDownCatalogRepository extends FakeCatalogRepository {
  _CategoriesDownCatalogRepository({super.initialItems});

  @override
  Future<List<Category>> listCategories() async =>
      throw StateError('categories unavailable');
}

Widget _wrap(
  FakeCatalogRepository catalogRepository,
  FakePosRepository posRepository,
) {
  return ProviderScope(
    overrides: [
      catalogRepositoryProvider.overrideWithValue(catalogRepository),
      posRepositoryProvider.overrideWithValue(posRepository),
    ],
    child: const MaterialApp(home: CashierScreen()),
  );
}

void main() {
  group('item grid', () {
    testWidgets('shows an empty state when there are no active items', (
      tester,
    ) async {
      await tester.pumpWidget(
        _wrap(FakeCatalogRepository(), FakePosRepository()),
      );
      await tester.pumpAndSettle();

      expect(find.text('No active items to sell yet.'), findsOneWidget);
    });

    testWidgets('tapping a unit-priced item adds it to the cart', (
      tester,
    ) async {
      final catalogRepository = FakeCatalogRepository(initialItems: [_water]);
      final posRepository = FakePosRepository();
      await tester.pumpWidget(_wrap(catalogRepository, posRepository));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Bottled Water'));
      await tester.pumpAndSettle();

      expect(posRepository.cart.lines, hasLength(1));
      expect(find.text('Added Bottled Water'), findsOneWidget);
      // The side cart panel is live on the same screen — no push to a
      // separate cart route in between.
      expect(find.byType(CartPanel), findsOneWidget);
    });

    testWidgets('tapping a non-unit item explains it is not addable yet', (
      tester,
    ) async {
      final catalogRepository = FakeCatalogRepository(
        initialItems: [_riceSack],
      );
      final posRepository = FakePosRepository();
      await tester.pumpWidget(_wrap(catalogRepository, posRepository));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Rice (Sack)'));
      await tester.pumpAndSettle();

      // weightVolume items open the TingiWeightDialog for portion entry —
      // they do NOT show a snack bar.  Cart must remain empty.
      expect(posRepository.cart.lines, isEmpty);
      expect(find.byType(TingiWeightDialog), findsOneWidget);
    });

    testWidgets('tapping a variant-matrix item opens the variant picker', (
      tester,
    ) async {
      final catalogRepository = FakeCatalogRepository(initialItems: [_tShirt]);
      await tester.pumpWidget(_wrap(catalogRepository, FakePosRepository()));
      await tester.pumpAndSettle();

      await tester.tap(find.text('T-Shirt'));
      await tester.pumpAndSettle();

      expect(
        find.text('No variants have been configured for this item yet.'),
        findsOneWidget,
      );
    });

    testWidgets('tapping a combo item opens the combo customization screen', (
      tester,
    ) async {
      final catalogRepository = FakeCatalogRepository(
        initialItems: [_valueMeal],
      );
      await tester.pumpWidget(_wrap(catalogRepository, FakePosRepository()));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Value Meal'));
      await tester.pumpAndSettle();

      expect(
        find.text('No slots have been configured for this combo yet.'),
        findsOneWidget,
      );
    });
  });

  group('cart panel', () {
    testWidgets('shows an empty state when the cart has no lines', (
      tester,
    ) async {
      await tester.pumpWidget(
        _wrap(FakeCatalogRepository(), FakePosRepository()),
      );
      await tester.pumpAndSettle();

      expect(
        find.text('Cart is empty — tap an item to start the order.'),
        findsOneWidget,
      );
    });

    testWidgets('shows line totals and the running total', (tester) async {
      final repository = FakePosRepository(initialCart: _cartWithOneLine);
      await tester.pumpWidget(_wrap(FakeCatalogRepository(), repository));
      await tester.pumpAndSettle();

      expect(find.text('Bottled Water'), findsOneWidget);
      expect(find.text('₱30.00'), findsWidgets);
    });

    testWidgets('tapping + increases the line quantity', (tester) async {
      final repository = FakePosRepository(initialCart: _cartWithOneLine);
      await tester.pumpWidget(_wrap(FakeCatalogRepository(), repository));
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.add_circle_outline));
      await tester.pumpAndSettle();

      expect(repository.cart.lines.single.quantity, 3);
    });

    testWidgets('tapping remove clears the line', (tester) async {
      final repository = FakePosRepository(initialCart: _cartWithOneLine);
      await tester.pumpWidget(_wrap(FakeCatalogRepository(), repository));
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.delete_outline));
      await tester.pumpAndSettle();

      expect(repository.cart.lines, isEmpty);
    });

    testWidgets('voiding the cart after confirming starts a fresh one', (
      tester,
    ) async {
      final repository = FakePosRepository(initialCart: _cartWithOneLine);
      await tester.pumpWidget(_wrap(FakeCatalogRepository(), repository));
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.delete_sweep));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Void'));
      await tester.pumpAndSettle();

      expect(repository.voidCallCount, 1);
      expect(
        find.text('Cart is empty — tap an item to start the order.'),
        findsOneWidget,
      );
    });

    testWidgets('toggling the senior/PWD switch applies the 20% discount', (
      tester,
    ) async {
      final repository = FakePosRepository(initialCart: _cartWithOneLine);
      await tester.pumpWidget(_wrap(FakeCatalogRepository(), repository));
      await tester.pumpAndSettle();

      await tester.tap(find.byType(SwitchListTile));
      await tester.pumpAndSettle();

      expect(repository.cart.seniorPwdDiscountApplied, isTrue);
      expect(find.text('₱-6.00'), findsOneWidget);
      expect(find.text('₱24.00'), findsOneWidget);
    });

    testWidgets('applying a promo code shows it as applied with a Remove '
        'action', (tester) async {
      final repository = FakePosRepository(initialCart: _cartWithOneLine);
      await tester.pumpWidget(_wrap(FakeCatalogRepository(), repository));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField), 'SAVE10');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Apply'));
      await tester.pumpAndSettle();

      expect(repository.lastApplyPromoCodeRequest?.code, 'SAVE10');
      expect(repository.cart.promoCode, 'SAVE10');
      expect(find.text('Promo code "SAVE10" applied'), findsOneWidget);
    });

    testWidgets('removing an applied promo code clears it', (tester) async {
      final repository = FakePosRepository(initialCart: _cartWithOneLine);
      await tester.pumpWidget(_wrap(FakeCatalogRepository(), repository));
      await tester.pumpAndSettle();

      await tester.enterText(find.byType(TextField), 'SAVE10');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Apply'));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(TextButton, 'Remove'));
      await tester.pumpAndSettle();

      expect(repository.cart.promoCode, isNull);
      expect(find.text('Promo code "SAVE10" applied'), findsNothing);
      expect(find.widgetWithText(FilledButton, 'Apply'), findsOneWidget);
    });
  });

  testWidgets('Shift, Promos and X/Z are compact secondary actions', (
    tester,
  ) async {
    await tester.pumpWidget(
      _wrap(FakeCatalogRepository(), FakePosRepository()),
    );
    await tester.pumpAndSettle();

    expect(find.text('Shift'), findsOneWidget);
    expect(find.text('Promos'), findsOneWidget);
    expect(find.text('X / Z'), findsOneWidget);
  });

  group('category selector', () {
    testWidgets('picking a category filters the grid to that category', (
      tester,
    ) async {
      final catalogRepository = FakeCatalogRepository(
        initialItems: [_drinksWater, _snackChips],
        initialCategories: _categories,
      );
      await tester.pumpWidget(_wrap(catalogRepository, FakePosRepository()));
      await tester.pumpAndSettle();

      // "All" is selected by default.
      expect(find.text('Bottled Water'), findsOneWidget);
      expect(find.text('Corn Chips'), findsOneWidget);

      await tester.tap(find.text('Snacks'));
      await tester.pumpAndSettle();

      expect(find.text('Corn Chips'), findsOneWidget);
      expect(find.text('Bottled Water'), findsNothing);

      await tester.tap(find.text('All'));
      await tester.pumpAndSettle();

      expect(find.text('Bottled Water'), findsOneWidget);
      expect(find.text('Corn Chips'), findsOneWidget);
    });

    testWidgets('a category with no active items says so, and offers the way '
        'back to everything', (tester) async {
      final catalogRepository = FakeCatalogRepository(
        initialItems: [_drinksWater],
        initialCategories: _categories,
      );
      await tester.pumpWidget(_wrap(catalogRepository, FakePosRepository()));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Snacks'));
      await tester.pumpAndSettle();

      expect(find.text('Nothing in this category yet.'), findsOneWidget);
      expect(find.text('No active items to sell yet.'), findsNothing);

      await tester.tap(find.widgetWithText(FilledButton, 'Show All Items'));
      await tester.pumpAndSettle();

      expect(find.text('Bottled Water'), findsOneWidget);
    });

    testWidgets('no categories means no selector, and every item still sells', (
      tester,
    ) async {
      final catalogRepository = FakeCatalogRepository(
        initialItems: [_drinksWater],
      );
      await tester.pumpWidget(_wrap(catalogRepository, FakePosRepository()));
      await tester.pumpAndSettle();

      expect(find.byType(CategorySelector), findsOneWidget);
      expect(find.text('All'), findsNothing);
      expect(find.text('Bottled Water'), findsOneWidget);
    });

    testWidgets('a categories failure never blocks selling', (tester) async {
      final catalogRepository = _CategoriesDownCatalogRepository(
        initialItems: [_drinksWater],
      );
      final posRepository = FakePosRepository();
      await tester.pumpWidget(_wrap(catalogRepository, posRepository));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.text('All'), findsNothing);

      await tester.tap(find.text('Bottled Water'));
      await tester.pumpAndSettle();

      expect(posRepository.cart.lines, hasLength(1));
    });
  });

  group('narrow portrait layout', () {
    void usePhoneSurface(WidgetTester tester) {
      tester.view.physicalSize = const Size(400, 820);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
    }

    testWidgets('the app bar fits, with the secondary tools behind one menu', (
      tester,
    ) async {
      usePhoneSurface(tester);
      await tester.pumpWidget(
        _wrap(
          FakeCatalogRepository(
            initialItems: [_drinksWater, _snackChips],
            initialCategories: _categories,
          ),
          FakePosRepository(),
        ),
      );
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      // Collapsed: the three labelled buttons are gone from the bar itself.
      expect(find.text('Shift'), findsNothing);
      expect(find.text('Promos'), findsNothing);
      expect(find.text('X / Z'), findsNothing);

      await tester.tap(find.byIcon(Icons.more_vert_rounded));
      await tester.pumpAndSettle();

      expect(find.text('Shift'), findsOneWidget);
      expect(find.text('Promos'), findsOneWidget);
      expect(find.text('X / Z'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('the category selector becomes a strip and still filters', (
      tester,
    ) async {
      usePhoneSurface(tester);
      await tester.pumpWidget(
        _wrap(
          FakeCatalogRepository(
            initialItems: [_drinksWater, _snackChips],
            initialCategories: _categories,
          ),
          FakePosRepository(),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('Drinks'));
      await tester.pumpAndSettle();

      expect(find.text('Bottled Water'), findsOneWidget);
      expect(find.text('Corn Chips'), findsNothing);
      expect(tester.takeException(), isNull);
    });
  });
}
