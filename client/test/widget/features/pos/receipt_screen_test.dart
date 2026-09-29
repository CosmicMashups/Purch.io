import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/errors/failure.dart';
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
  promoCode: null,
  promoDiscountAmount: 0,
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

    // "New Sale" now lands on the merged Cashier screen rather than the
    // standalone item grid.
    expect(find.text('Cashier'), findsWidgets);
  });

  testWidgets(
    'refunding needs a reason and a manager PIN, and retries after a wrong one',
    (tester) async {
      final repository = FakePosRepository(initialCart: _completedCart);
      repository.refundFailure = const ValidationFailure(
        "That PIN doesn't match a different active manager or admin.",
        {
          'approverPin': [
            "That PIN doesn't match a different active manager or admin.",
          ],
        },
      );
      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            posRepositoryProvider.overrideWithValue(repository),
            catalogRepositoryProvider.overrideWithValue(
              FakeCatalogRepository(),
            ),
          ],
          child: const MaterialApp(home: ReceiptScreen()),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('open-refund-dialog')));
      await tester.pumpAndSettle();

      final approveButton = find.byKey(const Key('confirm-refund'));
      expect(tester.widget<FilledButton>(approveButton).onPressed, isNull);

      await tester.enterText(
        find.byKey(const Key('refund-reason')),
        'Customer changed their mind',
      );
      await tester.pump();
      await tester.enterText(find.byKey(const Key('refund-pin')), '0000');
      await tester.pump();
      await tester.tap(approveButton);
      await tester.pumpAndSettle();

      expect(
        find.text(
          "That PIN doesn't match a different active manager or admin.",
        ),
        findsOneWidget,
      );
      expect(repository.cart.status, TransactionStatus.completed);

      repository.refundFailure = null;
      await tester.enterText(find.byKey(const Key('refund-pin')), '5678');
      await tester.tap(approveButton);
      await tester.pumpAndSettle();

      expect(repository.lastRefundApproverPin, '5678');
      expect(find.text('REFUNDED'), findsOneWidget);
      expect(find.byKey(const Key('open-refund-dialog')), findsNothing);
    },
  );
}
