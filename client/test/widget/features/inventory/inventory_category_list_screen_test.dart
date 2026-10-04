import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/routing/auth_gate.dart';
import 'package:purch_client/features/inventory/domain/inventory_item_models.dart';
import 'package:purch_client/features/inventory/presentation/providers/inventory_providers.dart';
import 'package:purch_client/features/inventory/presentation/screens/inventory_category_list_screen.dart';
import 'package:purch_client/features/onboarding/domain/onboarding_enums.dart';

import '../../../helpers/fake_inventory_repository.dart';

Widget _wrap(FakeInventoryRepository repository, StaffRole role) {
  return ProviderScope(
    overrides: [
      inventoryRepositoryProvider.overrideWithValue(repository),
      currentStaffRoleProvider.overrideWith((ref) async => role),
    ],
    child: const MaterialApp(home: InventoryCategoryListScreen()),
  );
}

void main() {
  testWidgets('a manager adds a category and sees it listed', (tester) async {
    final repository = FakeInventoryRepository();
    await tester.pumpWidget(_wrap(repository, StaffRole.manager));
    await tester.pumpAndSettle();

    expect(find.text('No ingredient categories yet.'), findsOneWidget);

    await tester.tap(find.byIcon(Icons.add));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextFormField, 'Name'), 'Dairy');
    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();

    expect(find.text('Dairy'), findsOneWidget);
    expect(repository.inventoryCategories.single.name, 'Dairy');
  });

  testWidgets('deleting a category leaves its ingredients uncategorised', (
    tester,
  ) async {
    final repository = FakeInventoryRepository();
    repository.inventoryCategories.add(
      const InventoryCategory(id: 'dairy', name: 'Dairy', sortOrder: 1),
    );
    repository.inventoryItems.add(
      const InventoryItem(
        id: 'milk',
        name: 'Milk',
        sku: null,
        baseUnit: 'mL',
        packagingUnit: 'carton',
        packagingSize: 1000,
        quantityOnHand: 5000,
        lowStockThreshold: null,
        isAutoCreatedForItem: false,
        linkedItemId: null,
        isActive: true,
        categoryId: 'dairy',
      ),
    );
    await tester.pumpWidget(_wrap(repository, StaffRole.admin));
    await tester.pumpAndSettle();

    expect(find.textContaining('1 ingredient'), findsOneWidget);

    await tester.tap(find.byTooltip('Delete category'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Delete'));
    await tester.pumpAndSettle();

    expect(repository.inventoryCategories, isEmpty);
    expect(repository.inventoryItems.single.categoryId, isNull);
  });

  testWidgets('a warehouse user can read categories but not change them', (
    tester,
  ) async {
    final repository = FakeInventoryRepository();
    repository.inventoryCategories.add(
      const InventoryCategory(id: 'dairy', name: 'Dairy', sortOrder: 1),
    );
    await tester.pumpWidget(_wrap(repository, StaffRole.warehouse));
    await tester.pumpAndSettle();

    expect(find.text('Dairy'), findsOneWidget);
    expect(find.byIcon(Icons.add), findsNothing);
    expect(find.byTooltip('Delete category'), findsNothing);
  });
}
