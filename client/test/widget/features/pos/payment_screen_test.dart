import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/pos/domain/payment_method.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';
import 'package:purch_client/features/pos/presentation/providers/pos_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/payment_screen.dart';

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
  promoCode: null,
  promoDiscountAmount: 0,
  totalAmount: 30,
  receiptNumber: null,
  payments: [],
);

Widget _wrap(FakePosRepository repository) {
  return ProviderScope(
    overrides: [posRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: PaymentScreen(total: 30)),
  );
}

void main() {
  testWidgets('Confirm Payment stays disabled until a method is chosen', (
    tester,
  ) async {
    final repository = FakePosRepository(initialCart: _cartWithOneLine);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    final confirmButton = tester.widget<FilledButton>(
      find.widgetWithText(FilledButton, 'Confirm Payment'),
    );
    expect(confirmButton.onPressed, isNull);
  });

  testWidgets('paying cash requires tendered amount to cover the total', (
    tester,
  ) async {
    final repository = FakePosRepository(initialCart: _cartWithOneLine);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Cash'));
    await tester.pumpAndSettle();

    var confirmButton = tester.widget<FilledButton>(
      find.widgetWithText(FilledButton, 'Confirm Payment'),
    );
    expect(confirmButton.onPressed, isNull);

    await tester.enterText(
      find.widgetWithText(TextField, 'Cash tendered'),
      '50',
    );
    await tester.pumpAndSettle();

    expect(find.text('Change: ₱20.00'), findsOneWidget);
    confirmButton = tester.widget<FilledButton>(
      find.widgetWithText(FilledButton, 'Confirm Payment'),
    );
    expect(confirmButton.onPressed, isNotNull);

    final confirmFinder = find.widgetWithText(FilledButton, 'Confirm Payment');
    await tester.ensureVisible(confirmFinder);
    await tester.tap(confirmFinder);
    await tester.pumpAndSettle();

    expect(repository.lastRecordPaymentRequest?.method, PaymentMethod.cash);
    expect(repository.lastRecordPaymentRequest?.amountTendered, 50);
    expect(find.text('Receipt'), findsOneWidget);
  });

  testWidgets('bank transfer confirms without needing a tendered amount', (
    tester,
  ) async {
    final repository = FakePosRepository(initialCart: _cartWithOneLine);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Bank Transfer'));
    await tester.pumpAndSettle();
    final confirmFinder = find.widgetWithText(FilledButton, 'Confirm Payment');
    await tester.ensureVisible(confirmFinder);
    await tester.tap(confirmFinder);
    await tester.pumpAndSettle();

    expect(
      repository.lastRecordPaymentRequest?.method,
      PaymentMethod.bankTransfer,
    );
  });

  testWidgets('QR Ph is shown but disabled', (tester) async {
    final repository = FakePosRepository(initialCart: _cartWithOneLine);
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.text('QR Ph'), findsOneWidget);
    expect(find.textContaining('Not yet available'), findsWidgets);
  });
}
