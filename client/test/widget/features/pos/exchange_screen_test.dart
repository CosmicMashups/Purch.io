import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/errors/failure.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/pos/domain/exchange_models.dart';
import 'package:purch_client/features/pos/domain/payment_method.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';
import 'package:purch_client/features/pos/presentation/providers/pos_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/exchange_screen.dart';

import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_pos_repository.dart';

const _sale = Transaction(
  id: 'sale-1',
  branchId: 'branch-1',
  deviceId: 'device-1',
  status: TransactionStatus.completed,
  lines: [
    TransactionLine(
      id: 'line-1',
      itemId: 'item-1',
      itemName: 'Iced Latte',
      itemVariantId: null,
      quantity: 2,
      unitPrice: 150,
      lineTotal: 300,
      comboSelections: [],
    ),
  ],
  subtotal: 300,
  discountAmount: 0,
  seniorPwdDiscountApplied: false,
  promoCode: null,
  promoDiscountAmount: 0,
  totalAmount: 300,
  receiptNumber: 1047,
  payments: [],
);

Item _item(String id, String name, double price, PricingType type) => Item(
  id: id,
  name: name,
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: price,
  imageUrl: null,
  pricingType: type,
  stockOnHand: 10,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: const [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: null,
);

const _result = Adjustment(
  originalReceiptNumber: 1047,
  approvedByName: 'Marisol',
  returnLines: [AdjustmentLine(itemName: 'Iced Latte', quantity: 1, lineTotal: 150)],
  replacementLines: [AdjustmentLine(itemName: 'Mocha', quantity: 1, lineTotal: 170)],
  priceDifference: 20,
  changeGiven: 180,
);

Future<FakePosRepository> _open(
  WidgetTester tester, {
  Map<String, double>? returnable,
}) async {
  final repository = FakePosRepository(initialCart: _sale)..returnableLines = returnable;
  final catalog = FakeCatalogRepository()
    ..items.addAll([
      _item('mocha', 'Mocha', 170, PricingType.unit),
      _item('combo', 'Mocha Combo', 200, PricingType.combo),
    ]);
  await tester.binding.setSurfaceSize(const Size(800, 1600));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  await tester.pumpWidget(
    ProviderScope(
      overrides: [
        posRepositoryProvider.overrideWithValue(repository),
        catalogRepositoryProvider.overrideWithValue(catalog),
      ],
      child: const MaterialApp(home: ExchangeScreen(sale: _sale)),
    ),
  );
  await tester.pumpAndSettle();
  return repository;
}

Future<void> _fillAndPick(WidgetTester tester) async {
  await tester.tap(find.byTooltip('Increase Iced Latte'));
  await tester.enterText(find.byKey(const Key('exchange-search')), 'moc');
  await tester.pumpAndSettle();
  await tester.tap(find.widgetWithText(OutlinedButton, 'Mocha'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('caps the return quantity at what earlier exchanges left', (tester) async {
    final repository = await _open(tester, returnable: {'line-1': 1});

    expect(find.textContaining('1 left to return'), findsOneWidget);
    await tester.tap(find.byTooltip('Increase Iced Latte'));
    await tester.pump();
    final increase = tester.widget<IconButton>(
      find.widgetWithIcon(IconButton, Icons.add_circle_outline),
    );
    expect(increase.onPressed, isNull);
    expect(repository.lastExchange, isNull);
  });

  testWidgets('only offers items an exchange supports', (tester) async {
    await _open(tester);
    await tester.enterText(find.byKey(const Key('exchange-search')), 'moc');
    await tester.pumpAndSettle();

    expect(find.text('Mocha'), findsOneWidget);
    expect(find.text('Mocha Combo'), findsNothing);
  });

  testWidgets('previews what the customer pays and sends the exchange', (tester) async {
    final repository = await _open(tester);
    repository.exchangeResult = _result;
    await _fillAndPick(tester);

    expect(find.text('Customer pays'), findsOneWidget);
    await tester.enterText(find.byKey(const Key('exchange-tendered')), '200');
    await tester.enterText(find.byKey(const Key('exchange-reason')), 'Wrong drink');
    await tester.enterText(find.byKey(const Key('exchange-pin')), '1234');
    await tester.pumpAndSettle();
    expect(find.text('Change: ₱180.00'), findsOneWidget);

    await tester.ensureVisible(find.byKey(const Key('record-exchange')));
    await tester.tap(find.byKey(const Key('record-exchange')));
    await tester.pumpAndSettle();

    final sent = repository.lastExchange!;
    expect(sent.returnLines.single.originalLineId, 'line-1');
    expect(sent.returnLines.single.quantity, 1);
    expect(sent.replacementLines.single.itemId, 'mocha');
    expect(sent.settlementMethod, PaymentMethod.cash);
    expect(sent.settlementAmountTendered, 200);
    expect(sent.approverPin, '1234');
    expect(find.text('Exchange recorded'), findsOneWidget);
  });

  testWidgets('blocks recording until cash covers the amount owed', (tester) async {
    await _open(tester);
    await _fillAndPick(tester);
    await tester.enterText(find.byKey(const Key('exchange-tendered')), '10');
    await tester.enterText(find.byKey(const Key('exchange-reason')), 'Wrong drink');
    await tester.enterText(find.byKey(const Key('exchange-pin')), '1234');
    await tester.pumpAndSettle();

    final button = tester.widget<FilledButton>(find.byKey(const Key('record-exchange')));
    expect(button.onPressed, isNull);
  });

  testWidgets("keeps the form filled in and shows the server's message when it refuses", (tester) async {
    final repository = await _open(tester);
    repository.exchangeFailure = const ValidationFailure('Only 0 of that line is still available to return.', {});
    await _fillAndPick(tester);
    await tester.enterText(find.byKey(const Key('exchange-tendered')), '200');
    await tester.enterText(find.byKey(const Key('exchange-reason')), 'Wrong drink');
    await tester.enterText(find.byKey(const Key('exchange-pin')), '1234');
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.byKey(const Key('record-exchange')));
    await tester.tap(find.byKey(const Key('record-exchange')));
    await tester.pumpAndSettle();

    expect(find.text('Only 0 of that line is still available to return.'), findsOneWidget);
    expect(find.text('Wrong drink'), findsOneWidget);
  });
}
