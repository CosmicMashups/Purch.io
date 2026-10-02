import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';
import 'package:purch_client/features/pos/presentation/providers/pos_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/find_sale_screen.dart';

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
      quantity: 1,
      unitPrice: 150,
      lineTotal: 150,
      comboSelections: [],
    ),
  ],
  subtotal: 150,
  discountAmount: 0,
  seniorPwdDiscountApplied: false,
  promoCode: null,
  promoDiscountAmount: 0,
  totalAmount: 150,
  receiptNumber: 1047,
  payments: [],
);

Widget _wrap(FakePosRepository repository) {
  return ProviderScope(
    overrides: [posRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: FindSaleScreen()),
  );
}

void main() {
  testWidgets('rejects a non-numeric receipt number without calling the repository', (tester) async {
    final repository = FakePosRepository(initialCart: _sale);
    await tester.pumpWidget(_wrap(repository));

    await tester.enterText(find.byType(TextField), 'abc');
    await tester.tap(find.widgetWithText(FilledButton, 'Find'));
    await tester.pumpAndSettle();

    expect(find.text('Enter the receipt number as it appears on the printed receipt.'), findsOneWidget);
  });

  testWidgets('shows a not-found message when nothing matches', (tester) async {
    final repository = FakePosRepository(initialCart: _sale)..foundByReceiptNumber = [];
    await tester.pumpWidget(_wrap(repository));

    await tester.enterText(find.byType(TextField), '999');
    await tester.tap(find.widgetWithText(FilledButton, 'Find'));
    await tester.pumpAndSettle();

    expect(find.text('No completed sale found with receipt number 999.'), findsOneWidget);
  });

  testWidgets('shows the sale summary when exactly one matches, with a Refund button', (tester) async {
    final repository = FakePosRepository(initialCart: _sale)..foundByReceiptNumber = [_sale];
    await tester.pumpWidget(_wrap(repository));

    await tester.enterText(find.byType(TextField), '1047');
    await tester.tap(find.widgetWithText(FilledButton, 'Find'));
    await tester.pumpAndSettle();

    expect(find.text('Receipt No. 1047'), findsOneWidget);
    expect(find.text('Iced Latte x1'), findsOneWidget);
    expect(find.text('Refund'), findsOneWidget);
  });

  testWidgets('lets the cashier pick when more than one sale shares a receipt number', (tester) async {
    const other = Transaction(
      id: 'sale-2',
      branchId: 'branch-1',
      deviceId: 'device-2',
      status: TransactionStatus.completed,
      lines: [
        TransactionLine(
          id: 'line-2',
          itemId: 'item-2',
          itemName: 'Mocha',
          itemVariantId: null,
          quantity: 1,
          unitPrice: 170,
          lineTotal: 170,
          comboSelections: [],
        ),
      ],
      subtotal: 170,
      discountAmount: 0,
      seniorPwdDiscountApplied: false,
      promoCode: null,
      promoDiscountAmount: 0,
      totalAmount: 170,
      receiptNumber: 7,
      payments: [],
    );
    final repository = FakePosRepository(initialCart: _sale)..foundByReceiptNumber = [_sale, other];
    await tester.pumpWidget(_wrap(repository));

    await tester.enterText(find.byType(TextField), '7');
    await tester.tap(find.widgetWithText(FilledButton, 'Find'));
    await tester.pumpAndSettle();

    expect(find.text('Mocha'), findsOneWidget);
    await tester.tap(find.text('Mocha'));
    await tester.pumpAndSettle();

    expect(find.text('Receipt No. 7'), findsOneWidget);
  });

  testWidgets('refunding the found sale needs a reason and PIN', (tester) async {
    final repository = FakePosRepository(initialCart: _sale)..foundByReceiptNumber = [_sale];
    await tester.pumpWidget(_wrap(repository));

    await tester.enterText(find.byType(TextField), '1047');
    await tester.tap(find.widgetWithText(FilledButton, 'Find'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Refund'));
    await tester.pumpAndSettle();

    await tester.enterText(find.byKey(const Key('refund-reason')), 'Customer changed their mind');
    await tester.pump();
    await tester.enterText(find.byKey(const Key('refund-pin')), '1234');
    await tester.pump();
    await tester.tap(find.byKey(const Key('confirm-refund')));
    await tester.pumpAndSettle();

    expect(repository.cart.status, TransactionStatus.refunded);
    expect(find.text('REFUNDED'), findsOneWidget);
  });
}
