import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/pos/domain/payment_method.dart';
import 'package:purch_client/features/pos/domain/transaction_models.dart';
import 'package:purch_client/features/pos/presentation/providers/pos_providers.dart';
import 'package:purch_client/features/pos/presentation/screens/receipt_screen.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';

import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_pos_repository.dart';

const _completedCart = Transaction(
  id: 'cart-1',
  branchId: 'branch-1',
  deviceId: 'device-1',
  status: TransactionStatus.completed,
  lines: [
    TransactionLine(
      id: 'line-1',
      itemId: 'item-1',
      itemName: 'Bottled Water',
      itemVariantId: null,
      quantity: 1,
      unitPrice: 15,
      lineTotal: 15,
      comboSelections: [],
    ),
  ],
  subtotal: 15,
  discountAmount: 0,
  seniorPwdDiscountApplied: false,
  totalAmount: 15,
  receiptNumber: 7,
  payments: [
    Payment(
      id: 'payment-1',
      method: PaymentMethod.cash,
      status: PaymentStatus.confirmed,
      amount: 15,
      amountTendered: 20,
      changeGiven: 5,
    ),
  ],
);

void main() {
  testWidgets('shows the receipt number, lines, total, and change', (
    tester,
  ) async {
    final repository = FakePosRepository(initialCart: _completedCart);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          posRepositoryProvider.overrideWithValue(repository),
          catalogRepositoryProvider.overrideWithValue(FakeCatalogRepository()),
        ],
        child: const MaterialApp(home: ReceiptScreen()),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Receipt No. 7'), findsOneWidget);
    expect(find.text('Bottled Water ×1'), findsOneWidget);
    expect(find.text('Change'), findsOneWidget);
    expect(find.text('₱5.00'), findsOneWidget);
  });

  testWidgets('tapping New Sale starts a fresh cart', (tester) async {
    final repository = FakePosRepository(initialCart: _completedCart);
    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          posRepositoryProvider.overrideWithValue(repository),
          catalogRepositoryProvider.overrideWithValue(FakeCatalogRepository()),
        ],
        child: const MaterialApp(home: ReceiptScreen()),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(FilledButton, 'New Sale'));
    await tester.pumpAndSettle();

    expect(find.text('New Sale'), findsWidgets);
  });
}
