import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/features/catalog/domain/item_models.dart';
import 'package:purch_client/features/catalog/domain/pricing_type.dart';
import 'package:purch_client/features/catalog/domain/tingi_mode.dart';
import 'package:purch_client/features/catalog/presentation/providers/catalog_providers.dart';
import 'package:purch_client/features/inventory/domain/inventory_movement_models.dart';
import 'package:purch_client/features/inventory/presentation/providers/inventory_providers.dart';
import 'package:purch_client/features/inventory/presentation/screens/inventory_dashboard_screen.dart';
import 'package:purch_client/features/onboarding/presentation/providers/onboarding_providers.dart';

import '../../../helpers/fake_catalog_repository.dart';
import '../../../helpers/fake_inventory_repository.dart';
import '../../../helpers/fake_onboarding_repository.dart';

const _sugar = Item(
  id: 'item-1',
  name: 'Sugar',
  sku: null,
  barcode: null,
  categoryId: null,
  basePrice: 20,
  imageUrl: null,
  pricingType: PricingType.unit,
  stockOnHand: 3,
  isActive: true,
  tingiMode: TingiMode.none,
  packagedSize: null,
  tingiIncrementStep: null,
  tingiAllowedSizes: [],
  serviceDurationMinutes: null,
  departmentId: null,
  lowStockThreshold: 5,
);

const _dashboardWithAlert = InventoryDashboard(
  totalSkus: 10,
  outOfStockCount: 1,
  lowStockCount: 1,
  lowStockItems: [
    LowStockItem(
      itemId: 'item-1',
      itemName: 'Sugar',
      stockOnHand: 3,
      lowStockThreshold: 5,
    ),
  ],
);

const _emptyDashboard = InventoryDashboard(
  totalSkus: 0,
  outOfStockCount: 0,
  lowStockCount: 0,
  lowStockItems: [],
);

Widget _wrap(
  FakeInventoryRepository inventoryRepository,
  FakeCatalogRepository catalogRepository,
) {
  return ProviderScope(
    overrides: [
      inventoryRepositoryProvider.overrideWithValue(inventoryRepository),
      catalogRepositoryProvider.overrideWithValue(catalogRepository),
      onboardingRepositoryProvider.overrideWithValue(
        FakeOnboardingRepository(),
      ),
    ],
    child: const MaterialApp(home: InventoryDashboardScreen()),
  );
}

void main() {
  testWidgets('shows the stat cards and an empty low-stock state', (
    tester,
  ) async {
    final repository = FakeInventoryRepository(
      initialDashboard: _emptyDashboard,
    );
    await tester.pumpWidget(_wrap(repository, FakeCatalogRepository()));
    await tester.pumpAndSettle();

    expect(find.text('0'), findsWidgets);
    expect(find.text('Nothing is running low right now.'), findsOneWidget);
  });

  testWidgets('lists a low-stock alert with a reorder shortcut', (
    tester,
  ) async {
    final repository = FakeInventoryRepository(
      initialDashboard: _dashboardWithAlert,
    );
    final catalogRepository = FakeCatalogRepository(initialItems: [_sugar]);
    await tester.pumpWidget(_wrap(repository, catalogRepository));
    await tester.pumpAndSettle();

    expect(find.text('Sugar'), findsOneWidget);
    expect(find.text('Reorder'), findsOneWidget);

    await tester.tap(find.text('Reorder'));
    await tester.pumpAndSettle();

    expect(find.text('Record Movement'), findsWidgets);
  });
}
