import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/kiosk/presentation/providers/kiosk_providers.dart';
import 'package:purch_client/features/kiosk/presentation/screens/kiosk_cart_screen.dart';
import 'package:purch_client/features/kiosk/presentation/screens/kiosk_fulfillment_screen.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';

import '../../../helpers/fake_kiosk_cart_repository.dart';

Widget _wrap(Widget child, {required FakeKioskCartRepository repository}) {
  return ProviderScope(
    overrides: [kioskCartRepositoryProvider.overrideWithValue(repository)],
    child: MaterialApp(home: child),
  );
}

void main() {
  testWidgets('shows an empty state when the order has no lines', (
    tester,
  ) async {
    final repository = FakeKioskCartRepository();
    await tester.pumpWidget(
      _wrap(const KioskCartScreen(), repository: repository),
    );
    await tester.pumpAndSettle();

    expect(
      find.text('Your order is empty — go back and add something.'),
      findsOneWidget,
    );
  });

  testWidgets('shows line totals, the running total, and enables Continue', (
    tester,
  ) async {
    final line = TransactionLine(
      id: 'line-1',
      itemId: 'item-1',
      itemName: 'Rice Meal',
      itemVariantId: null,
      quantity: 2,
      unitPrice: 85,
      lineTotal: 170,
      comboSelections: const [],
    );
    final cart = Transaction(
      id: 'cart-1',
      branchId: 'branch-1',
      deviceId: 'device-1',
      status: TransactionStatus.open,
      lines: [line],
      subtotal: 170,
      discountAmount: 0,
      seniorPwdDiscountApplied: false,
      promoCode: null,
      promoDiscountAmount: 0,
      totalAmount: 170,
      receiptNumber: null,
      payments: const [],
    );
    final repository = FakeKioskCartRepository(initialCart: cart);

    await tester.pumpWidget(
      _wrap(const KioskCartScreen(), repository: repository),
    );
    await tester.pumpAndSettle();

    expect(find.text('Rice Meal'), findsOneWidget);
    expect(find.text('₱170.00'), findsWidgets);

    await tester.tap(find.text('Continue'));
    await tester.pumpAndSettle();

    expect(find.byType(KioskFulfillmentScreen), findsOneWidget);
  });
}
