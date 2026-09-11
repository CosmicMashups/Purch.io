import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/inventory/domain/purchase_order_models.dart';
import 'package:purch_client/features/inventory/presentation/providers/purchase_order_providers.dart';
import 'package:purch_client/features/inventory/presentation/screens/receive_purchase_order_screen.dart';

import '../../../helpers/fake_purchase_order_repository.dart';

const _sentPurchaseOrder = PurchaseOrder(
  id: 'po-1',
  supplierId: 'supplier-1',
  supplierName: 'Acme Distribution',
  branchId: 'branch-1',
  branchName: 'Main Branch',
  status: PurchaseOrderStatus.sent,
  sentAt: null,
  lines: [
    PurchaseOrderLine(
      id: 'line-1',
      itemId: 'item-1',
      itemName: 'Bottled Water',
      quantityOrdered: 100,
      quantityReceived: 0,
      expectedUnitCost: 10,
    ),
  ],
);

Widget _wrap(FakePurchaseOrderRepository repository) {
  return ProviderScope(
    overrides: [purchaseOrderRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(
      home: ReceivePurchaseOrderScreen(purchaseOrder: _sentPurchaseOrder),
    ),
  );
}

void main() {
  testWidgets('receiving a partial quantity submits it against the line', (
    tester,
  ) async {
    final repository = FakePurchaseOrderRepository(
      initialPurchaseOrders: [_sentPurchaseOrder],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.text('100 remaining of 100 ordered'), findsOneWidget);

    await tester.enterText(
      find.widgetWithText(TextField, 'Bottled Water'),
      '40',
    );
    await tester.pump();
    await tester.tap(find.widgetWithText(FilledButton, 'Receive'));
    await tester.pumpAndSettle();

    expect(repository.lastReceiveRequest?.lines.single.lineId, 'line-1');
    expect(repository.lastReceiveRequest?.lines.single.receivedQuantity, 40);
  });

  testWidgets('receiving more than remaining is blocked', (tester) async {
    final repository = FakePurchaseOrderRepository(
      initialPurchaseOrders: [_sentPurchaseOrder],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.enterText(
      find.widgetWithText(TextField, 'Bottled Water'),
      '150',
    );
    await tester.pump();

    final receiveButton = tester.widget<FilledButton>(
      find.widgetWithText(FilledButton, 'Receive'),
    );
    expect(receiveButton.onPressed, isNull);
  });
}
