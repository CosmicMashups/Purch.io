import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/inventory/domain/inventory_movement_models.dart';
import 'package:purch_client/features/inventory/presentation/providers/inventory_providers.dart';
import 'package:purch_client/features/inventory/presentation/screens/movement_log_screen.dart';

import '../../../helpers/fake_inventory_repository.dart';

final _stockInMovement = InventoryMovement(
  id: 'movement-1',
  itemId: 'item-1',
  itemName: 'Bottled Water',
  branchId: 'branch-1',
  branchName: 'Main Branch',
  type: MovementType.stockIn,
  quantity: 50,
  staffUserId: 'user-1',
  staffUserName: 'Admin User',
  note: 'Initial delivery',
  reasonCategory: null,
  photoUrl: null,
  supplierReference: null,
  createdAt: DateTime(2026, 1, 1),
);

final _spoiledMovement = InventoryMovement(
  id: 'movement-2',
  itemId: 'item-2',
  itemName: 'Milk',
  branchId: 'branch-1',
  branchName: 'Main Branch',
  type: MovementType.spoiled,
  quantity: 3,
  staffUserId: 'user-1',
  staffUserName: 'Admin User',
  note: null,
  reasonCategory: 'Expired',
  photoUrl: null,
  supplierReference: null,
  createdAt: DateTime(2026, 1, 1),
);

Widget _wrap(FakeInventoryRepository repository) {
  return ProviderScope(
    overrides: [inventoryRepositoryProvider.overrideWithValue(repository)],
    child: const MaterialApp(home: MovementLogScreen()),
  );
}

void main() {
  testWidgets('shows an empty state when there are no movements', (
    tester,
  ) async {
    await tester.pumpWidget(_wrap(FakeInventoryRepository()));
    await tester.pumpAndSettle();

    expect(find.text('No movements recorded yet.'), findsOneWidget);
  });

  testWidgets('lists movements with their type label', (tester) async {
    final repository = FakeInventoryRepository(
      initialMovements: [_stockInMovement, _spoiledMovement],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    expect(find.text('Bottled Water'), findsOneWidget);
    expect(find.text('Milk'), findsOneWidget);
  });

  testWidgets('filtering by a type chip narrows the list', (tester) async {
    final repository = FakeInventoryRepository(
      initialMovements: [_stockInMovement, _spoiledMovement],
    );
    await tester.pumpWidget(_wrap(repository));
    await tester.pumpAndSettle();

    await tester.tap(find.widgetWithText(ChoiceChip, 'Spoiled'));
    await tester.pumpAndSettle();

    expect(find.text('Milk'), findsOneWidget);
    expect(find.text('Bottled Water'), findsNothing);
  });
}
