import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/inventory/domain/purchase_order_models.dart';
import 'package:purch_client/features/inventory/presentation/providers/purchase_order_providers.dart';
import 'package:purch_client/features/inventory/presentation/screens/purchase_order_list_screen.dart';

import '../../../helpers/fake_purchase_order_repository.dart';

const _draftPurchaseOrder = PurchaseOrder(
  id: 'po-1',
  supplierId: 'supplier-1',
  supplierName: 'Acme Distribution',
  branchId: 'branch-1',
  branchName: 'Main Branch',
  status: PurchaseOrderStatus.draft,
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
    child: const MaterialApp(home: PurchaseOrderListScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no purchase orders', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakePurchaseOrderRepository()));
    await tester.pumpAndSettle();

    expect(
      find.text('No purchase orders yet — tap + to create one.'),
      findsOneWidget,
    );
  });

  testWidgets('a draft PO can be marked sent', (tester) async {
    final repository = FakePurchaseOrderRepository(
      initialPurchaseOrders: [_draftPurchaseOrder],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.text('Acme Distribution → Main Branch'), findsOneWidget);
    expect(find.text('Draft'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, 'Mark Sent'));
    await tester.pumpAndSettle();

    expect(repository.purchaseOrders.single.status, PurchaseOrderStatus.sent);
    expect(find.text('Receive'), findsOneWidget);
  });
}
