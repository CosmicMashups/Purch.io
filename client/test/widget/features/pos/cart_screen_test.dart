import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';
import 'package:purch_client/features/pos/presentation/providers/pos_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/cart_screen.dart';

import '../../../helpers/fake_pos_repository.dart';

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
  totalAmount: 30,
  receiptNumber: null,
  payments: [],
);

Widget _wrap(FakePosRepository repository) {
  return ProviderScope(
    overrides: [posRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: CartScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when the cart has no lines', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakePosRepository()));
    await tester.pumpAndSettle();

    expect(
      find.text('Cart is empty — go back and add an item.'),
      findsOneWidget,
    );
  });

  testWidgets('shows line totals and the running total', (tester) async {
    final repository = FakePosRepository(initialCart: _cartWithOneLine);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.text('Bottled Water'), findsOneWidget);
    expect(find.text('₱30.00'), findsWidgets);
  });

  testWidgets('tapping + increases the line quantity', (tester) async {
    final repository = FakePosRepository(initialCart: _cartWithOneLine);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.add_circle_outline));
    await tester.pumpAndSettle();

    expect(repository.cart.lines.single.quantity, 3);
  });

  testWidgets('tapping remove clears the line', (tester) async {
    final repository = FakePosRepository(initialCart: _cartWithOneLine);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.delete_outline));
    await tester.pumpAndSettle();

    expect(repository.cart.lines, isEmpty);
  });

  testWidgets('voiding the cart after confirming starts a fresh one', (
    tester,
  ) async {
    final repository = FakePosRepository(initialCart: _cartWithOneLine);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byIcon(Icons.delete_sweep));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Void'));
    await tester.pumpAndSettle();

    expect(repository.voidCallCount, 1);
    expect(
      find.text('Cart is empty — go back and add an item.'),
      findsOneWidget,
    );
  });

  testWidgets('toggling the senior/PWD switch applies the 20% discount', (
    tester,
  ) async {
    final repository = FakePosRepository(initialCart: _cartWithOneLine);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.byType(SwitchListTile));
    await tester.pumpAndSettle();

    expect(repository.cart.seniorPwdDiscountApplied, isTrue);
    expect(find.text('₱-6.00'), findsOneWidget);
    expect(find.text('₱24.00'), findsOneWidget);
  });
}
